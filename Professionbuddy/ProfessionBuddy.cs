//!CompilerOption:Optimize:On
//!CompilerOption:AddRef:WindowsBase.dll

// Professionbuddy botbase by HighVoltz

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.IO.Packaging;

using Styx;
using TreeSharp;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.Logic;
using Styx.Logic.Combat;
using System.Diagnostics;
using Styx.Patchables;
using Styx.Plugins;
using Styx.Plugins.PluginClass;
using Styx.Logic.Pathing;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using Styx.Combat.CombatRoutine;
using Styx.Logic.POI;
using HighVoltz.Composites;
using System.Reflection;
using Styx.Logic.Profiles;
using System.Collections.Specialized;
using Action = TreeSharp.Action;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows.Documents;
using System.Windows.Threading;

namespace HighVoltz
{
    public partial class Professionbuddy : BotBase
    {
        #region Declarations
        public ProfessionBuddySettings MySettings;
        public List<TradeSkill> TradeSkillList { get; private set; }
        // <itemId,count>
        public DataStore DataStore { get; private set; }
        // path to the currently loaded HB profile
        public static string HonorBuddyProfilePath { get; set; }
        // dictionary that keeps track of material list using item ID for key and number required as value
        public Dictionary<uint, int> MaterialList { get; private set; }
        public List<uint> ProtectedItems { get; private set; }
        public bool IsTradeSkillsLoaded { get; private set; }

        static readonly string _name = "ProfessionBuddy";
        static string _botPath = Logging.ApplicationPath + @"\Bots\" + _name;
        public string BotPath { get { return _botPath; } }

        static string _profilePath = Environment.UserName == "highvoltz" ?
                        @"C:\Users\highvoltz\Desktop\Buddy\Projects\Professionbuddy\Profiles" : Path.Combine(_botPath, "Profiles");
        public string ProfilePath { get { return _profilePath; } }

        public event EventHandler OnTradeSkillsLoaded;
        public readonly LocalPlayer Me = ObjectManager.Me;
        Svn _svn = new Svn();
        // DataStore is an addon for WOW thats stores bag/ah/mail item info and more.
        public bool HasDataStoreAddon { get { return DataStore != null ? DataStore.HasDataStoreAddon : false; } }
        // profile Settings.
        public PbProfileSettings ProfileSettings { get; private set; }
        public bool IsRunning = false;
        // static instance
        public static Professionbuddy Instance { get; private set; }
        public Version Version { get { return new Version(1, _svn.Revision); } }
        // test some culture specific stuff.
        public Professionbuddy()
        {
            Instance = this;
            new Thread((ThreadStart)delegate
                {
                    try
                    {
                        var mod = Process.GetCurrentProcess().MainModule;
                        using (HashAlgorithm hashAlg = new SHA1Managed())
                        {
                            using (Stream file = new FileStream(mod.FileName, FileMode.Open, FileAccess.Read))
                            {
                                byte[] hash = hashAlg.ComputeHash(file);
                                Logging.WriteDebug("H: {0}", BitConverter.ToString(hash));
                            }
                        }
                        var vInfo = mod.FileVersionInfo;
                        Logging.WriteDebug("V: {0}", vInfo.FileVersion);
                    }
                    catch { }
                }).Start();
            // Initialize is called when bot is started.. we need to hook these events before that.
            Styx.BotEvents.Profile.OnNewOuterProfileLoaded += new BotEvents.Profile.NewProfileLoadedDelegate(Profile_OnNewOuterProfileLoaded);
            Styx.Logic.Profiles.Profile.OnUnknownProfileElement += new EventHandler<UnknownProfileElementEventArgs>(Profile_OnUnknownProfileElement);
        }
        #endregion

        #region Overrides
        public override string Name
        {
            get { return _name; }
        }

        public override PulseFlags PulseFlags { get { return Styx.PulseFlags.All; } }


