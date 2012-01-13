using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.Trainer;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Styx.Helpers;
using ObjectManager = Styx.WoWInternals.ObjectManager;

namespace HighVoltz.Composites
{
    #region TrainSkillAction

    sealed class TrainSkillAction : PBAction
    {
        [PbXmlAttribute]
        public uint NpcEntry
        {
            get { return (uint)Properties["NpcEntry"].Value; }
            set { Properties["NpcEntry"].Value = value; }
        }
        WoWPoint _loc;
        [PbXmlAttribute]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public TrainSkillAction()
        {
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));
            Properties["NpcEntry"] = new MetaProp("NpcEntry", typeof(uint), new EditorAttribute(typeof(PropertyBag.EntryEditor), typeof(UITypeEditor)));
            _loc = WoWPoint.Zero;
            Location = _loc.ToInvariantString();
            NpcEntry = 0u;

            Properties["Location"].PropertyChanged += LocationChanged;
        }
        void LocationChanged(object sender, MetaPropArgs e)
        {
            _loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= LocationChanged;
            Properties["Location"].Value = string.Format("{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
            Properties["Location"].PropertyChanged += LocationChanged;
            RefreshPropertyGrid();
        }
        Stopwatch _concludingStopWatch = new Stopwatch();
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (TrainerFrame.Instance == null || !TrainerFrame.Instance.IsVisible || !ObjectManager.Me.GotTarget ||
                    (ObjectManager.Me.GotTarget && ObjectManager.Me.CurrentTarget.Entry != NpcEntry))
                {
                    WoWPoint movetoPoint = _loc;
                    WoWUnit unit = ObjectManager.GetObjectsOfType<WoWUnit>().Where(o => o.Entry == NpcEntry).
                        OrderBy(o => o.Distance).FirstOrDefault();
                    if (unit != null)
                        movetoPoint = WoWMathHelper.CalculatePointFrom(Me.Location, unit.Location, 3);
                    else if (movetoPoint == WoWPoint.Zero)
                        movetoPoint = MoveToAction.GetLocationFromDB(MoveToAction.MoveToType.NpcByID, NpcEntry);
                    if (movetoPoint != WoWPoint.Zero && ObjectManager.Me.Location.Distance(movetoPoint) > 4.5)
                    {
                        Util.MoveTo(movetoPoint);
                    }
                    else if (unit != null)
                    {
                        if (Me.IsMoving)
                            WoWMovement.MoveStop();
                        unit.Target();
                        unit.Interact();
                    }
                    if (GossipFrame.Instance != null && GossipFrame.Instance.IsVisible &&
                        GossipFrame.Instance.GossipOptionEntries != null)
                    {
                        foreach (GossipEntry ge in GossipFrame.Instance.GossipOptionEntries)
                        {
                            if (ge.Type == GossipEntry.GossipEntryType.Trainer)
                            {
                                GossipFrame.Instance.SelectGossipOption(ge.Index);
                                return RunStatus.Success;
                            }
                        }
                    }
                    return RunStatus.Success;
                }
                if (!_concludingStopWatch.IsRunning)
                {
                    Lua.DoString("SetTrainerServiceTypeFilter('available', 1) BuyTrainerService(0) ");
                    _concludingStopWatch.Start();
                }
                else if (_concludingStopWatch.ElapsedMilliseconds >= 3000)
                {
                    _concludingStopWatch.Reset();
                    Professionbuddy.Log("Training Completed ");
                    IsDone = true;
                }
            }
            return RunStatus.Failure;
        }
        public override string Name
        {
            get { return string.Format("Train Skill"); }
        }
        public override string Title
        {
            get { return string.Format("{0}: {1}", Name, NpcEntry); }
        }
        public override string Help
        {
            get
            {
                return "This action will go to the trainer and train all available spells. Location can be left blank if the NPC is in the database.";
            }
        }
        public override object Clone()
        {
            return new TrainSkillAction { NpcEntry = this.NpcEntry, _loc = this._loc, Location = this.Location };
        }
    }
    #endregion
}
