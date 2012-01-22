using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HighVoltz.Dynamic;
using Styx;
using Styx.Logic.Inventory.Frames.MailBox;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using System.Reflection;
using System.Diagnostics;

namespace HighVoltz.Composites
{
    #region MailItemAction

    sealed class MailItemAction : PBAction
    {
        [PbXmlAttribute]
        public bool UseCategory
        {
            get { return (bool)Properties["UseCategory"].Value; }
            set { Properties["UseCategory"].Value = value; }
        }

        public WoWItemClass Category
        {
            get { return (WoWItemClass)Properties["Category"].Value; }
            set
            {
                Properties["Category"].Value = value;
            }
        }
        public object SubCategory
        {
            get
            {
                return Properties["SubCategory"].Value;
            }
            set
            {
                Properties["SubCategory"].Value = value;
            }
        }

        [PbXmlAttribute]
        [PbXmlAttribute("Entry")]
        public string ItemID
        {
            get { return (string)Properties["ItemID"].Value; }
            set { Properties["ItemID"].Value = value; }
        }
        [PbXmlAttribute]
        [TypeConverter(typeof(DynamicProperty<int>.DynamivExpressionConverter))]
        public DynamicProperty<int> Amount
        {
            get { return (DynamicProperty<int>)Properties["Amount"].Value; }
            set { Properties["Amount"].Value = value; }
        }
        [PbXmlAttribute]
        public bool AutoFindMailBox
        {
            get { return (bool)Properties["AutoFindMailBox"].Value; }
            set { Properties["AutoFindMailBox"].Value = value; }
        }

        WoWPoint _loc;
        [PbXmlAttribute]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public MailItemAction()
        {
            Properties["ItemID"] = new MetaProp("ItemID", typeof(string));
            Properties["AutoFindMailBox"] = new MetaProp("AutoFindMailBox", typeof(bool), new DisplayNameAttribute("Automatically find Mailbox"));
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));
            Properties["UseCategory"] = new MetaProp("UseCategory", typeof(bool), new DisplayNameAttribute("Use Category"));
            Properties["Category"] = new MetaProp("Category", typeof(WoWItemClass), new DisplayNameAttribute("Item Category"));
            Properties["SubCategory"] = new MetaProp("SubCategory", typeof(WoWItemTradeGoodsClass), new DisplayNameAttribute("Item SubCategory"));
            Properties["Amount"] = new MetaProp("Amount", typeof(DynamicProperty<int>),
                new TypeConverterAttribute(typeof(DynamicProperty<int>.DynamivExpressionConverter)));

            ItemID = "";
            AutoFindMailBox = true;
            _loc = WoWPoint.Zero;
            Location = _loc.ToInvariantString();
            UseCategory = true;
            Category = WoWItemClass.TradeGoods;
            SubCategory = WoWItemTradeGoodsClass.None;
            Amount = new DynamicProperty<int>(this, "0");
            RegisterDynamicProperty("Amount");

