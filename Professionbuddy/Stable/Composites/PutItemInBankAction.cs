using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using System.Xml;
using Styx;
using Styx.Helpers;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Reflection;
using ObjectManager = Styx.WoWInternals.ObjectManager;


namespace HighVoltz.Composites
{
    #region PutItemInBankAction
    public class PutItemInBankAction : PBAction
    {
        public bool UseCategory
        {
            get { return (bool)Properties["UseCategory"].Value; }
            set { Properties["UseCategory"].Value = value; }
        }
        public WoWItemClass Category
        {
            get { return (WoWItemClass)Properties["Category"].Value; }
            set { Properties["Category"].Value = value; }
        }
        public object SubCategory
        {
            get { return (object)Properties["SubCategory"].Value; }
            set { Properties["SubCategory"].Value = value; }
        }

        public BankType Bank
        {
            get { return (BankType)Properties["Bank"].Value; }
            set { Properties["Bank"].Value = value; }
        }

        public uint Entry
        {
            get { return (uint)Properties["Entry"].Value; }
            set { Properties["Entry"].Value = value; }
        }
        public uint GuildTab
        {
            get { return (uint)Properties["GuildTab"].Value; }
            set { Properties["GuildTab"].Value = value; }
        }
        public uint NpcEntry
        {
            get { return (uint)Properties["NpcEntry"].Value; }
            set { Properties["NpcEntry"].Value = value; }
        }
        public int Amount
        {
            get { return (int)Properties["Amount"].Value; }
            set { Properties["Amount"].Value = value; }
        }
        public bool AutoFindBank
        {
            get { return (bool)Properties["AutoFindBank"].Value; }
            set { Properties["AutoFindBank"].Value = value; }
        }
        WoWPoint loc;
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public PutItemInBankAction()
        {
            Properties["Amount"] = new MetaProp("Amount", typeof(int));
            Properties["Entry"] = new MetaProp("Entry", typeof(uint));
            Properties["Bank"] = new MetaProp("Bank", typeof(BankType));
            Properties["AutoFindBank"] = new MetaProp("AutoFindBank", typeof(bool), new DisplayNameAttribute("Auto find Bank"));
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));
            Properties["NpcEntry"] = new MetaProp("NpcEntry", typeof(uint), new EditorAttribute(typeof(PropertyBag.EntryEditor), typeof(UITypeEditor)));
            Properties["GuildTab"] = new MetaProp("GuildTab", typeof(uint));
            Properties["UseCategory"] = new MetaProp("UseCategory", typeof(bool), new DisplayNameAttribute("Use Category"));
            Properties["Category"] = new MetaProp("Category", typeof(WoWItemClass), new DisplayNameAttribute("Item Category"));
            Properties["SubCategory"] = new MetaProp("SubCategory", typeof(WoWItemTradeGoodsClass), new DisplayNameAttribute("Item SubCategory"));

            Amount = 0;
            Entry = 0u;
            Bank = BankType.Personal;
            AutoFindBank = true;
            loc = WoWPoint.Zero;
            Location = loc.ToInvariantString();
            NpcEntry = 0u;
            GuildTab = 0u;
            UseCategory = true;
            Category = WoWItemClass.TradeGoods;
            SubCategory = WoWItemTradeGoodsClass.None;

            Properties["Entry"].Show = false;
            Properties["Location"].Show = false;
            Properties["NpcEntry"].Show = false;
            Properties["GuildTab"].Show = false;

