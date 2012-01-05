using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using System.Xml;
using Styx;
using Styx.Logic.Inventory.Frames.MailBox;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using TreeSharp;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using System.Collections.Generic;


namespace HighVoltz.Composites
{
    #region GetMailAction
    class GetMailAction : PBAction
    {
        public enum GetMailActionType
        {
            AllItems,
            Specific,
        }
        [PbXmlAttribute()]
        public GetMailActionType GetMailType
        {
            get { return (GetMailActionType)Properties["GetMailType"].Value; }
            set { Properties["GetMailType"].Value = value; }
        }
        [PbXmlAttribute()]
        [PbXmlAttribute("Entry")]
        public string ItemID
        {
            get { return (string)Properties["ItemID"].Value; }
            set { Properties["ItemID"].Value = value; }
        }
        [PbXmlAttribute()]
        public bool CheckNewMail
        {
            get { return (bool)Properties["CheckNewMail"].Value; }
            set { Properties["CheckNewMail"].Value = value; }
        }
        [PbXmlAttribute()]
        public uint MinFreeBagSlots
        {
            get { return (uint)Properties["MinFreeBagSlots"].Value; }
            set { Properties["MinFreeBagSlots"].Value = value; }
        }
        [PbXmlAttribute()]
        public bool AutoFindMailBox
        {
            get { return (bool)Properties["AutoFindMailBox"].Value; }
            set { Properties["AutoFindMailBox"].Value = value; }
        }
        WoWPoint loc;
        [PbXmlAttribute()]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public GetMailAction()
        {
            //CheckNewMail
            Properties["ItemID"] = new MetaProp("ItemID", typeof(string));
            Properties["MinFreeBagSlots"] = new MetaProp("MinFreeBagSlots", typeof(uint), new DisplayNameAttribute("Min Free Bagslots"));
            Properties["CheckNewMail"] = new MetaProp("CheckNewMail", typeof(bool), new DisplayNameAttribute("Check for New Mail"));
            Properties["GetMailType"] = new MetaProp("GetMailType", typeof(GetMailActionType), new DisplayNameAttribute("Get Mail"));
            Properties["AutoFindMailBox"] = new MetaProp("AutoFindMailBox", typeof(bool), new DisplayNameAttribute("Auto find Mailbox"));
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));

            ItemID = "";
            CheckNewMail = true;
            GetMailType = (GetMailActionType)GetMailActionType.AllItems;
            AutoFindMailBox = true;
            loc = WoWPoint.Zero;
            Location = loc.ToInvariantString();
            MinFreeBagSlots = 0u;

