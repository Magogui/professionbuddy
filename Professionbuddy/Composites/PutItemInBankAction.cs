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
        #region Strings
        static string _gbankSlotInfo =
             "local _,c,l=GetGuildBankItemInfo({0}, {1}) " +
             "if c > 0 and l == nil then " +
                 "local id = tonumber(string.match(GetGuildBankItemLink({0},{1}), 'Hitem:(%d+)')) " +
                 "local maxStack = select(8,GetItemInfo(id)) " +
                 "return id,c,maxStack " +
             "elseif c == 0 then " +
                "return 0,0,0 " +
             "end ";
        #endregion

        class BankSlotInfo : IEquatable<BankSlotInfo>
        {
            public BankSlotInfo(int bag, int bagType, int slot, uint itemID, uint stackSize, int maxStackSize)
            {
                this.Bag = bag;
                this.BagType = bagType;
                this.Slot = slot;
                this.ItemID = itemID;
                this.StackSize = stackSize;
                this.MaxStackSize = maxStackSize;
            }
            public int Bag { get; private set; } // this is also the tab for GBank
            public int BagType { get; private set; }
            public int Slot { get; private set; }
            public uint ItemID { get; set; } //  0 if slot is has no items.
            public uint StackSize { get; set; } // amount of items in slot..
            public int MaxStackSize { get; set; }
            // public static BankSlotInfo Zero { get { return new BankSlotInfo(0, 0, 0, 0, 0, 0); } }

            public bool Equals(BankSlotInfo other)
            {
                return other != null && this.Bag == other.Bag && this.Slot == other.Slot;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as BankSlotInfo);
            }

            public override int GetHashCode()
            {
                return (Bag * 1000 + Slot).GetHashCode();
            }

            public static bool operator ==(BankSlotInfo a, BankSlotInfo b)
            {
                if ((object)a == null || (object)b == null)
                    return Object.Equals(a, b);
                return a.Equals(b);
            }

            public static bool operator !=(BankSlotInfo a, BankSlotInfo b)
            {
                return !(a == b);
            }
        }

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

        public string ItemID
        {
            get { return (string)Properties["ItemID"].Value; }
            set { Properties["ItemID"].Value = value; }
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
            Properties["ItemID"] = new MetaProp("ItemID", typeof(string));
            Properties["Bank"] = new MetaProp("Bank", typeof(BankType));
            Properties["AutoFindBank"] = new MetaProp("AutoFindBank", typeof(bool), new DisplayNameAttribute("Auto find Bank"));
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));
            Properties["NpcEntry"] = new MetaProp("NpcEntry", typeof(uint), new EditorAttribute(typeof(PropertyBag.EntryEditor), typeof(UITypeEditor)));
            Properties["GuildTab"] = new MetaProp("GuildTab", typeof(uint));
            Properties["UseCategory"] = new MetaProp("UseCategory", typeof(bool), new DisplayNameAttribute("Use Category"));
            Properties["Category"] = new MetaProp("Category", typeof(WoWItemClass), new DisplayNameAttribute("Item Category"));
            Properties["SubCategory"] = new MetaProp("SubCategory", typeof(WoWItemTradeGoodsClass), new DisplayNameAttribute("Item SubCategory"));

            Amount = 0;
            ItemID = "";
            Bank = BankType.Personal;
            AutoFindBank = true;
            loc = WoWPoint.Zero;
            Location = loc.ToInvariantString();
            NpcEntry = 0u;
            GuildTab = 0u;
            UseCategory = true;
            Category = WoWItemClass.TradeGoods;
            SubCategory = WoWItemTradeGoodsClass.None;

            Properties["ItemID"].Show = false;
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
                Properties["ItemID"].Show = false;
                Properties["Category"].Show = true;
                Properties["SubCategory"].Show = true;
            }
            else
            {
                Properties["ItemID"].Show = true;
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
        Dictionary<uint, int> ItemList = null;
        //bool _switchingTabs = false;
        Stopwatch _gbankItemThrottleSW = new Stopwatch();
        const long _gbankItemThrottle = 800; // 8 times per sec.
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if ((Bank == BankType.Guild && !IsGbankFrameVisible) ||
                                 (Bank == BankType.Personal && !Util.IsBankFrameOpen))
                {
                    MoveToBanker();
                }
                else
                {
                    if (_itemsSW == null)
                    {
                        _itemsSW = new Stopwatch();
                        _itemsSW.Start();
                    }
                    else if (_itemsSW.ElapsedMilliseconds < Util.WoWPing * 3)
                        return RunStatus.Running;
                    if (ItemList == null)
                        ItemList = BuildItemList();
                    // no bag space... 
                    if (ItemList.Count == 0)
                        IsDone = true;
                    else
                    {
                        uint itemID = ItemList.Keys.FirstOrDefault();
                        bool done = false;
                        if (Bank == BankType.Personal)
                            done = PutItemInBank(itemID, ItemList[itemID]);
                        else
                        {
                            // throttle the amount of items being withdrawn from gbank per sec
                            if (!_gbankItemThrottleSW.IsRunning)
                                _gbankItemThrottleSW.Start();
                            if (_gbankItemThrottleSW.ElapsedMilliseconds < _gbankItemThrottle)
                                return RunStatus.Success;
                            else
                            {
                                _gbankItemThrottleSW.Reset();
                                _gbankItemThrottleSW.Start();
                            }
                            int ret = PutItemInGBank(itemID, ItemList[itemID], GuildTab);
                            ItemList[itemID] = ret == -1 ? 0 : ItemList[itemID] - ret;
                            if (ItemList[itemID] <= 0)
                                done = true;
                            else
                                done = false;
                        }
                        if (done)
                        {
                            Professionbuddy.Debug("Done Depositing Item:{0} to bank", itemID);
                            ItemList.Remove(itemID);
                        }
                        _itemsSW.Reset();
                        _itemsSW.Start();
                    }
                }
                if (IsDone)
                {
                    Professionbuddy.Log("Deposited Items:[{0}] to {1} Bank", ItemID, Bank);
                }
                else
                    return RunStatus.Running;
            }
            return RunStatus.Failure;
        }

        void MoveToBanker()
        {
            WoWPoint movetoPoint = loc;
            WoWObject bank = GetLocalBanker();
            if (bank != null)
                movetoPoint = WoWMathHelper.CalculatePointFrom(me.Location, bank.Location, 4);
            // search the database
            else if (movetoPoint == WoWPoint.Zero)
            {
                if (Bank == BankType.Personal)
                    movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestBanker, NpcEntry);
                else
                    movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestGB, NpcEntry);
            }
            if (movetoPoint == WoWPoint.Zero)
            {
                IsDone = true;
                Professionbuddy.Err("Unable to find bank");
            }
            if (movetoPoint.Distance(ObjectManager.Me.Location) > 4)
            {
                Util.MoveTo(movetoPoint);
            }
            // since there are many personal bank replacement addons I can't just check if frame is open and be generic.. using events isn't reliable
            else if (bank != null)
            {
                bank.Interact();
            }
            else
            {
                IsDone = true;
                Logging.Write(System.Drawing.Color.Red, "Unable to find a banker at location. aborting");
            }
        }

        Dictionary<uint, int> BuildItemList()
        {
            Dictionary<uint, int> itemList = new Dictionary<uint, int>();
            IEnumerable<WoWItem> tmpItemlist = from item in me.BagItems
                                               where !item.IsConjured && !item.IsSoulbound && !item.IsDisabled
                                               select item;
            if (UseCategory)
                foreach (WoWItem item in tmpItemlist)
                {
                    if (!Pb.ProtectedItems.Contains(item.Entry) && item.ItemInfo.ItemClass == Category &&
                        subCategoryCheck(item) && !itemList.ContainsKey(item.Entry))
                    {
                        itemList.Add(item.Entry, Amount > 0 ? Amount : (int)Util.GetCarriedItemCount(item.Entry));
                    }
                }
            else
            {
                string[] entries = ItemID.Split(',');
                if (entries != null && entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        uint itemID = 0;
                        uint.TryParse(entry.Trim(), out itemID);
                        itemList.Add(itemID, Amount > 0 ? Amount : (int)Util.GetCarriedItemCount(itemID));
                    }
                }
                else
                {
                    Professionbuddy.Err("No ItemIDs are specified");
                    IsDone = true;
                }
            }
            return itemList;
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

        #region GuildBank
        bool IsGbankFrameVisible { get { return Lua.GetReturnVal<int>("if GuildBankFrame and GuildBankFrame:IsVisible() then return 1 else return 0 end ", 0) == 1; } }
        Stopwatch queueServerSW;
        Stopwatch _itemsSW;
        List<BankSlotInfo> _bankSlots;

        public int PutItemInGBank(uint id, int amount, uint tab)
        {
            using (new FrameLock())
            {
                if (queueServerSW == null)
                {
                    queueServerSW = new Stopwatch();
                    queueServerSW.Start();
                    Lua.DoString("for i=GetNumGuildBankTabs(), 1, -1 do QueryGuildBankTab(i) end SetCurrentGuildBankTab({0}) ", tab == 0 ? 1 : tab);
                    Professionbuddy.Log("Queuing server for gbank info");
                    return 0;
                }
                else if (queueServerSW.ElapsedMilliseconds < 2000)
                    return 0;
                if (_bankSlots == null)
                    _bankSlots = GetBankSlotInfo();
                int tabCnt = Lua.GetReturnVal<int>("return GetNumGuildBankTabs()", 0);
                int currentTab = Lua.GetReturnVal<int>("return GetCurrentGuildBankTab()", 0);

                IEnumerable<BankSlotInfo> slotsInCurrentTab = _bankSlots.Where(slotI => slotI.Bag == currentTab);
                WoWItem itemToDeposit = me.CarriedItems.OrderBy(item => item.StackCount)
                    .FirstOrDefault(item => item.Entry == id && !item.IsDisabled);
                if (itemToDeposit != null)
                {
                    int depositAmount = amount > 0 && amount < (int)itemToDeposit.StackCount ?
                       amount : (int)itemToDeposit.StackCount;

                    BankSlotInfo emptySlot = slotsInCurrentTab.FirstOrDefault(slotI => slotI.StackSize == 0);
                    BankSlotInfo partialStack = slotsInCurrentTab
                        .FirstOrDefault(slotI => slotI.ItemID == id && slotI.MaxStackSize - slotI.StackSize >= depositAmount);
                    if (partialStack != null || emptySlot != null)
                    {
                        bool slotIsEmpty = partialStack == null;
                        int bSlotIndex = slotIsEmpty ? _bankSlots.IndexOf(emptySlot) : _bankSlots.IndexOf(partialStack);
                        _bankSlots[bSlotIndex].StackSize += itemToDeposit.StackCount;
                        if (slotIsEmpty)
                        {
                            _bankSlots[bSlotIndex].ItemID = itemToDeposit.Entry;
                            _bankSlots[bSlotIndex].MaxStackSize = itemToDeposit.ItemInfo.MaxStackSize;
                        }
                        if (depositAmount == itemToDeposit.StackCount)
                            itemToDeposit.UseContainerItem();
                        else
                        {
                            Lua.DoString("SplitContainerItem({0},{1},{2}) PickupGuildBankItem({3},{4})",
                                itemToDeposit.BagIndex + 1, itemToDeposit.BagSlot + 1,depositAmount, _bankSlots[bSlotIndex].Bag, _bankSlots[bSlotIndex].Slot);
                        }
                        return depositAmount;
                    }
                    if (tab == 0 && currentTab < tabCnt)
                    {
                        Lua.DoString("SetCurrentGuildBankTab({0})", currentTab + 1);
                        return 0;
                    }
                }
                return -1;
            }
        }

        const int GuildTabSlotNum = 98;
        /// <summary>
        /// Returns a list of bag/gbank tab slots with empty/partial full slots.
        /// </summary>
        /// <returns></returns>
        List<BankSlotInfo> GetBankSlotInfo()
        {
            List<BankSlotInfo> bankSlotInfo = new List<BankSlotInfo>();
            using (new FrameLock())
            {
                if (Bank == BankType.Guild)
                {
                    int tabCnt = Lua.GetReturnVal<int>("return GetNumGuildBankTabs()", 0);
                    int minTab = GuildTab > 0 ? (int)GuildTab : 1;
                    int maxTab = GuildTab > 0 ? (int)GuildTab : tabCnt;
                    for (int tab = minTab; tab <= maxTab; tab++)
                    {
                        // check permissions for tab
                        bool canDespositInTab =
                            Lua.GetReturnVal<int>(string.Format("local _,_,v,d =GetGuildBankTabInfo({0}) if v==1 and d==1 then return 1 else return 0 end", tab), 0) == 1;
                        if (canDespositInTab)
                        {
                            for (int slot = 1; slot <= GuildTabSlotNum; slot++)
                            {
                                // 3 return values in following order, ItemID,StackSize,MaxStackSize
                                string lua = string.Format(_gbankSlotInfo, tab, slot);
                                List<string> retVals = Lua.GetReturnValues(lua);
                                bankSlotInfo.Add(new BankSlotInfo(tab, 0, slot, uint.Parse(retVals[0]), uint.Parse(retVals[1]), int.Parse(retVals[2])));
                            }
                        }
                    }
                }
                else
                {

                }
            }
            return bankSlotInfo;
        }

        #endregion

        public bool PutItemInBank(uint id, int amount)
        {
            string lua = string.Format(
                "local bagged = 0 " +
                "local bagInfo = {{0}} " +
                "local bag = -1 " +
                "local i=1; " +
                "local _,_,_,_,_,_,_,maxStack = GetItemInfo({0}) " +
                "while bag <= 11 do " +
                   "local itemf  = GetItemFamily({0}) " +
                   "local fs,bfamily = GetContainerNumFreeSlots(bag) " +
                   "if fs > 0 and (bfamily == 0 or bit.band(itemf, bfamily) > 0) then " +
                      "for slot=1, GetContainerNumSlots(bag) do " +
                         "local _,c,l = GetContainerItemInfo(bag, slot) " +
                         "local id = GetContainerItemID(bag, slot) or 0 " +
                         "if c == nil then " +
                            "bagInfo[i]={{bag,slot,maxStack}} " +
                            "i=i+1 " +
                         "elseif l == nil and id == {0} and c < maxStack then " +
                            "bagInfo[i]={{bag,slot,maxStack-c}} " +
                            "i=i+1 " +
                         "end " +
                      "end " +
                   "end " +
                   "bag = bag + 1 " +
                   "if bag == 0 then bag = 5 end " +
                "end " +
                "i=1 " +
                "for bag = 0,4 do " +
                   "for slot=1,GetContainerNumSlots(bag) do " +
                      "if i > #bagInfo then return end " +
                      "local id = GetContainerItemID(bag,slot) or 0 " +
                      "local _,c,l = GetContainerItemInfo(bag, slot) " +
                      "local _,_,_,_,_,_,_, maxStack = GetItemInfo(id) " +
                      "if id == {0} and l == nil then " +
                         "if c + bagged <= {1} and c <= bagInfo[i][3] then " +
                            "PickupContainerItem(bag, slot) " +
                            "PickupContainerItem(bagInfo[i][1], bagInfo[i][2]) " +
                            "bagged = bagged + c " +
                         "else " +
                            "local cnt = {1}-bagged " +
                            "if cnt > bagInfo[i][3] then cnt = bagInfo[i][3] end " +
                            "SplitContainerItem(bag,slot, cnt) " +
                            "PickupContainerItem(bagInfo[i][1], bagInfo[i][2]) " +
                            "bagged = bagged + cnt " +
                         "end " +
                         "i=i+1 " +
                      "end " +
                      "if bagged == {1} then return end " +
                   "end " +
                "end return "
                , id, amount <= 0 ? int.MaxValue : amount);
            Lua.DoString(lua);
            return true;
        }
        public override string Name { get { return "Deposit Item in Bank"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} {2}", Name,
                    UseCategory ? string.Format("{0} {1}", Category, SubCategory) : ItemID.ToString(),
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
            _bankSlots = null;
            ItemList = null;
            _itemsSW = null;
        }
        public override object Clone()
        {
            return new PutItemInBankAction()
            {
                ItemID = this.ItemID,
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
            if (reader.MoveToAttribute("ItemID"))
                ItemID = reader["ItemID"];
            else if (reader.MoveToAttribute("Entry"))
                ItemID = reader["Entry"];
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
            writer.WriteAttributeString("ItemID", ItemID);
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
