using System;
using System.Collections.Generic;
using System.Drawing.Design;
using System.Linq;
using System.Xml;
using Styx.WoWInternals;
using Styx.Logic.Pathing;
using Styx;
using Styx.Database;
using System.ComponentModel;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.Merchant;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using Styx.Logic.BehaviorTree;
using System.Threading;

namespace HighVoltz.Composites
{
    #region SellItemAction
    class SellItemAction : PBAction
    {
        [PbXmlAttribute()]
        public uint NpcEntry
        {
            get { return (uint)Properties["NpcEntry"].Value; }
            set { Properties["NpcEntry"].Value = value; }
        }
        WoWPoint loc;
        [PbXmlAttribute()]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public enum SellItemActionType
        {
            Specific,
            List,
            Greys,
            Whites,
            Greens,
        }
        [PbXmlAttribute()]
        public SellItemActionType SellItemType
        {
            get { return (SellItemActionType)Properties["SellItemType"].Value; }
            set { Properties["SellItemType"].Value = value; }
        }
        [PbXmlAttribute()]
        public string ItemID
        {
            get { return (string)Properties["ItemID"].Value; }
            set { Properties["ItemID"].Value = value; }
        }
        [PbXmlAttribute()]
        public uint Count
        {
            get { return (uint)Properties["Count"].Value; }
            set { Properties["Count"].Value = value; }
        }
        public SellItemAction()
        {
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));
            Properties["NpcEntry"] = new MetaProp("NpcEntry", typeof(uint), new EditorAttribute(typeof(PropertyBag.EntryEditor), typeof(UITypeEditor)));
            Properties["ItemID"] = new MetaProp("ItemID", typeof(string));
            Properties["Count"] = new MetaProp("Count", typeof(uint));
            Properties["SellItemType"] = new MetaProp("SellItemType", typeof(SellItemActionType), new DisplayNameAttribute("Sell Item Type"));

            ItemID = "";
            Count = 0u;
            loc = WoWPoint.Zero;
            Location = loc.ToInvariantString();
            NpcEntry = 0u;

            Properties["Location"].PropertyChanged += new EventHandler(LocationChanged);
            Properties["SellItemType"].Value = SellItemActionType.Specific;
            Properties["SellItemType"].PropertyChanged += new EventHandler(SellItemAction_PropertyChanged);
        }
        void LocationChanged(object sender, EventArgs e)
        {
            MetaProp mp = (MetaProp)sender;
            loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= new EventHandler(LocationChanged);
            Properties["Location"].Value = string.Format("{0}, {1}, {2}", loc.X, loc.Y, loc.Z);
            Properties["Location"].PropertyChanged += new EventHandler(LocationChanged);
            RefreshPropertyGrid();
        }

        void SellItemAction_PropertyChanged(object sender, EventArgs e)
        {
            switch (SellItemType)
            {
                case SellItemActionType.Specific:
                    Properties["Count"].Show = true;
                    Properties["ItemID"].Show = true;
                    break;
                default:
                    Properties["Count"].Show = false;
                    Properties["ItemID"].Show = false;
                    break;
            }
            RefreshPropertyGrid();
        }

        uint _entry = 0;
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (MerchantFrame.Instance == null || !MerchantFrame.Instance.IsVisible)
                {
                    WoWPoint movetoPoint = loc;
                    WoWUnit unit = null;
                    if (_entry == 0)
                        _entry = NpcEntry;
                    if (_entry == 0)
                    {
                        MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NearestVendor, 0);
                        var npcResults = NpcQueries.GetNearestNpc(ObjectManager.Me.FactionTemplate, ObjectManager.Me.MapId,
                                                ObjectManager.Me.Location, UnitNPCFlags.Vendor);
                        _entry = (uint)npcResults.Entry;
                        movetoPoint = npcResults.Location;
                    }
                    unit = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == _entry).
                        OrderBy(o => o.Distance).FirstOrDefault();
                    if (unit != null)
                        movetoPoint = unit.Location;
                    else if (movetoPoint == WoWPoint.Zero)
                        movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NpcByID, NpcEntry);
                    if (movetoPoint != WoWPoint.Zero && ObjectManager.Me.Location.Distance(movetoPoint) > 4.5)
                    {
                        Util.MoveTo(movetoPoint);
                    }
                    else if (unit != null)
                    {
                        unit.Target();
                        unit.Interact();
                    }

                    if (GossipFrame.Instance != null && GossipFrame.Instance.IsVisible &&
                        GossipFrame.Instance.GossipOptionEntries != null)
                    {
                        foreach (GossipEntry ge in GossipFrame.Instance.GossipOptionEntries)
                        {
                            if (ge.Type == GossipEntry.GossipEntryType.Vendor)
                            {
                                GossipFrame.Instance.SelectGossipOption(ge.Index);
                                return RunStatus.Success;
                            }
                        }
                    }
                }
                else
                {
                    if (SellItemType == SellItemActionType.Specific)
                    {
                        List<uint> idList = new List<uint>();
                        string[] entries = ItemID.Split(',');
                        if (entries != null && entries.Length > 0)
                        {
                            foreach (var entry in entries)
                            {
                                uint temp = 0;
                                uint.TryParse(entry.Trim(), out temp);
                                idList.Add(temp);
                            }
                        }
                        else
                        {
                            Professionbuddy.Err("No ItemIDs are specified");
                            IsDone = true;
                            return RunStatus.Failure;
                        }
                        List<WoWItem> itemList = ObjectManager.Me.BagItems.Where(u => idList.Contains(u.Entry)).
                            Take((int)Count <= 0 ? int.MaxValue : (int)Count).ToList();
                        if (itemList != null)
                        {
                            using (new FrameLock())
                            {
                                foreach (WoWItem item in itemList)
                                    item.UseContainerItem();
                            }
                        }
                    }
                    else
                    {
                        List<WoWItem> itemList = null;
                        IEnumerable<WoWItem> itemQuery = from item in me.BagItems
                                                         where !Pb.ProtectedItems.Contains(item.Entry)
                                                         select item;
                        switch (SellItemType)
                        {
                            case SellItemActionType.Greys:
                                itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Poor).ToList();
                                break;
                            case SellItemActionType.Whites:
                                itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Common).ToList();
                                break;
                            case SellItemActionType.Greens:
                                itemList = itemQuery.Where(i => i.Quality == WoWItemQuality.Uncommon).ToList();
                                break;
                        }
                        if (itemList != null)
                        {
                            using (new FrameLock())
                            {
                                foreach (WoWItem item in itemList)
                                {
                                    item.UseContainerItem();
                                }
                            }
                        }
                    }
                    Professionbuddy.Log("SellItemAction Completed for {0}", ItemID);
                    IsDone = true;
                }
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }

        public override void Reset()
        {
            base.Reset();
            _entry = 0;
        }
        public override string Name
        {
            get { return "Sell Item"; }
        }
        public override string Title
        {
            get
            {
                return string.Format("({0}) " +
                  (SellItemType == SellItemActionType.Specific ? ItemID.ToString() + " x{1} " : SellItemType.ToString()), Name, Count);
            }
        }
        public override string Help
        {
            get
            {
                return "This action will sell items to a merchant. It will either sell a specified item x Count stacks, or sell all grey/white/green quality items. This action will find the closest vendor if NpcEntry is set to 0. ItemID can be a comma separated list of Items IDs";
            }
        }
        public override object Clone()
        {
            return new SellItemAction() { Count = this.Count, ItemID = this.ItemID, SellItemType = this.SellItemType, NpcEntry = this.NpcEntry, Location = this.Location };
        }
    }
    #endregion
}
