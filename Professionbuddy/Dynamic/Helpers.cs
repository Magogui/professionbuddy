﻿using System;
using System.Collections.Generic;
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
            return (int)Ingredient.GetInBagItemCount(id);
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

        public static bool IsSwitchingToons { get { return _isSwitchingToons; } }
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

            System.Windows.Application.Current.Dispatcher.Invoke(
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
        // Format index: {0}=GuildName, {1}=ItemId
        // returns item count
        private const string InGbankCountFormat =
            "local itemCount = 0 " +
            "for k,v in pairs(DataStore_ContainersDB.global.Guilds) do " +
               "if string.find(k,\"{0}\") and v.Tabs then " +
                  "for k2,v2 in ipairs(v.Tabs) do " +
                     "if v2 and v2.ids then " +
                        "for k3,v3 in pairs(v2.ids) do " +
                           "if v3 == {1} then " +
                              "itemCount = itemCount + (v2.counts[k3] or 1) " +
                           "end " +
                        "end " +
                     "end " +
                  "end " +
               "end " +
            "end " +
            "return itemCount";

        public static int InGBankCount(uint itemId)
        {
            return InGBankCount(null, itemId);
        }

        public static int InGBankCount(string character, uint itemId)
        {
            int ret = 0;
            using (new FrameLock())
            {
                bool hasDataStore = Lua.GetReturnVal<bool>("if DataStoreDB and DataStore_ContainersDB then return 1 else return 0 end", 0);
                if (hasDataStore)
                {
                    if (string.IsNullOrEmpty(character))
                        character = Lua.GetReturnVal<string>("return UnitName('player')", 0);
                    string server = Lua.GetReturnVal<string>("return GetRealmName()", 0);
                    string guildName = null, lua;
                    List<string> profiles = Lua.GetReturnValues("local t={} for k,v in pairs(DataStoreDB.global.Characters) do table.insert(t,k) end return unpack(t)");
                    foreach (string profile in profiles)
                    {
                        string[] elements = profile.Split('.');
                        if (elements[1].Equals(server, StringComparison.InvariantCultureIgnoreCase) &&
                            elements[2].Equals(character, StringComparison.InvariantCultureIgnoreCase))
                        {
                            lua = string.Format("local val = DataStoreDB.global.Characters[\"{0}\"] if val and val.guildName then return val.guildName end return '' ", profile);
                            guildName = Lua.GetReturnVal<string>(lua, 0);
                            if (string.IsNullOrEmpty(guildName))
                                return 0;
                            break;
                        }
                    }
                    lua = string.Format(InGbankCountFormat, guildName, itemId);
                    ret = Lua.GetReturnVal<int>(lua, 0);
                }
            }
            return ret;
        }

        public static int OnAhCount(uint itemId)
        {
            return OnAhCount(null, itemId);
        }

        public static int OnAhCount(string character, uint itemId)
        {
            int ret = 0;
            using (new FrameLock())
            {
                var hasDataStore = Lua.GetReturnVal<bool>("if DataStoreDB and DataStore_AuctionsDB then return 1 else return 0 end", 0);
                if (hasDataStore)
                {
                    if (string.IsNullOrEmpty(character))
                        character = Lua.GetReturnVal<string>("return UnitName('player')", 0);
                    var server = Lua.GetReturnVal<string>("return GetRealmName()", 0);
                    var profiles = Lua.GetReturnValues("local t={} for k,v in pairs(DataStoreDB.global.Characters) do table.insert(t,k) end return unpack(t)");
                    string profile = (from p in profiles
                                      let elements = p.Split('.')
                                      where elements[1].Equals(server, StringComparison.InvariantCultureIgnoreCase) &&
                                      elements[2].Equals(character, StringComparison.InvariantCultureIgnoreCase)
                                      select p).FirstOrDefault();
                    if (string.IsNullOrEmpty(profile))
                        return 0;
                    string lua = string.Format(
                        "local char=DataStore_AuctionsDB.global.Characters[\"{0}\"] if char and char.Auctions then return #char.Auctions end return 0 ",
                        profile);
                    var tableSize = Lua.GetReturnVal<int>(lua, 0);
                    for (int i = 1; i <= tableSize; i++)
                    {
                        lua = string.Format("local char=DataStore_AuctionsDB.global.Characters[\"{0}\"] if char then return char.Auctions[{1}] end return '' ", profile, i);
                        string aucStr = Lua.GetReturnVal<string>(lua, 0);
                        string[] strs = aucStr.Split('|');
                        int id = int.Parse(strs[1]);
                        if (id == itemId)
                        {
                            int cnt = int.Parse(strs[2]);
                            ret += cnt;
                        }
                    }
                }
            }
            return ret;
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
