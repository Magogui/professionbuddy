using System;
using System.Linq;
using System.Threading;
using Styx.WoWInternals;
using Styx.Logic.BehaviorTree;
using Styx;
using Styx.Logic;
using Styx.Logic.Pathing;
using Styx.Helpers;

namespace HighVoltz.Dynamic
{
    public class Helpers
    {
        static Helpers()
        {
            Alchemy = new TradeskillHelper(SkillLine.Alchemy);
            Archaeology = new TradeskillHelper(SkillLine.Archaeology);
            Blacksmithing = new TradeskillHelper(SkillLine.Blacksmithing);
            Cooking = new TradeskillHelper(SkillLine.Cooking);
            Enchanting = new TradeskillHelper(SkillLine.Enchanting);
            Engineering = new TradeskillHelper(SkillLine.Engineering);
            FirstAid = new TradeskillHelper(SkillLine.FirstAid);
            Fishing = new TradeskillHelper(SkillLine.Fishing);
            Herbalism = new TradeskillHelper(SkillLine.Herbalism);
            Inscription = new TradeskillHelper(SkillLine.Inscription);
            Jewelcrafting = new TradeskillHelper(SkillLine.Jewelcrafting);
            Leatherworking = new TradeskillHelper(SkillLine.Leatherworking);
            Mining = new TradeskillHelper(SkillLine.Mining);
            Tailoring = new TradeskillHelper(SkillLine.Tailoring);
        }
        public static void Log(string f, params object[] args)
        {
            Logging.Write(f, args);
        }
        public static void Log(System.Drawing.Color c, string f, params object[] args)
        {
            Logging.Write(c, f, args);  
        }
        public static int InbagCount(uint id)
        {
            return (int) Ingredient.GetInBagItemCount(id);
        }
        public static float DistanceTo(double x, double y, double z)
        {
            return ObjectManager.Me.Location.Distance(new WoWPoint(x, y, z));
        }
        public static void MoveTo(double x, double y, double z)
        {
            Util.MoveTo(new WoWPoint(x, y, z));
        }
        public static void CTM(double x, double y, double z)
        {
            WoWMovement.ClickToMove(new WoWPoint(x, y, z));
        }

        static bool _isSwitchingToons;
        // credit to mvbc of mmocore.com
        // {0}=character,{1}=server
        const string LoginLua =
        "if (RealmList and RealmList:IsVisible()) then " +
            "for i = 1, select('#',GetRealmCategories()) do " +
                "for j = 1, GetNumRealms(i) do " +
                    "if GetRealmInfo(i, j) == \"{1}\" then " +
                        "RealmList:Hide() " +
                        "ChangeRealm(i, j) " +
                    "end " +
                "end " +
            "end " +
        "elseif (CharacterSelectUI and CharacterSelectUI:IsVisible()) then " +
            "if GetServerName() ~= \"{1}\" and (not RealmList or not RealmList:IsVisible()) then " +
                "RequestRealmList(1) " +
            "else " +
                "for i = 1,GetNumCharacters() do " +
                    "if (GetCharacterInfo(i) == \"{0}\") then " +
                        "CharacterSelect_SelectCharacter(i) " +
                        "EnterWorld() " +
                    "end " +
                "end " +
            "end " +
        "elseif (CharCreateRandomizeButton and CharCreateRandomizeButton:IsVisible()) then " +
            "CharacterCreate_Back() " +
        "end ";

        /// <summary>
        /// Switches to a different character on same account
        /// </summary>
        /// <param name="character"></param>
        /// <param name="server"></param>
        /// <param name="botName">Name of bot to use on that character</param>