        public override void Start()
        {
            Debug("Start Called");
            IsRunning = true;

            // reset all actions 
            foreach (IPBComposite comp in CurrentProfile.Branch.Children)
            {
                comp.Reset();
            }
            if (DynamicCode.CodeWasModified)
            {
                DynamicCode.GenorateDynamicCode();
            }

            if (MainForm.IsValid)
                MainForm.Instance.UpdateControls();
            try
            {
                if (SecondaryBot != null)
                    SecondaryBot.Start();
            }
            catch (Exception ex)
            {
                Err("{0} {1}", SecondaryBot.Name, ex);
            }
        }

        public override void Stop()
        {
            //Styx.BotEvents.Profile.OnNewOuterProfileLoaded -= new BotEvents.Profile.NewProfileLoadedDelegate(Profile_OnNewOuterProfileLoaded);
            //Styx.Logic.Profiles.Profile.OnUnknownProfileElement -= new EventHandler<UnknownProfileElementEventArgs>(Profile_OnUnknownProfileElement);
            IsRunning = false;
            Debug("Stop Called");
            if (MainForm.IsValid)
                MainForm.Instance.UpdateControls();
            //Lua.Events.DetachEvent("BAG_UPDATE", OnBagUpdate);
            //Lua.Events.DetachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
            //Lua.Events.DetachEvent("SPELLS_CHANGED", OnSpellsChanged);
            //Lua.Events.DetachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
            //Lua.Events.DetachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
            if (SecondaryBot != null)
                SecondaryBot.Stop();
        }

        // used as a hack to get a modeless form for pb's GUI window. 
        InvisiForm _ivisibleForm;
        public override Form ConfigurationForm
        {
            get
            {
                if (!_init)
                    Init();
                return _ivisibleForm ?? (_ivisibleForm = new InvisiForm());
            }
        }

        class InvisiForm : Form
        {
            public InvisiForm()
            {
                Size = new System.Drawing.Size(0, 0);
            }
            protected override void OnLoad(EventArgs e)
            {
                if (!MainForm.IsValid)
                {
                    new MainForm() { TopLevel = true }.Show();
                    MainForm.Instance.TopLevel = true;
                }
                DialogResult = System.Windows.Forms.DialogResult.OK;
                MainForm.Instance.Activate();
            }
        }

        public override void Pulse()
        {
            // Due to some non-thread safe HB api I need to make sure the callbacks are executed from main thread
            // Throttling the events so the callback doesn't get called repeatedly in a short time frame.
            if (OnBagUpdateSpamSW.ElapsedMilliseconds >= 1000)
            {
                OnBagUpdateSpamSW.Stop();
                OnBagUpdateSpamSW.Reset();
                OnBagUpdateTimerCB(null);
            }
            if (OnSkillUpdateSpamSW.ElapsedMilliseconds >= 1000)
            {
                OnSkillUpdateSpamSW.Stop();
                OnSkillUpdateSpamSW.Reset();
                OnSkillUpdateTimerCB(null);
            }
            if (OnSpellsChangedSpamSW.ElapsedMilliseconds >= 1000)
            {
                OnSpellsChangedSpamSW.Stop();
                OnSpellsChangedSpamSW.Reset();
                OnSpellsChangedCB(null);
            }
        }

        public override void Initialize()
        {
            Init();
        }

        #endregion

        #region Callbacks

        #region OnBagUpdate
        Stopwatch OnBagUpdateSpamSW = new Stopwatch();
        public void OnBagUpdate(object obj, LuaEventArgs args)
        {
            if (!OnBagUpdateSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("BAG_UPDATE", OnBagUpdate);
                OnBagUpdateSpamSW.Start();
            }
        }

        void OnBagUpdateTimerCB(Object stateInfo)
        {
            try
            {
                Lua.Events.AttachEvent("BAG_UPDATE", OnBagUpdate);
                foreach (TradeSkill ts in TradeSkillList)
                {
                    ts.PulseBags();
                }
                UpdateMaterials();
                if (MainForm.IsValid)
                {
                    MainForm.Instance.RefreshTradeSkillTabs();
                    MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
                }
            }
            catch (Exception ex) { Err(ex.ToString()); }
        }
        #endregion

