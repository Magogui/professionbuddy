﻿//!CompilerOption:Optimize:On
//!CompilerOption:AddRef:WindowsBase.dll
// Professionbuddy plugin by HighVoltz

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

namespace HighVoltz
{
    public partial class Professionbuddy : HBPlugin
    {
        #region Declarations
        public ProfessionBuddySettings MySettings;
        public List<TradeSkill> TradeSkillList { get; private set; }
        // <itemId,count>
        public Dictionary<uint, int> DataStore { get; private set; }
        // dictionary that keeps track of material list using item ID for key and number required as value
        public Dictionary<uint, int> MaterialList { get; private set; }
        public List<uint> ProtectedItems { get; private set; }
        public bool IsTradeSkillsLoaded { get; private set; }
        public string PluginPath { get { return Logging.ApplicationPath + @"\Plugins\" + Name; } }
        public string TempFolder { get { return Path.Combine(PluginPath, "Temp\\"); } }
        public event EventHandler OnTradeSkillsLoaded;
        public readonly LocalPlayer Me = ObjectManager.Me;
        static public uint Ping { get { return StyxWoW.WoWClient.Latency; } }
        // DataStore is an addon for WOW thats stores bag/ah/mail item info and more.
        public bool HasDataStoreAddon { get; private set; }
        // profile Settings.
        public PbProfileSettings ProfileSettings { get; private set; }
        public bool IsRunning = false;
        // singleton instance
        public static Professionbuddy Instance { get; private set; }
        public Professionbuddy() { Instance = this; }
        #endregion

        #region Overrides
        public override string Name {
            get { return "ProfessionBuddy"; }
        }

        public override string Author { get { return "HighVoltz"; } }

        public override Version Version { get { return new Version(1, 37); } }

        public override bool WantButton { get { return true; } }

        public override string ButtonText { get { return Name; } }

        public override void OnButtonPress() {
            try
            {
                if (IsEnabled)
                {
                    if (!MainForm.IsValid)
                        new MainForm().Show();
                    else
                        MainForm.Instance.Activate();
                }
                else
                    MessageBox.Show("You must enable " + Name + " before you can use it");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }
        public bool IsEnabled {
            get { return PluginManager.Plugins.Any(p => p.Name == Name && p.Author == Author && p.Enabled); }
        }

        public override void Dispose() {
            BotEvents.OnBotChanged -= BotEvents_OnBotChanged;
            IsRunning = false;
            //BotBaseCleanUp(null);
            Lua.Events.DetachEvent("BAG_UPDATE", OnBagUpdate);
            Lua.Events.DetachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
            Lua.Events.DetachEvent("SPELLS_CHANGED", OnSpellsChanged);
            Lua.Events.DetachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
            Lua.Events.DetachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
            if (MainForm.IsValid)
                MainForm.Instance.Close();
        }

        public override void Pulse() {
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

        public override void Initialize() {
            Init();
        }
        #endregion

        #region Callbacks

        #region OnBagUpdate
        Stopwatch OnBagUpdateSpamSW = new Stopwatch();
        public void OnBagUpdate(object obj, LuaEventArgs args) {
            if (!OnBagUpdateSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("BAG_UPDATE", OnBagUpdate);
                OnBagUpdateSpamSW.Start();
            }
        }

        void OnBagUpdateTimerCB(Object stateInfo) {
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
                    MainForm.Instance.RefreshActionTree();
                }
            }
            catch (Exception ex) { Err(ex.ToString()); }
        }
        #endregion

        #region OnSkillUpdate
        Stopwatch OnSkillUpdateSpamSW = new Stopwatch();
        public void OnSkillUpdate(object obj, LuaEventArgs args) {
            foreach (object o in args.Args)
                Debug("spell changed {0}", o);
            if (!OnSkillUpdateSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
                OnSkillUpdateSpamSW.Start();
            }
        }

        void OnSkillUpdateTimerCB(Object stateInfo) {
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
                    loadTradeSkills();
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
                        MainForm.Instance.RefreshActionTree();
                    }
                }
            }
            catch (Exception ex) { Err(ex.ToString()); }
        }

        #endregion