            Properties["Location"].Show = false;
            Properties["ItemID"].Show = false;
            Properties["AutoFindMailBox"].PropertyChanged += AutoFindMailBoxChanged;
            Properties["Location"].PropertyChanged += LocationChanged;
            Properties["UseCategory"].PropertyChanged += UseCategoryChanged;
            Properties["Category"].PropertyChanged += CategoryChanged;
        }

        #region Callbacks
        void LocationChanged(object sender, MetaPropArgs e)
        {
            _loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= LocationChanged;
            Properties["Location"].Value = string.Format("{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
            Properties["Location"].PropertyChanged += LocationChanged;
            RefreshPropertyGrid();
        }
        void UseCategoryChanged(object sender, MetaPropArgs e)
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

        void CategoryChanged(object sender, MetaPropArgs e)
        {
            object subCategory = Callbacks.GetSubCategory(Category);
            Properties["SubCategory"] = new MetaProp("SubCategory", subCategory.GetType(),
                new DisplayNameAttribute("Item SubCategory"));
            SubCategory = subCategory;
            RefreshPropertyGrid();
        }

        void AutoFindMailBoxChanged(object sender, MetaPropArgs e)
        {
            Properties["Location"].Show = !AutoFindMailBox;
            RefreshPropertyGrid();
        }

        #endregion

        WoWGameObject _mailbox;
        Dictionary<uint, int> _itemList;
        readonly Stopwatch _itemSplitSW = new Stopwatch();
        string _mailSubject;
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                WoWPoint movetoPoint = _loc;
                if (MailFrame.Instance == null || !MailFrame.Instance.IsVisible)
                {
                    if (AutoFindMailBox || movetoPoint == WoWPoint.Zero)
                    {
                        _mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.Mailbox)
                            .OrderBy(o => o.Distance).FirstOrDefault();
                    }
                    else
                    {
                        _mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.Mailbox
                            && o.Location.Distance(_loc) < 10)
                            .OrderBy(o => o.Distance).FirstOrDefault();
                    }
                    if (_mailbox != null)
                        movetoPoint = WoWMathHelper.CalculatePointFrom(Me.Location, _mailbox.Location, 3);
                    if (movetoPoint == WoWPoint.Zero)
                    {
                        Professionbuddy.Err("Unable To find Mailbox");
                        IsDone = true;
                        return RunStatus.Failure;
                    }
                    if (movetoPoint.Distance(ObjectManager.Me.Location) > 4.5)
                        Util.MoveTo(movetoPoint);
                    else if (_mailbox != null)
                    {
                        _mailbox.Interact();
                    }
                    return RunStatus.Success;
                }
                // Mail Frame is open..
                // item split in proceess
                if (_itemSplitSW.IsRunning && _itemSplitSW.ElapsedMilliseconds <= 2000)
                    return RunStatus.Success;
                if (_itemList == null)
                    _itemList = BuildItemList();
                if (_itemList.Count == 0)
                {
                    //Professionbuddy.Debug("Sending any remaining items already in SendMail item slots. Mail subject will be: {0} ",_mailSubject);
                    Lua.DoString("for i=1,ATTACHMENTS_MAX_SEND do if GetSendMailItem(i) ~= nil then SendMail (\"{0}\",\"{1}\",'') end end ",
                        CharacterSettings.Instance.MailRecipient.ToFormatedUTF8(), _mailSubject != null ? _mailSubject.ToFormatedUTF8() : " ");
                    //Professionbuddy.Debug("Done sending mail");
                    IsDone = true;
                    return RunStatus.Failure;
                }

                MailFrame.Instance.SwitchToSendMailTab();
                uint itemID = _itemList.Keys.FirstOrDefault();
                WoWItem item = Me.BagItems.FirstOrDefault(i => i.Entry == itemID);
                _mailSubject = item != null ? item.Name : " ";
                if (string.IsNullOrEmpty(_mailSubject))
                    _mailSubject = " ";
                Professionbuddy.Debug("MailItem: sending {0}", itemID);
                int ret = MailItem(itemID, _itemList[itemID]);
                // we need to wait for item split to finish if ret == 0
                // format indexs are MailRecipient=0, Mail subject=1
                string mailToLua = string.Format(MailItemsFormat, CharacterSettings.Instance.MailRecipient.ToFormatedUTF8(), _mailSubject.ToFormatedUTF8());
                var mailItemsRet = Lua.GetReturnVal<int>(mailToLua, 0);
                if (ret == 0 || mailItemsRet == 1)
                {
                    _itemSplitSW.Reset();
                    _itemSplitSW.Start();
                    return RunStatus.Success;
                }
                _itemList[itemID] = ret == -1 ? 0 : _itemList[itemID] - ret;

                bool done = _itemList[itemID] <= 0;
                if (done)
                {
                    _itemList.Remove(itemID);
                }
                if (IsDone)
                {
                    Professionbuddy.Log("Done sending {0} via mail",
                                        UseCategory ? string.Format("Items that belong to category {0} and subcategory {1}", Category, SubCategory) :
                                        string.Format("Items that match Id of {0}", ItemID));
                }
                else
                    return RunStatus.Success;
            }
            return RunStatus.Failure;
        }

        Dictionary<uint, int> BuildItemList()
        {
            var itemList = new Dictionary<uint, int>();
            IEnumerable<WoWItem> tmpItemlist = from item in Me.BagItems
                                               where !item.IsConjured && !item.IsSoulbound && !item.IsDisabled
                                               select item;
            if (UseCategory)
                foreach (WoWItem item in tmpItemlist)
                {
                    if (!Pb.ProtectedItems.Contains(item.Entry) && item.ItemInfo.ItemClass == Category &&
                        SubCategoryCheck(item) && !itemList.ContainsKey(item.Entry))
                    {
                        itemList.Add(item.Entry, Amount > 0 ? Amount : Util.GetCarriedItemCount(item.Entry));
                    }
                }
            else
            {
                string[] entries = ItemID.Split(',');
                if (entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        uint itemID;
                        uint.TryParse(entry.Trim(), out itemID);
                        itemList.Add(itemID, Amount > 0 ? Amount : Util.GetCarriedItemCount(itemID));
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

        bool SubCategoryCheck(WoWItem item)
        {
            var sub = (int)SubCategory;
            if (sub == -1 || sub == 0)
                return true;
            var firstOrDefault = item.ItemInfo.GetType().GetProperties().FirstOrDefault(t => t.PropertyType == SubCategory.GetType());
            if (firstOrDefault != null)
            {
                object val = firstOrDefault.GetValue(item.ItemInfo, null);
                if (val != null && (int)val == sub)
                    return true;
            }
            return false;
        }

        // format indexs are ItemID=0, Amount=1
        private const string MailItemLuaFormat =
            "local mailItemI =1 " +
            "local freeBagSlots = 0 " +
            "local amount = {1} " +
            "local itemId = {0} " +
            "local bagged =0 " +
            "for i=0,NUM_BAG_SLOTS do " +
                "freeBagSlots = freeBagSlots + GetContainerNumFreeSlots(i) " +
            "end " +
            "local bagInfo={{}} " +
            "for bag = 0,NUM_BAG_SLOTS do " +
                "for slot=1,GetContainerNumSlots(bag) do " +
                    "local id = GetContainerItemID(bag,slot) or 0 " +
                    "local _,c,l = GetContainerItemInfo(bag, slot) " +
                    "if id == itemId and l == nil then " +
                        "table.insert(bagInfo,{{bag,slot,c}}) " +
                    "end " +
                "end " +
            "end " +
            "local sortF = function (a,b) " +
                "if a == nil and b == nil or b == nil then return false end " +
                "if a == nil or  a[3] < b[3] then return true else return false end " +
            "end " +
            "if #bagInfo == 0 then return -1 end " +
            "table.sort(bagInfo,sortF) " +
            "local bagI = #bagInfo " +
            "while bagI > 0 do " +
                "if GetSendMailItem(mailItemI) == nil then " +
                    "while bagInfo[bagI][3] > amount-bagged and bagI >1 do bagI = bagI - 1 end " +
                    "if bagInfo[bagI][3] + bagged <= amount or freeBagSlots == 0 then " +
                        "PickupContainerItem(bagInfo[bagI][1], bagInfo[bagI][2]) " +
                        "ClickSendMailItemButton(mailItemI) " +
                        "bagged = bagged + bagInfo[bagI][3] " +
                        "bagI = bagI - 1 " +
                        "return bagged " +
                    "else " +
                        "local cnt = bagInfo[bagI][3]-amount " +
                        "SplitContainerItem(bagInfo[bagI][1],bagInfo[bagI][2], cnt) " +
                        "local bagSpaces ={{}} " + "for b=NUM_BAG_SLOTS,0,-1 do " +
                        "bagSpaces = GetContainerFreeSlots(b) " +
                        "if #bagSpaces > 0 then " +
                            "PickupContainerItem(b,bagSpaces[#bagSpaces]) " +
                            "return 0 " +
                        "end " +
                    "end " +
                "end " +
            "end " +
            "if bagged >= amount then return -1 end " +
            "mailItemI = mailItemI + 1 " +
            "if mailItemI > ATTACHMENTS_MAX_SEND then " +
                "return bagged " +
            "end " +
        "end " +
        "return bagged ";

        // format indexs are MailRecipient=0, Mail subject=1
        private const string MailItemsFormat =
            "local cnt = 0 " +
            "for i=1,ATTACHMENTS_MAX_SEND do " +
                "if GetSendMailItem(i) ~= nil then cnt = cnt + 1 end " +
            "end " +
            "if cnt == ATTACHMENTS_MAX_SEND then " +
                "SendMail (\"{0}\",\"{1}\",'') " +
                "return 1 " +
            "end " +
            "return 0 ";

        // return -1 if done,0 if spliting item else the amount of items placed in mail.
        int MailItem(uint id, int amount)
        {
            // format indexs are ItemID=0, Amount=1, MailRecipient=2
            string lua = string.Format(MailItemLuaFormat, id, amount);
            return Lua.GetReturnVal<int>(lua, 0);
        }

        public override void Reset()
        {
            _itemList = null;
            base.Reset();
        }
        public override string Name
        {
            get
            {
                return "Mail Item";
            }
        }
        public override string Title
        {
            get
            {
                return string.Format("{0}: to:{1} {2} ", Name, CharacterSettings.Instance.MailRecipient,
                    UseCategory ? string.Format("{0} {1}", Category, SubCategory) : ItemID);
            }
        }
        public override string Help
        {
            get
            {
                return "This action will mail either all items that match Item ID or by item category.Setting Amount to 0 will mail all items that match Entry. Note: Amount = axact number, not stacks. This mails items to the 'Mail Recipient' from Honorbuddy settings. ";
            }
        }
        public override object Clone()
        {
            return new MailItemAction
                       {
                           ItemID = this.ItemID,
                           _loc = this._loc,
                           AutoFindMailBox = this.AutoFindMailBox,
                           Location = this.Location,
                           UseCategory = this.UseCategory,
                           Category = this.Category,
                           SubCategory = this.SubCategory,
                           Amount = this.Amount
                       };
        }

        public override void OnProfileLoad(XElement element)
        {
            var cat = element.Attribute("Category");
            var subCatAttr = element.Attribute("SubCategory");
            var subCatTypeAttr = element.Attribute("SubCategoryType");
            if (cat != null)
            {
                Category = (WoWItemClass)Enum.Parse(typeof(WoWItemClass), cat.Value);
                cat.Remove();
            }
            if (subCatAttr != null && subCatTypeAttr != null)
            {
                Type subCategoryType;
                if (subCatTypeAttr.Value != "SubCategoryType")
                {
                    string typeName = string.Format("Styx.{0}", subCatTypeAttr.Value);
                    subCategoryType = Assembly.GetEntryAssembly().GetType(typeName);
                }
                else
                    subCategoryType = typeof(SubCategoryType);
                SubCategory = Enum.Parse(subCategoryType, subCatAttr.Value);
                subCatAttr.Remove();
                subCatTypeAttr.Remove();
            }
        }

        public override void OnProfileSave(XElement element)
        {
            element.Add(new XAttribute("Category", Category.ToString()));
            element.Add(new XAttribute("SubCategoryType", SubCategory.GetType().Name));
            element.Add(new XAttribute("SubCategory", SubCategory.ToString()));
        }
    }
    #endregion
}