        #region OnSkillUpdate
        Stopwatch OnSkillUpdateSpamSW = new Stopwatch();
        public void OnSkillUpdate(object obj, LuaEventArgs args)
        {
            foreach (object o in args.Args)
                Debug("spell changed {0}", o);
            if (!OnSkillUpdateSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
                OnSkillUpdateSpamSW.Start();
            }
        }

        void OnSkillUpdateTimerCB(Object stateInfo)
        {
            Lua.Events.AttachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
            try
            {
                UpdateMaterials();
                // check if there was any tradeskills added or removed.
                WoWSkill[] skills = SupportedTradeSkills;
                bool changed = skills.
                    Count(s => TradeSkillList.Count(l => l.SkillLine == (SkillLine)s.Id) == 1) != TradeSkillList.Count ||
                    skills.Length != TradeSkillList.Count;
                if (changed)
                {
                    Debug("A profession was added or removed. Reloading Tradeskills (OnSkillUpdateTimerCB)");
                    LoadTradeSkills();
                    if (MainForm.IsValid)
                        MainForm.Instance.InitTradeSkillTab();
                }
                else
                {
                    Debug("Updated tradeskills from OnSkillUpdateTimerCB");
                    foreach (TradeSkill ts in TradeSkillList)
                    {
                        ts.PulseSkill();
                    }
                    if (MainForm.IsValid)
                    {
                        MainForm.Instance.RefreshTradeSkillTabs();
                        MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
                    }
                }
            }
            catch (Exception ex) { Err(ex.ToString()); }
        }

        #endregion

        #region OnSpellsChanged
        Stopwatch OnSpellsChangedSpamSW = new Stopwatch();
        public void OnSpellsChanged(object obj, LuaEventArgs args)
        {
            if (!OnSpellsChangedSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("SPELLS_CHANGED", OnSpellsChanged);
                OnSpellsChangedSpamSW.Start();
            }
        }
        public void OnSpellsChangedCB(Object stateInfo)
        {
            try
            {
                Lua.Events.AttachEvent("SPELLS_CHANGED", OnSpellsChanged);
                Debug("Pulsing Tradeskills from OnSpellsChanged");
                foreach (TradeSkill ts in TradeSkillList)
                {
                    TradeSkillFrame.Instance.UpdateTradeSkill(ts, true);
                }
                if (MainForm.IsValid)
                {
                    MainForm.Instance.InitTradeSkillTab();
                    MainForm.Instance.RefreshActionTree(typeof(CastSpellAction));
                }
            }
            catch (Exception ex)
            { Err(ex.ToString()); }
        }
        #endregion

        void Profile_OnUnknownProfileElement(object sender, UnknownProfileElementEventArgs e)
        {
            if (e.Element.Ancestors("Professionbuddy").Any())
            {
                e.Handled = true;
            }
        }

        static void Profile_OnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
        {
            if (args.NewProfile.XmlElement.Name == "Professionbuddy")
            {
                LoadProfile(ProfileManager.XmlLocation);
                if (MainForm.IsValid)
                {
                    if (Instance.ProfileSettings.Settings.Count > 0)
                        MainForm.Instance.AddProfileSettingsTab();
                    else
                        MainForm.Instance.RemoveProfileSettingsTab();
                }
                BotEvents.Profile.OnNewOuterProfileLoaded -= Profile_OnNewOuterProfileLoaded;
                if (!string.IsNullOrEmpty(HonorBuddyProfilePath) && File.Exists(HonorBuddyProfilePath))
                    ProfileManager.LoadNew(HonorBuddyProfilePath, true);
                else
                    ProfileManager.LoadEmpty();
                BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
            }
            else
                HonorBuddyProfilePath = ProfileManager.XmlLocation;
        }

        #endregion

        #region Behavior Tree

        static PbProfile _currentProfile = new PbProfile();
        public PbProfile CurrentProfile
        {
            get
            {
                return _currentProfile;
            }
            private set
            {
                _currentProfile = value;
            }
        }