        #region OnSpellsChanged
        Stopwatch OnSpellsChangedSpamSW = new Stopwatch();
        public void OnSpellsChanged(object obj, LuaEventArgs args) {
            if (!OnSpellsChangedSpamSW.IsRunning)
            {
                Lua.Events.DetachEvent("SPELLS_CHANGED", OnSpellsChanged);
                OnSpellsChangedSpamSW.Start();
            }
        }
        public void OnSpellsChangedCB(Object stateInfo) {
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
                    MainForm.Instance.RefreshActionTree();
                }
            }
            catch (Exception ex)
            { Err(ex.ToString()); }
        }
        #endregion

        #region OnBotChanged

        void BotEvents_OnBotChanged(BotEvents.BotChangedEventArgs args) {
            if (TreeRoot.Current.Root is PrioritySelector)
            {
                PrioritySelector botbase = TreeRoot.Current.Root as PrioritySelector;
                BotBaseCleanUp(botbase);
                botbase.InsertChild(0, Root);
            }
        }
        #endregion

        #endregion

        #region Behavior Tree
        PBIdentityComposite root;
        public TreeSharp.Composite Root {
            get {
                return root ?? (root = new PBIdentityComposite(CurrentProfile.Branch));
            }
            set {
                root = (PBIdentityComposite)value;
            }
        }
        PbProfile _currentProfile;
        public PbProfile CurrentProfile {
            get {
                return _currentProfile ?? (_currentProfile = new PbProfile());
            }
            private set {
                _currentProfile = value;
            }
        }
        #endregion

        #region Misc
        // remove any occurance of IdentityComposite in the current BotBase, used on dispose or botbase change
        void BotBaseCleanUp(PrioritySelector bot) {
            PrioritySelector botbase = null;
            if (bot != null)
                botbase = bot;
            else if (TreeRoot.Current.Root is PrioritySelector)
                botbase = TreeRoot.Current.Root as PrioritySelector;
            // check if we already injected into the BotBase
            if (botbase != null)
            {
                bool isRunning = botbase.IsRunning;
                if (isRunning)
                    TreeRoot.Stop();
                for (int i = botbase.Children.Count - 1; i >= 0; i--)
                {
                    //if (botbase.Children[i] is IdentityComposite ) // this will not work after a recompile because the types are now in different assemblies
                    if (botbase.Children[i].GetType().Name.Contains("PBIdentityComposite"))
                    {
                        botbase.Children.RemoveAt(i);
                    }
                }
            }
        }

        bool _init = false;
        public void Init() {
            try
            {
                if (!_init)
                {
                    Debug("Initializing ...");
                    if (!Directory.Exists(PluginPath))
                        Directory.CreateDirectory(PluginPath);
                    WipeTempFolder();
                    // force Tripper.Tools.dll to load........
                    new Tripper.Tools.Math.Vector3(0, 0, 0);
                    MySettings = new ProfessionBuddySettings
                        (Path.Combine(Logging.ApplicationPath, string.Format(@"Settings\{0}\{0}-{1}.xml", Name, Me.Name)));
                    IsTradeSkillsLoaded = false;
                    HasDataStoreAddon = false;
                    IsRunning = MySettings.IsRunning;
                    MaterialList = new Dictionary<uint, int>();
                    TradeSkillList = new List<TradeSkill>();
                    LoadProtectedItems();
                    loadTradeSkills();
                    BotEvents.OnBotChanged += BotEvents_OnBotChanged;
                    Lua.Events.AttachEvent("BAG_UPDATE", OnBagUpdate);
                    Lua.Events.AttachEvent("SKILL_LINES_CHANGED", OnSkillUpdate);
                    Lua.Events.AttachEvent("SPELLS_CHANGED", OnSpellsChanged);
                    Lua.Events.AttachEvent("BANKFRAME_OPENED", Util.OnBankFrameOpened);
                    Lua.Events.AttachEvent("BANKFRAME_CLOSED", Util.OnBankFrameClosed);
                    ImportDataStore();
                    BotEvents_OnBotChanged(null);
                    if (!string.IsNullOrEmpty(MySettings.LastProfile))
                        LoadProfile(MySettings.LastProfile);
                    else
                        ProfileSettings = new PbProfileSettings();

                    _init = true;
                }
            }
            catch (Exception ex) { Logging.Write(System.Drawing.Color.Red, ex.ToString()); }
        }

        WoWSkill[] SupportedTradeSkills {
            get {
                IEnumerable<WoWSkill> skillList = from skill in TradeSkillFrame.SupportedSkills
                                                  where skill != SkillLine.Archaeology && Me.GetSkill(skill).MaxValue > 0
                                                  select Me.GetSkill(skill);
                return skillList.ToArray();
            }
        }
        private void loadTradeSkills() {
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

        public void UpdateMaterials() {
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

        public static bool LoadProfile(string path) {
            if (File.Exists(path))
            {
                Log("Loading profile {0}", path);
                PBIdentityComposite idComp = Instance.CurrentProfile.LoadFromFile(path);
                if (idComp != null)
                {
                    if (MainForm.IsValid)
                    {
                        MainForm.Instance.InitActionTree();
                        MainForm.Instance.RefreshTradeSkillTabs();
                    }
                    Instance.MySettings.LastProfile = path;
                    Instance.Root = idComp;
                    Instance.ProfileSettings = new PbProfileSettings();
                    Instance.ProfileSettings.Load();
                    Instance.GenorateDynamicCode();
                    Instance.UpdateMaterials();
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

        void LoadProtectedItems() {
            List<uint> tempList = null;
            string path = Path.Combine(PluginPath, "Protected Items.xml");
            if (File.Exists(path))
            {
                XElement xml = XElement.Load(path);
                tempList = xml.Elements("Item").Select(x => x.Attribute("Entry").Value.ToUint()).Distinct().ToList();
            }
            ProtectedItems = tempList != null ? tempList : new List<uint>();
        }
        #endregion

        #region Utilies
        public static void Err(string format, params object[] args) {
            Logging.Write(System.Drawing.Color.Red, "Err: " + format, args);
        }

        public static void Log(string format, params object[] args) {
            Logging.Write(System.Drawing.Color.SaddleBrown, "ProfessionBuddy: " + format, args);
        }

        public static void Debug(string format, params object[] args) {
            Logging.WriteDebug(System.Drawing.Color.SaddleBrown, "ProfessionBuddy: " + format, args);
        }

        public void ImportDataStore() {
            Log("Importing from DataStore...");
            int tableSize, tableIndex = 1;
            DataStore = new Dictionary<uint, int>();
            string tableName = Util.RandomString;
            string storeInTableLua =
            "if DataStoreDB and DataStore_ContainersDB  and DataStore_AuctionsDB and DataStore_MailsDB then " +
               "local realm = GetRealmName() " +
               "local faction = UnitFactionGroup('player') " +
               "local profiles = {} " +
               "local items = {} " +
               "local guilds = {} " +
               "local storeItem = function (id,cnt) id=tonumber(id) cnt=tonumber(cnt) if items[id]  then items[id] = items[id] + cnt else items[id] = cnt end end " +
               "for k,v in pairs(DataStoreDB.global.Characters) do " +
                  @"local r = string.match(k,'%a+\.(%a+ ?%a+ ?%a+)') " +
                  "if r and r == realm and v and v.faction == faction then " +
                     "table.insert (profiles,k) " +
                     "if v.guildName then " +
                        "guilds[string.format('%s.%s',realm,v.guildName)] = 1 " +
                     "end " +
                  "end " +
               "end " +
               "for k,v in ipairs(profiles) do " +
                  "local char=DataStore_ContainersDB.global.Characters[v] " +
                  "if char then " +
                     "for i=-2,100 do " +
                        "local x = char.Containers['Bag'..i] " +
                        "if x then " +
                           "for i=1, x.size do " +
                              "if x.ids[i] then " +
                                 "storeItem (x.ids[i],x.counts[i] or 1) " +
                              "end " +
                           "end " +
                        "end " +
                     "end " +
                  "end " +
                  "char=DataStore_AuctionsDB.global.Characters[v] " +
                  "if char and char.Auctions then " +
                     "for k,v in ipairs(char.Auctions) do " +
                        "storeItem(string.match(v,'%d+|(%d+)'),string.match(v,'%d+|%d+|(%d+)')) " +
                     "end " +
                  "end " +
                  "char=DataStore_MailsDB.global.Characters[v] " +
                  "if char then " +
                     "for k,v in pairs(char.Mails) do " +
                        "if v.link and v.count then " +
                           "storeItem(string.match(v.link,'|Hitem:(%d+)'),v.count) " +
                        "end " +
                     "end " +
                  "end " +
               "end " +
               "for k,v in pairs(DataStore_ContainersDB.global.Guilds) do " +
                  "for g,_ in pairs(guilds) do " +
                     "if string.find(k,g) and v.Tabs then " +
                        "for k2,v2 in ipairs(v.Tabs) do " +
                           "if v2 and v2.ids then " +
                              "for k3,v3 in pairs(v2.ids) do " +
                                 "storeItem (v3,v2.counts[k3] or 1) " +
                              "end " +
                           "end " +
                        "end " +
                     "end " +
                  "end " +
               "end " +
               tableName + " = {} " +
               "for k,v in pairs(items) do " +
                  "table.insert(" + tableName + ",k) " +
                  "table.insert(" + tableName + ",v) " +
               "end " +
               "return #" + tableName + " " +
            "end " +
            "return 0 ";

            using (new FrameLock())
            {
                List<string> retVals = Lua.GetReturnValues(storeInTableLua);
                if (retVals != null && retVals[0] != "0")
                {
                    HasDataStoreAddon = true;
                    int.TryParse(retVals[0], out tableSize);
                    while (true)
                    {
                        string getTableDataLua =
                            "local retVals = {" + tableIndex + "} " +
                            "for i=retVals[1], #" + tableName + " do " +
                              "table.insert(retVals," + tableName + "[i]) " +
                              "if #retVals >= 501 then " +
                                "retVals[1] = i +1 " +
                                "return unpack(retVals) " +
                              "end " +
                            "end " +
                            "retVals[1] = #" + tableName + " " +
                            "return unpack(retVals) ";
                        retVals = Lua.GetReturnValues(getTableDataLua);
                        int.TryParse(retVals[0], out tableIndex);
                        for (int i = 2; i < retVals.Count; i += 2)
                        {
                            uint id, num;
                            uint.TryParse(retVals[i - 1], out id);
                            uint.TryParse(retVals[i], out num);
                            DataStore[id] = (int)num;
                        }
                        if (retVals == null || tableIndex >= tableSize)
                            break;
                    }
                    Lua.DoString(tableName + "={}");
                    Log("Imported DataStore");
                }
                else
                    Log("No DataStore addon found");
            }
        }

        #endregion

        #region PBIdentityComposite
        public class PBIdentityComposite : Decorator, IXmlSerializable
        {
            public PBIdentityComposite(PrioritySelector ps)
                : base(c => StyxWoW.IsInWorld && !ExitBehavior() && Professionbuddy.Instance.IsRunning, ps) { }

            static LocalPlayer Me = ObjectManager.Me;
            public override RunStatus Tick(object context) {
                if (CanRun(null))
                    return base.Tick(context);
                else
                {
                    LastStatus = RunStatus.Failure;
                    return RunStatus.Failure;
                }
            }
            // returns true if in combat or dead or low hp %
            public static bool ExitBehavior() {
                return !Professionbuddy.Instance.IsEnabled || Me.IsActuallyInCombat ||
                    !Me.IsAlive || Me.HealthPercent <= 40;
            }

            public void ReadXml(XmlReader reader) {
                int count;
                reader.MoveToContent();
                int.TryParse(reader["ChildrenCount"], out count);
                reader.ReadStartElement("Professionbuddy");
                PrioritySelector ps = (PrioritySelector)DecoratedChild;
                for (int i = 0; i < count; i++)
                {
                    Type type = Type.GetType("HighVoltz.Composites." + reader.Name);
                    if (type != null)
                    {
                        IPBComposite comp = (IPBComposite)Activator.CreateInstance(type);
                        if (comp != null)
                        {
                            comp.ReadXml(reader);
                            ps.AddChild((Composite)comp);
                        }
                    }
                    else
                    {
                        Err("PB:Failed to load type {0}", type);
                    }
                }
                if (reader.NodeType == XmlNodeType.EndElement)
                    reader.ReadEndElement();
            }

            public void WriteXml(XmlWriter writer) {
                writer.WriteStartElement("Professionbuddy");
                PrioritySelector ps = (PrioritySelector)DecoratedChild;
                writer.WriteStartAttribute("ChildrenCount");
                writer.WriteValue(ps.Children.Count);
                writer.WriteEndAttribute();
                foreach (IPBComposite comp in ps.Children)
                {
                    writer.WriteStartElement(comp.GetType().Name);
                    ((IXmlSerializable)comp).WriteXml(writer);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            public System.Xml.Schema.XmlSchema GetSchema() { return null; }
        }
        #endregion
    }
}