        public static void SwitchCharacter(string character, string server, string botName)
        {
            if (_isSwitchingToons)
            {
                Professionbuddy.Log("Already switching characters");
                return;
            }
            string loginLua = string.Format(LoginLua, character, server);
            _isSwitchingToons = true;
            // reset all actions 
            Professionbuddy.Instance.IsRunning = false;
            Professionbuddy.Instance.PbBehavior.Reset();

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
            new Action(() =>
            {
                TreeRoot.Stop();
                BotBase bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Key.IndexOf(botName, StringComparison.OrdinalIgnoreCase) >= 0).Value;
                if (bot != null)
                {
                    if (Professionbuddy.Instance.SecondaryBot.Name != bot.Name)
                        Professionbuddy.Instance.SecondaryBot = bot;
                    if (!bot.Initialized)
                        bot.Initialize();
                    if (ProfessionBuddySettings.Instance.LastBotBase != bot.Name)
                    {
                        ProfessionBuddySettings.Instance.LastBotBase = bot.Name;
                        ProfessionBuddySettings.Instance.Save();
                    }
                }
                else
                    Professionbuddy.Err("Could not find bot with name {0}", botName);

                Lua.DoString("Logout()");

                new Thread(() =>
                {
                    while (ObjectManager.IsInGame)
                        Thread.Sleep(2000);
                    while (!ObjectManager.IsInGame)
                    {
                        Lua.DoString(loginLua);
                        Thread.Sleep(2000);
                    }
                    TreeRoot.Start();
                    Professionbuddy.Instance.LoadTradeSkills();
                    if (MainForm.IsValid)
                    {
                        MainForm.Instance.InitTradeSkillTab();
                        MainForm.Instance.InitTradeSkillTab();
                    }
                    _isSwitchingToons = false;
                    Professionbuddy.Instance.IsRunning = true;
                }) { IsBackground = true }.Start();
            }));
        }

        public class TradeskillHelper
        {
            readonly SkillLine _skillLine;
            WoWSkill _wowSkill;
            public TradeskillHelper(SkillLine skillLine)
            {
                _skillLine = skillLine;
                _wowSkill = ObjectManager.Me.GetSkill(skillLine);
            }
            public uint Level
            {
                get
                {
                    _wowSkill = ObjectManager.Me.GetSkill(_skillLine);
                    return (uint)_wowSkill.CurrentValue + _wowSkill.Bonus;
                }
            }
            public uint MaxLevel
            {
                get
                {
                    _wowSkill = ObjectManager.Me.GetSkill(_skillLine);
                    return (uint)_wowSkill.MaxValue;
                }
            }
            static public uint CanRepeatNum(uint recipeID)
            {
                return (from ts in Professionbuddy.Instance.TradeSkillList where ts.Recipes.ContainsKey(recipeID) select ts.Recipes[recipeID].CanRepeatNum).FirstOrDefault();
            }

            static public bool CanCraft(uint recipeID)
            {
                return (from ts in Professionbuddy.Instance.TradeSkillList
                        where ts.Recipes.ContainsKey(recipeID)
                        select (ts.Recipes[recipeID].Tools.Count(t => t.HasTool) == ts.Recipes[recipeID].Tools.Count) && ts.Recipes[recipeID].CanRepeatNum > 0).FirstOrDefault();
            }

            static public bool HasMats(uint recipeID)
            {
                return CanRepeatNum(recipeID) > 0;
            }
            static public bool HasRecipe(uint recipeID)
            {
                return Professionbuddy.Instance.TradeSkillList.Any(ts => ts.Recipes.ContainsKey(recipeID));
            }

            static public bool HasTools(uint recipeID)
            {
                return (from ts in Professionbuddy.Instance.TradeSkillList where ts.Recipes.ContainsKey(recipeID) select ts.Recipes[recipeID].Tools.Count(t => t.HasTool) == ts.Recipes[recipeID].Tools.Count).FirstOrDefault();
            }
        }
        public static TradeskillHelper Alchemy { get; private set; }
        public static TradeskillHelper Archaeology { get; private set; }
        public static TradeskillHelper Blacksmithing { get; private set; }
        public static TradeskillHelper Cooking { get; private set; }
        public static TradeskillHelper Enchanting { get; private set; }
        public static TradeskillHelper Engineering { get; private set; }
        public static TradeskillHelper FirstAid { get; private set; }
        public static TradeskillHelper Fishing { get; private set; }
        public static TradeskillHelper Herbalism { get; private set; }
        public static TradeskillHelper Inscription { get; private set; }
        public static TradeskillHelper Jewelcrafting { get; private set; }
        public static TradeskillHelper Leatherworking { get; private set; }
        public static TradeskillHelper Mining { get; private set; }
        public static TradeskillHelper Tailoring { get; private set; }
    }
}