        static PbRootComposite root; //= new PbRootComposite(new PbDecorator(new PrioritySelector()),null);
        override public TreeSharp.Composite Root
        {
            get
            {
                return root;
            }
        }

        public PbDecorator PbBehavior
        {
            get { return root.PbBotBase; }
            set { root.PbBotBase = value; }
        }


        public BotBase SecondaryBot
        {
            get { return root.SecondaryBot; }
            set { root.SecondaryBot = value; }
        }

        #endregion

        #region Misc

        bool _init = false;
        public void Init()
        {
            try
            {
                if (!_init)
                {
                    Lua.Events.AttachEvent("BAG_UPDATE", OnBagUpdate);
                    Lua.Events.AttachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
                    Lua.Events.AttachEvent("SPELLS_CHANGED", OnSpellsChanged);
                    Lua.Events.AttachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
                    Lua.Events.AttachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
                    Debug("Initializing ...");
                    if (!Directory.Exists(BotPath))
                        Directory.CreateDirectory(BotPath);
                    DynamicCode.WipeTempFolder();
                    // force Tripper.Tools.dll to load........
                    new Tripper.Tools.Math.Vector3(0, 0, 0);

                    MySettings = new ProfessionBuddySettings(
                        Path.Combine(Logging.ApplicationPath, string.Format(@"Settings\{0}\{0}[{1}-{2}].xml",
                        Name, Me.Name, Lua.GetReturnVal<string>("return GetRealmName()", 0)))
                    );
                    root = new PbRootComposite(new PbDecorator(new PrioritySelector()), null);
                    BotBase bot = BotManager.Instance.Bots.Values.FirstOrDefault(b => b.Name.IndexOf(MySettings.LastBotBase, StringComparison.InvariantCultureIgnoreCase) >= 0);
                    if (bot != null)
                        root.SecondaryBot = bot;

                    IsTradeSkillsLoaded = false;
                    MaterialList = new Dictionary<uint, int>();
                    TradeSkillList = new List<TradeSkill>();
                    Instance.ProfileSettings = new PbProfileSettings();
                    LoadProtectedItems();
                    LoadTradeSkills();
                    DataStore = new DataStore();
                    DataStore.ImportDataStore();

                    if (!string.IsNullOrEmpty(MySettings.LastProfile))
                    {
                        try
                        {
                            LoadProfile(MySettings.LastProfile);
                        }
                        catch (Exception ex) { Err(ex.ToString()); }
                    }
                    else
                        ProfileSettings = new PbProfileSettings();
                    HonorBuddyProfilePath = ProfileManager.XmlLocation;
                    _init = true;
                }
            }
            catch (Exception ex) { Logging.Write(System.Drawing.Color.Red, ex.ToString()); }
        }

        WoWSkill[] SupportedTradeSkills
        {
            get
            {
                IEnumerable<WoWSkill> skillList = from skill in TradeSkillFrame.SupportedSkills
                                                  where skill != SkillLine.Archaeology && Me.GetSkill(skill).MaxValue > 0
                                                  select Me.GetSkill(skill);
                return skillList.ToArray();
            }
        }

        public void LoadTradeSkills()
        {
            try
            {
                lock (TradeSkillList)
                {
                    TradeSkillList.Clear();
                    foreach (WoWSkill skill in SupportedTradeSkills)
                    {
                        Log("Adding TradeSkill {0}", skill.Name);
                        TradeSkill ts = TradeSkillFrame.Instance.GetTradeSkill((SkillLine)skill.Id, true);
                        if (ts != null)
                        {
                            TradeSkillList.Add(ts);
                        }
                        else
                        {
                            IsTradeSkillsLoaded = false;
                            Log("Unable to load tradeskill {0}", (SkillLine)skill.Id);
                            return;
                        }
                    }
                }
                IsTradeSkillsLoaded = true;
                if (OnTradeSkillsLoaded != null)
                {
                    OnTradeSkillsLoaded(this, null);
                }
            }
            catch (Exception ex) { Logging.Write(System.Drawing.Color.Red, ex.ToString()); }
        }