            Properties["AutoFindBank"].PropertyChanged += new EventHandler(AutoFindBankChanged);
            Properties["Bank"].PropertyChanged += new EventHandler(PutItemInBankAction_PropertyChanged);
            Properties["Location"].PropertyChanged += new EventHandler(LocationChanged);
            Properties["UseCategory"].PropertyChanged += UseCategoryChanged;
            Properties["Category"].PropertyChanged += CategoryChanged;
        }

        #region Callbacks
        void LocationChanged(object sender, EventArgs e)
        {
            MetaProp mp = (MetaProp)sender;
            loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= new EventHandler(LocationChanged);
            Properties["Location"].Value = string.Format("<{0}, {1}, {2}>", loc.X, loc.Y, loc.Z);
            Properties["Location"].PropertyChanged += new EventHandler(LocationChanged);
            RefreshPropertyGrid();
        }

        void PutItemInBankAction_PropertyChanged(object sender, EventArgs e)
        {
            if (Bank == BankType.Personal)
            {
                Properties["GuildTab"].Show = false;
            }
            else
            {
                Properties["GuildTab"].Show = true;
            }
            RefreshPropertyGrid();
        }

        void AutoFindBankChanged(object sender, EventArgs e)
        {
            if (AutoFindBank)
            {
                Properties["Location"].Show = false;
                Properties["NpcEntry"].Show = false;
            }
            else
            {
                Properties["Location"].Show = true;
                Properties["NpcEntry"].Show = true;
            }
            RefreshPropertyGrid();
        }
        void UseCategoryChanged(object sender, EventArgs e)
        {
            if (UseCategory)
            {
                Properties["Entry"].Show = false;
                Properties["Category"].Show = true;
                Properties["SubCategory"].Show = true;
            }
            else
            {
                Properties["Entry"].Show = true;
                Properties["Category"].Show = false;
                Properties["SubCategory"].Show = false;
            }
            RefreshPropertyGrid();
        }

        void CategoryChanged(object sender, EventArgs e)
        {
            object subCategory = Callbacks.GetSubCategory(Category);
            Properties["SubCategory"] = new MetaProp("SubCategory", subCategory.GetType(),
                new DisplayNameAttribute("Item SubCategory"));
            SubCategory = subCategory;
            RefreshPropertyGrid();
        }

        #endregion

        List<uint> ItemBlackList = new List<uint>();
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                WoWPoint movetoPoint = loc;
                WoWObject bank = GetLocalBanker();
                if (bank != null)
                    movetoPoint = WoWMathHelper.CalculatePointFrom(me.Location, bank.Location, 3);
                // search the database
                else if (movetoPoint == WoWPoint.Zero)
                {
                    if (Bank == BankType.Personal)
                        movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestBanker, NpcEntry);
                    else
                        movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestGB, NpcEntry);
                }
                if (movetoPoint == WoWPoint.Zero)
                    return RunStatus.Failure;
                if (movetoPoint.Distance(ObjectManager.Me.Location) > 4)
                {
                    Util.MoveTo(movetoPoint);
                    return RunStatus.Running;
                }
                else if (bank == null)
                {
                    Logging.Write(System.Drawing.Color.Red, "Unable to find a banker at location. aborting");
                    return RunStatus.Failure;
                }
                else
                {
                    // since there are many personal bank replacement addons I can't just check if frame is open and be generic.. using events isn't reliable
                    if ((Bank == BankType.Guild && !IsGbankFrameVisible) || (Bank == BankType.Personal && !Util.IsBankFrameOpen))
                    {
                        bank.Interact();
                        return RunStatus.Running;
                    }
                    List<WoWItem> itemList = BuildItemList();
                    if (itemList == null || itemList.Count == 0)
                    {
                        IsDone = true;
                    }
                    else
                    {
                        foreach (WoWItem item in itemList)
                        {
                            if (!ItemBlackList.Contains(item.Entry))
                            {
                                bool status;
                                if (Bank == BankType.Personal)
                                    status = PutItemInBank(item.Entry, Amount);
                                else
                                    status = PutItemInGBank(item.Entry, Amount, GuildTab);
                                if (status)
                                    ItemBlackList.Add(item.Entry);
                                else
                                    return RunStatus.Running;
                            }
                        }
                    }
                }
                if (IsDone)
                {
                    Professionbuddy.Log("Deposited Item with ID: {0} into {1} Bank", Entry, Bank);
                }
                else
                    return RunStatus.Running;
            }
            return RunStatus.Failure;
        }

        List<WoWItem> BuildItemList()
        {
            IEnumerable<WoWItem> tmpItemlist = from item in me.BagItems
                                               where !item.IsConjured && !item.IsSoulbound && !item.IsDisabled
                                               && !ItemBlackList.Contains(item.Entry) && !Pb.ProtectedItems.Contains(item.Entry)
                                               select item;
            if (UseCategory)
                return tmpItemlist.Where(i => i.ItemInfo.ItemClass == Category && subCategoryCheck(i)).ToList();
            else
                return tmpItemlist.Where(i => i.Entry == Entry).ToList();
        }

        bool subCategoryCheck(WoWItem item)
        {
            int sub = (int)SubCategory;
            if (sub == -1 || sub == 0)
                return true;
            object val = item.ItemInfo.GetType().GetProperties()
                .FirstOrDefault(t => t.PropertyType == SubCategory.GetType()).GetValue(item.ItemInfo, null);
            if (val != null && (int)val == sub)
                return true;
            else
                return false;
        }

        WoWObject GetLocalBanker()
        {
            WoWObject bank = null;
            List<WoWObject> bankers = null;
            if (Bank == BankType.Guild)
                bankers = (from banker in ObjectManager.ObjectList
                           where (banker is WoWGameObject && ((WoWGameObject)banker).SubType == WoWGameObjectType.GuildBank) ||
                             (banker is WoWUnit && ((WoWUnit)banker).IsGuildBanker && ((WoWUnit)banker).IsAlive && ((WoWUnit)banker).CanSelect)
                           select banker).ToList();
            else
                bankers = (from banker in ObjectManager.ObjectList
                           where (banker is WoWUnit &&
                                ((WoWUnit)banker).IsBanker &&
                                ((WoWUnit)banker).IsAlive &&
                                ((WoWUnit)banker).CanSelect)
                           select banker).ToList();
            if (bankers != null)
            {
                if (!AutoFindBank && NpcEntry != 0)
                    bank = bankers.Where(b => b.Entry == NpcEntry).OrderBy(o => o.Distance).FirstOrDefault();
                else if (AutoFindBank || loc == WoWPoint.Zero) 
                    bank = bankers.OrderBy(o => o.Distance).FirstOrDefault();
                else if (ObjectManager.Me.Location.Distance(loc) <= 90)
                {
                    bank = bankers.Where(o => o.Location.Distance(loc) < 10).
                        OrderBy(o => o.Distance).FirstOrDefault();
                }
            }
            return bank;
        }

        bool IsGbankFrameVisible { get { return Lua.GetReturnVal<int>("if GuildBankFrame and GuildBankFrame:IsVisible() then return 1 else return 0 end ", 0) == 1; } }
        Stopwatch queueServerSW;
        int _currentBag = -1;
        int _currentSlot = 1;
        public bool PutItemInGBank(uint id, int amount, uint tab)
        {
            if (queueServerSW == null)
            {
                queueServerSW = new Stopwatch();
                queueServerSW.Start();
                Lua.DoString("for i=GetNumGuildBankTabs(), 1, -1 do QueryGuildBankTab(i) end ");
                Professionbuddy.Log("Queuing server for gbank info");
                return false;
            }
            if (queueServerSW.ElapsedMilliseconds < 2000)
            {
                return false;
            }
            string lua = string.Format(
                "local tabnum = GetNumGuildBankTabs() " +
                "local bagged = 0 " +
                "local tabInfo = {{{3},{4}}} " +
                "local storeInGbank= function () " +
                   "if {2} > 0 then tabInfo[1] = {2} end " +
                   "while tabInfo[1] <= tabnum do " +
                      "_,_,v,d =GetGuildBankTabInfo(tabInfo[1]) " +
                      "if v == 1 and d == 1 then " +
                         "SetCurrentGuildBankTab(tabInfo[1]) " +
                         "for i=tabInfo[2], 98 do " +
                            "local _,c,l=GetGuildBankItemInfo(tabInfo[1], i) " +
                            "if c == 0 then " +
                               "PickupGuildBankItem(tabInfo[1] ,i) " +
                               "tabInfo[2] = i +1 " +
                               "return " +
                            "end " +
                         "end " +
                      "end " +
                      "tabInfo[2] = 1 " +
                      "tabInfo[1] = tabInfo[1] + 1 " +
                   "end " +
                "end " +
                   "for bag = 0,4 do " +
                      "for slot=GetContainerNumSlots(bag),1,-1 do " +
                         "local id = GetContainerItemID(bag,slot) " +
                         "local _,c,l = GetContainerItemInfo(bag, slot) " +
                         "if id == {0} and l == nil then  " +
                            "if c + bagged <= {1} then " +
                               "PickupContainerItem(bag,slot) " +
                               "storeInGbank() " +
                               "bagged = bagged + c " +
                            "else " +
                               "SplitContainerItem(bag,slot, {1}-bagged) " +
                               "storeInGbank() " +
                               "return unpack(tabInfo) " +
                            "end " +
                         "end " +
                         "if bagged == {1} then return unpack(tabInfo) end " +
                      "end " +
                   "end " +
                "return unpack(tabInfo) ",
                id, amount <= 0 ? int.MaxValue : amount, tab, _currentBag, _currentSlot);
            List<string> retVals = Lua.GetReturnValues(lua);
            if (retVals != null)
            {
                int.TryParse(retVals[0], out _currentBag);
                int.TryParse(retVals[1], out _currentSlot);
                return true;
            }
            return false;
        }

        public bool PutItemInBank(uint id, int amount)
        {
            string lua = string.Format(
                "local bagged = 0 " +
                "local bagInfo = {{{2},{3}}} " +
                "local storeInBank= function () " +
                   "while bagInfo[1] <= 11 do " +
                      "local itemf  = GetItemFamily({0}) " +
                      "local fs,bfamily = GetContainerNumFreeSlots(bagInfo[1]) " +
                      "if fs > 0 and (bfamily == 0 or bit.band(itemf, bfamily) > 0) then " +
                         "for i=bagInfo[2], GetContainerNumSlots(bagInfo[1]) do " +
                            "local _,c,l = GetContainerItemInfo(bagInfo[1], i) " +
                            "if c == nil then " +
                               "PickupContainerItem(bagInfo[1],i) " +
                               "bagInfo[2] = i +1 " +
                               "return " +
                            "end " +
                         "end " +
                      "end " +
                       "bagInfo[2] = 1 " +
                      "bagInfo[1] = bagInfo[1] + 1 " +
                      "if bagInfo[1] == 0 then bagInfo[1] = 5 end " +
                   "end " +
                "end " +
                "for bag = 0,4 do " +
                   "for slot=GetContainerNumSlots(bag),1,-1 do " +
                      "local id = GetContainerItemID(bag,slot) " +
                      "local _,c,l = GetContainerItemInfo(bag, slot) " +
                      "local _,_,_,_,_,_,_, maxStack = GetItemInfo(id or 0) " +
                      "if id == {0} and l == nil then " +
                         "if c + bagged <= {1} then " +
                            "PickupContainerItem(bag, slot) " +
                            "storeInBank() " +
                            "bagged = bagged + c " +
                         "else " +
                            "SplitContainerItem(bag,slot, {1}-bagged) " +
                            "storeInBank() " +
                            "return unpack(bagInfo) " +
                         "end " +
                      "end " +
                      "if bagged == {1} then return unpack(bagInfo) end " +
                   "end " +
                "end " +
                "return unpack(bagInfo) "
                , id, amount <= 0 ? int.MaxValue : amount, _currentBag, _currentSlot);
            List<string> retVals = Lua.GetReturnValues(lua);
            int.TryParse(retVals[0], out _currentBag);
            int.TryParse(retVals[1], out _currentSlot);
            return true;
        }
        public override string Name { get { return "Deposit Item in Bank"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} {2}", Name,
                    UseCategory ? string.Format("{0} {1}", Category, SubCategory) : Entry.ToString(),
                    Amount > 0 ? Amount.ToString() : "");
            }
        }
        public override string Help
        {
            get
            {
                return "This action will deposit the specified item/s into your personal or guild bank. Set Amount to 0 if you want to deposit all items that match Entry or Category. Set GuildTab to 0 to deposit in whichever tab has room";
            }
        }
        public override void Reset()
        {
            base.Reset();
            queueServerSW = null;
            ItemBlackList = new List<uint>();
            _currentBag = -1;
            _currentSlot = 1;
        }
        public override object Clone()
        {
            return new PutItemInBankAction()
            {
                Entry = this.Entry,
                Amount = this.Amount,
                Bank = this.Bank,
                NpcEntry = this.NpcEntry,
                loc = this.loc,
                GuildTab = this.GuildTab,
                AutoFindBank = this.AutoFindBank,
                Parent = this.Parent,
                Location = this.Location,
                UseCategory = this.UseCategory,
                Category = this.Category,
                SubCategory = this.SubCategory,
            };
        }
        #region XmlSerializer
        public override void ReadXml(XmlReader reader)
        {
            uint id;
            uint.TryParse(reader["Amount"], out id);
            Amount = (int)id;
            uint.TryParse(reader["Entry"], out id);
            Entry = id;
            uint.TryParse(reader["NpcEntry"], out id);
            NpcEntry = id;
            uint.TryParse(reader["GuildTab"], out id);
            GuildTab = id;
            bool boolVal = false;
            if (reader.MoveToAttribute("UseCategory"))
            {
                bool.TryParse(reader["UseCategory"], out boolVal);
                UseCategory = boolVal;
            }
            if (reader.MoveToAttribute("Category"))
            {
                Category = (WoWItemClass)Enum.Parse(typeof(WoWItemClass), reader["Category"]);
            }
            string subCatType = "";
            if (reader.MoveToAttribute("SubCategoryType"))
            {
                subCatType = reader["SubCategoryType"];
            }
            if (reader.MoveToAttribute("SubCategory") && !string.IsNullOrEmpty(subCatType))
            {
                Type t;
                if (subCatType != "SubCategoryType")
                {
                    string typeName = string.Format("Styx.{0}", subCatType);
                    t = Assembly.GetEntryAssembly().GetType(typeName);
                }
                else
                    t = typeof(SubCategoryType);
                object subVal = Activator.CreateInstance(t);
                subVal = Enum.Parse(t, reader["SubCategory"]);
                SubCategory = subVal;
            }

            bool autoFind;
            bool.TryParse(reader["AutoFindBank"], out autoFind);
            AutoFindBank = autoFind;
            Bank = (BankType)Enum.Parse(typeof(BankType), reader["Bank"]);
            float x, y, z;
            x = reader["X"].ToSingle();
            y = reader["Y"].ToSingle();
            z = reader["Z"].ToSingle();
            loc = new WoWPoint(x, y, z);
            Properties["Location"].Value = loc.ToInvariantString();
            reader.ReadStartElement();
        }
        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("Amount", Amount.ToString());
            writer.WriteAttributeString("Entry", Entry.ToString());
            writer.WriteAttributeString("NpcEntry", NpcEntry.ToString());
            writer.WriteAttributeString("GuildTab", GuildTab.ToString());
            writer.WriteAttributeString("AutoFindBank", AutoFindBank.ToString());
            writer.WriteAttributeString("UseCategory", UseCategory.ToString());
            writer.WriteAttributeString("Category", Category.ToString());
            writer.WriteAttributeString("SubCategoryType", SubCategory.GetType().Name);
            writer.WriteAttributeString("SubCategory", SubCategory.ToString());
            writer.WriteAttributeString("Bank", Bank.ToString());
            writer.WriteAttributeString("X", loc.X.ToString());
            writer.WriteAttributeString("Y", loc.Y.ToString());
            writer.WriteAttributeString("Z", loc.Z.ToString());
        }
        #endregion
    }
    #endregion
}
