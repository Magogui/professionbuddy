﻿using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using Styx.Helpers;
using Styx.Logic.Inventory.Frames.Gossip;
using Styx.Logic.Inventory.Frames.Trainer;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;

namespace HighVoltz.Composites
{

    #region TrainSkillAction

    internal sealed class TrainSkillAction : PBAction
    {
        private readonly Stopwatch _concludingStopWatch = new Stopwatch();
        private readonly Stopwatch _waitBeforeTrainingStopWatch = new Stopwatch();
        private WoWPoint _loc;

        public TrainSkillAction()
        {
            Properties["Location"] = new MetaProp("Location", typeof(string),
                new EditorAttribute(typeof(PropertyBag.LocationEditor),typeof(UITypeEditor)),
                new DisplayNameAttribute(Pb.Strings["Action_Common_Location"]));

            Properties["NpcEntry"] = new MetaProp("NpcEntry", typeof(uint),
                new EditorAttribute(typeof(PropertyBag.EntryEditor),typeof(UITypeEditor)),
                new DisplayNameAttribute(Pb.Strings["Action_Common_NpcEntry"]));

            _loc = WoWPoint.Zero;
            Location = _loc.ToInvariantString();
            NpcEntry = 0u;

            Properties["Location"].PropertyChanged += LocationChanged;
        }

        [PbXmlAttribute]
        public uint NpcEntry
        {
            get { return (uint)Properties["NpcEntry"].Value; }
            set { Properties["NpcEntry"].Value = value; }
        }

        [PbXmlAttribute]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }

        public override string Name
        {
            get { return Pb.Strings["Action_TrainSkillAction_Name"] ; }
        }

        public override string Title
        {
            get { return string.Format("{0}: {1}", Name, NpcEntry); }
        }

        public override string Help
        {
            get
            {
                return Pb.Strings["Action_TrainSkillAction_Help"];
            }
        }

        private void LocationChanged(object sender, MetaPropArgs e)
        {
            _loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= LocationChanged;
            Properties["Location"].Value = string.Format("{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
            Properties["Location"].PropertyChanged += LocationChanged;
            RefreshPropertyGrid();
        }

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
                // wait 2 seconds before training
                if (!_waitBeforeTrainingStopWatch.IsRunning)
                    _waitBeforeTrainingStopWatch.Start();
                if (_waitBeforeTrainingStopWatch.ElapsedMilliseconds < 2000)
                    return RunStatus.Success;
                if (!_concludingStopWatch.IsRunning)
                {
                    Lua.DoString("SetTrainerServiceTypeFilter('available', 1)");
                    _concludingStopWatch.Start();
                } 
                // wait 3 seconds after training.
                else if (_concludingStopWatch.ElapsedMilliseconds >= 2000)
                {
                    _waitBeforeTrainingStopWatch.Reset();
                    _concludingStopWatch.Reset();
                    //Lua.DoString("BuyTrainerService(0) ");
                    Lua.DoString("for i=GetNumTrainerServices(),1,-1 do if select(3,GetTrainerServiceInfo(i)) == 'available' then BuyTrainerService(i) end end");
                    Professionbuddy.Log("Training Completed ");
                    IsDone = true;
                }
            }
            return RunStatus.Failure;
        }

        public override object Clone()
        {
            return new TrainSkillAction { NpcEntry = NpcEntry, _loc = _loc, Location = Location };
        }
    }

    #endregion
}