        public void UpdateMaterials()
        {
            try
            {
                lock (MaterialList)
                {
                    MaterialList.Clear();
                    List<CastSpellAction> castSpellList =
                        CastSpellAction.GetCastSpellActionList(CurrentProfile.Branch);
                    if (castSpellList != null)
                    {
                        foreach (CastSpellAction ca in castSpellList)
                        {
                            if (ca.IsRecipe)
                            {
                                foreach (var ingred in ca.Recipe.Ingredients)
                                {
                                    MaterialList[ingred.ID] = MaterialList.ContainsKey(ingred.ID) ?
                                        MaterialList[ingred.ID] + (ca.CalculatedRepeat > 0 ?
                                        (int)ingred.Required * (ca.CalculatedRepeat - ca.Casted) : 0) :

                                        (ca.CalculatedRepeat > 0 ? (int)ingred.Required * (ca.CalculatedRepeat - ca.Casted) : 0);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            { Err(ex.ToString()); }
        }

        public static bool LoadProfile(string path)
        {
            if (File.Exists(path))
            {
                Log("Loading profile {0}", Path.GetFileName(path));
                PbDecorator idComp = Instance.CurrentProfile.LoadFromFile(path);
                if (idComp != null)
                {
                    if (MainForm.IsValid)
                    {
                        MainForm.Instance.InitActionTree();
                        MainForm.Instance.RefreshTradeSkillTabs();
                    }
                    Instance.MySettings.LastProfile = path;
                    Instance.PbBehavior = idComp;
                    Instance.ProfileSettings.Load();
                    DynamicCode.GenorateDynamicCode();
                    Instance.UpdateMaterials();
                    PreLoadHbProfile();
                }
                else
                    return false;
            }
            else
            {
                Err("Profile: {0} does not exist", path);
                Instance.MySettings.LastProfile = path;
                return false;
            }
            if (MainForm.IsValid)
                MainForm.Instance.UpdateControls();
            Instance.MySettings.Save();
            return true;
        }

        public static void ChangeSecondaryBot(string botName)
        {
            BotBase bot = BotManager.Instance.Bots.Values.FirstOrDefault(b => b.Name.IndexOf(botName, StringComparison.InvariantCultureIgnoreCase) >= 0);

            if (bot != null)
            {
                if (Instance.SecondaryBot != null && Instance.SecondaryBot.Name != bot.Name || Instance.SecondaryBot == null)
                {
                    // execute from GUI thread since this thread will get aborted when switching bot
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                       new System.Action(() =>
                       {
                           bool isRunning = TreeRoot.IsRunning;
                           if (isRunning)
                               TreeRoot.Stop();
                           Instance.SecondaryBot = bot;
                           if (!bot.Initialized)
                               bot.Initialize();
                           if (ProfessionBuddySettings.Instance.LastBotBase != bot.Name)
                           {
                               ProfessionBuddySettings.Instance.LastBotBase = bot.Name;
                               ProfessionBuddySettings.Instance.Save();
                           }
                           if (MainForm.IsValid)
                               MainForm.Instance.UpdateBotCombo();
                           if (isRunning)
                               TreeRoot.Start();
                       }
                   ));
                    Professionbuddy.Log("Changing SecondaryBot to {0}", botName);
                }
            }
            else
                Err("Bot with name: {0} was not found", botName);
        }

        public static void PreLoadHbProfile()
        {
            if (!string.IsNullOrEmpty(Instance.CurrentProfile.ProfilePath) && Instance.CurrentProfile.Branch != null)
            {
                Dictionary<string, Uri> dict = new Dictionary<string, Uri>();
                PbProfile.GetHbprofiles(Instance.CurrentProfile.ProfilePath, Instance.CurrentProfile.Branch, dict);
                if (dict.Count > 0)
                {
                    foreach (var kv in dict)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && File.Exists(kv.Key))
                        {
                            Log("Preloading profile {0}", kv.Key);
                            // unhook event to prevent recursive loop
                            Styx.BotEvents.Profile.OnNewOuterProfileLoaded -= Profile_OnNewOuterProfileLoaded;
                            ProfileManager.LoadNew(kv.Key);
                            Styx.BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
                            return;
                        }
                    }
                }
            }
            if (ProfileManager.CurrentProfile == null)
            {
                Styx.BotEvents.Profile.OnNewOuterProfileLoaded -= Profile_OnNewOuterProfileLoaded;
                ProfileManager.LoadEmpty();
                Styx.BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
            }
        }

        static internal List<T> GetListOfActionsByType<T>(Composite comp, List<T> list) where T : Composite
        {
            if (list == null)
                list = new List<T>();
            if (comp.GetType() == typeof(T))
            {
                list.Add((T)comp);
            }
            if (comp is GroupComposite)
            {
                foreach (Composite c in ((GroupComposite)comp).Children)
                {
                    GetListOfActionsByType<T>(c, list);
                }
            }
            return list;
        }

        void LoadProtectedItems()
        {
            List<uint> tempList = null;
            string path = Path.Combine(BotPath, "Protected Items.xml");
            if (File.Exists(path))
            {
                XElement xml = XElement.Load(path);
                tempList = xml.Elements("Item").Select(x => x.Attribute("Entry").Value.ToUint()).Distinct().ToList();
            }
            ProtectedItems = tempList != null ? tempList : new List<uint>();
        }
        #endregion

        #region Utilies
        public static void Err(string format, params object[] args)
        {
            Logging.Write(System.Drawing.Color.Red, "Err: " + format, args);
        }
        static string _logHeader;
        static string Header
        {
            get
            {
                return _logHeader ?? (_logHeader = string.Format("PB {0}: ", Instance.Version));
            }
        }

        public static void Log(string format, params object[] args)
        {
            //Logging.Write(System.Drawing.Color.DodgerBlue, string.Format("PB {0}:", Instance.Version) + format, args);
            LogInvoker(System.Drawing.Color.DodgerBlue, Header, System.Drawing.Color.LightSteelBlue, format, args);
        }

        public static void Log(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args)
        {
            LogInvoker(headerColor, header, msgColor, format, args);
        }

        public static void Debug(string format, params object[] args)
        {
            Logging.WriteDebug(System.Drawing.Color.DodgerBlue, string.Format("PB {0}:", Instance.Version) + format, args);
        }

        static System.Windows.Controls.RichTextBox _rtbLog;
        delegate void LogDelegate(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args);

        static void LogInvoker(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args)
        {
            if (System.Windows.Application.Current.Dispatcher.Thread == Thread.CurrentThread)
                LogInternal(headerColor, header, msgColor, format, args);
            else
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new LogDelegate(LogInternal), headerColor, header, msgColor, format, args);
        }

        static void LogInternal(System.Drawing.Color headerColor, string header, System.Drawing.Color msgColor, string format, params object[] args)
        {
            try
            {
                if (_rtbLog == null)
                    _rtbLog = (System.Windows.Controls.RichTextBox)System.Windows.Application.Current.MainWindow.FindName("rtbLog");
                System.Windows.Media.Color headerColorMedia = System.Windows.Media.Color.FromArgb(headerColor.A, headerColor.R, headerColor.G, headerColor.B);
                System.Windows.Media.Color msgColorMedia = System.Windows.Media.Color.FromArgb(msgColor.A, msgColor.R, msgColor.G, msgColor.B);

                TextRange headerTR = new TextRange(_rtbLog.Document.ContentEnd, _rtbLog.Document.ContentEnd);
                headerTR.Text = header;
                headerTR.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(headerColorMedia));

                TextRange MessageTR = new TextRange(_rtbLog.Document.ContentEnd, _rtbLog.Document.ContentEnd);
                MessageTR.Text = String.Format(format + Environment.NewLine, args);
                MessageTR.ApplyPropertyValue(TextElement.ForegroundProperty, new System.Windows.Media.SolidColorBrush(msgColorMedia));
            }
            catch
            {
                Logging.Write("PB: " + format, args);
            }
        }

        #endregion

    }
}