            Properties["GetMailType"].PropertyChanged += new EventHandler<MetaPropArgs>(GetMailAction_PropertyChanged);
            Properties["AutoFindMailBox"].PropertyChanged += new EventHandler<MetaPropArgs>(AutoFindMailBoxChanged);
            Properties["ItemID"].Show = false;
            Properties["Location"].Show = false;
            Properties["Location"].PropertyChanged += new EventHandler<MetaPropArgs>(LocationChanged);
        }
        void LocationChanged(object sender, MetaPropArgs e)
        {
            MetaProp mp = (MetaProp)sender;
            loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= new EventHandler<MetaPropArgs>(LocationChanged);
            Properties["Location"].Value = string.Format("{0}, {1}, {2}", loc.X, loc.Y, loc.Z);
            Properties["Location"].PropertyChanged += new EventHandler<MetaPropArgs>(LocationChanged);
            RefreshPropertyGrid();
        }

        void AutoFindMailBoxChanged(object sender, MetaPropArgs e)
        {
            if (AutoFindMailBox)
                Properties["Location"].Show = false;
            else
                Properties["Location"].Show = true;
            RefreshPropertyGrid();
        }
        void GetMailAction_PropertyChanged(object sender, MetaPropArgs e)
        {
            if (GetMailType == GetMailActionType.AllItems)
                Properties["ItemID"].Show = false;
            else
                Properties["ItemID"].Show = true;
            RefreshPropertyGrid();
        }
        WoWGameObject mailbox;
        Stopwatch WaitForContentToShowSW = new Stopwatch();
        Stopwatch ConcludingSW = new Stopwatch();
        Stopwatch TimeoutSW = new Stopwatch();
        Stopwatch _throttleSW = new Stopwatch();
        Stopwatch _refreshInboxSW = new Stopwatch();

        List<uint> _idList;
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (!TimeoutSW.IsRunning)
                    TimeoutSW.Start();
                if (TimeoutSW.ElapsedMilliseconds > 300000)
                    IsDone = true;
                WoWPoint movetoPoint = loc;
                if (MailFrame.Instance == null || !MailFrame.Instance.IsVisible)
                {
                    if (AutoFindMailBox || movetoPoint == WoWPoint.Zero)
                    {
                        mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.Mailbox)
                            .OrderBy(o => o.Distance).FirstOrDefault();
                    }
                    else
                    {
                        mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().Where(o => o.SubType == WoWGameObjectType.Mailbox
                            && o.Location.Distance(loc) < 10)
                            .OrderBy(o => o.Distance).FirstOrDefault();
                    }
                    if (mailbox != null)
                        movetoPoint = WoWMathHelper.CalculatePointFrom(me.Location, mailbox.Location, 3);
                    if (movetoPoint == WoWPoint.Zero)
                        return RunStatus.Failure;
                    if (movetoPoint.Distance(ObjectManager.Me.Location) > 4.5)
                        Util.MoveTo(movetoPoint);
                    else if (mailbox != null)
                    {
                        mailbox.Interact();
                    }
                    return RunStatus.Success;
                }
                else
                {
                    if (_idList == null)
                    {
                        _idList = BuildItemList();
                    }
                    if (!_refreshInboxSW.IsRunning)
                        _refreshInboxSW.Start();
                    if (!WaitForContentToShowSW.IsRunning)
                        WaitForContentToShowSW.Start();
                    if (WaitForContentToShowSW.ElapsedMilliseconds < 3000)
                        return RunStatus.Success;
                    uint freeslots = ObjectManager.Me.FreeNormalBagSlots;

                    if (!ConcludingSW.IsRunning)
                    {
                        if (_refreshInboxSW.ElapsedMilliseconds < 62000)
                        {
                            if (MinFreeBagSlots > 0 && me.FreeNormalBagSlots - MinFreeBagSlots <= 4)
                            {
                                if (!_throttleSW.IsRunning)
                                    _throttleSW.Start();
                                if (_throttleSW.ElapsedMilliseconds <4000 - (me.FreeNormalBagSlots - MinFreeBagSlots)* 1000 )
                                    return RunStatus.Success;
                                else
                                {
                                    _throttleSW.Reset();
                                    _throttleSW.Start();
                                }
                            }
                            if (GetMailType == GetMailActionType.AllItems)
                            {
                                string lua = string.Format("local freeslots = 0 for bag=0,NUM_BAG_SLOTS do local fs, bagType = GetContainerNumFreeSlots(bag) if bagType == 0 then freeslots = freeslots + fs end end if freeslots <= {1} then return 1 end local numItems,totalItems = GetInboxNumItems() local foundMail=0 for index=numItems,1,-1 do local _,_,sender,subj,gold,cod,_,itemCnt,_,_,hasText=GetInboxHeaderInfo(index) if sender ~= nil and cod == 0 and itemCnt == nil and gold == 0 and hasText == nil then DeleteInboxItem(index) end if cod == 0 and ((itemCnt and itemCnt >0) or (gold and gold > 0)) then AutoLootMailItem(index) foundMail = foundMail + 1 break end end local beans = BeanCounterMail and BeanCounterMail:IsVisible() if foundMail == 0 {0}and totalItems == numItems and beans ~= 1 then return 1 else return 0 end ",
                                    CheckNewMail ? "and HasNewMail() == nil " : "", MinFreeBagSlots);
                                //freeslots / 2 >= MinFreeBagSlots ? (freeslots - MinFreeBagSlots) / 2 : 1);
                                if (Lua.GetReturnValues(lua)[0] == "1")
                                    ConcludingSW.Start();
                            }
                            else
                            {
                                for (int i = 0; i < _idList.Count; i++)
                                {
                                    string lua = string.Format("local freeslots = 0 for bag=0,NUM_BAG_SLOTS do local fs, bagType = GetContainerNumFreeSlots(bag) if bagType == 0 then freeslots = freeslots + fs end end if freeslots <= {2} then return 1 end local numItems,totalItems = GetInboxNumItems() local foundMail=0 for index=numItems,1,-1 do local _,_,sender,subj,gold,cod,_,itemCnt,_,_,hasText=GetInboxHeaderInfo(index) if sender ~= nil and cod == 0 and itemCnt == nil and gold == 0 and hasText == nil then DeleteInboxItem(index) end if cod == 0 and itemCnt and itemCnt >0  then for i2=1, ATTACHMENTS_MAX_RECEIVE do local itemlink = GetInboxItemLink(index, i2) if itemlink ~= nil and string.find(itemlink,'{0}') then foundMail = foundMail + 1 TakeInboxItem(index, i2) break end end end end if (foundMail == 0 {1})  or (foundMail == 0 and (numItems == 50 and totalItems >= 50)) then return 1 else return 0 end ",
                                        //, Entry, freeslots / 2 >= MinFreeBagSlots ? (freeslots - MinFreeBagSlots) / 2 : 1);
                                    _idList[i], CheckNewMail ? "and HasNewMail() == nil " : "", MinFreeBagSlots);

                                    if (Lua.GetReturnValues(lua)[0] == "1")
                                        _idList.RemoveAt(i);
                                }
                                if (_idList.Count == 0)
                                    ConcludingSW.Start();
                            }
                        }
                        else
                        {
                            _refreshInboxSW.Reset();
                            MailFrame.Instance.Close();
                        }
                    }
                    if (ConcludingSW.ElapsedMilliseconds > 2000)
                        IsDone = true;
                    if (IsDone)
                    {
                        Professionbuddy.Log("Mail retrieval of items:{0} finished", GetMailType);
                    }
                    else
                        return RunStatus.Success;
                }
            }
            return RunStatus.Failure;
        }

        List<uint> BuildItemList()
        {
            List<uint> list = new List<uint>();
            string[] entries = ItemID.Split(',');
            if (entries != null && entries.Length > 0)
            {
                foreach (var entry in entries)
                {
                    uint temp = 0;
                    uint.TryParse(entry.Trim(), out temp);
                    list.Add(temp);
                }
            }
            else
            {
                Professionbuddy.Err("No ItemIDs are specified");
                IsDone = true;
            }
            return list;
        }

        public override void Reset()
        {
            base.Reset();
            WaitForContentToShowSW = new Stopwatch();
            ConcludingSW = new Stopwatch();
            TimeoutSW = new Stopwatch();
            _refreshInboxSW = new Stopwatch();
            _throttleSW = new Stopwatch();
        }
        public override string Name
        {
            get
            {
                return "Get Mail";
            }
        }
        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} " + (GetMailType == GetMailActionType.Specific ? " - " +
                    ItemID.ToString() : ""), Name, GetMailType);
            }
        }
        public override string Help
        {
            get
            {
                return "This action retrieves all mail in the mailbox, or only items that match the ID.Since mailboxes are not in the NPC database you need to be within 100 yards of a mailbox to 'autofind' it";
            }
        }
        public override object Clone()
        {
            return new GetMailAction()
            {
                ItemID = this.ItemID,
                GetMailType = this.GetMailType,
                loc = this.loc,
                AutoFindMailBox = this.AutoFindMailBox,
                Location = this.Location,
                MinFreeBagSlots = this.MinFreeBagSlots,
                CheckNewMail = this.CheckNewMail,
            };
        }
    }
    #endregion
}
