using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using Styx.Logic;
using Styx.Logic.Pathing;
using TreeSharp;
using Styx.Logic.BehaviorTree;
using ObjectManager = Styx.WoWInternals.ObjectManager;

namespace HighVoltz.Composites
{
    #region FlyToAction
    public sealed class FlyToAction : PBAction
    {
        [PbXmlAttribute]
        public bool Dismount
        {
            get { return (bool)Properties["Dismount"].Value; }
            set { Properties["Dismount"].Value = value; }
        }
        WoWPoint _loc;
        [PbXmlAttribute]
        public string Location
        {
            get { return (string)Properties["Location"].Value; }
            set { Properties["Location"].Value = value; }
        }
        public FlyToAction()
        {
            Properties["Dismount"] = new MetaProp("Dismount", typeof(bool), new DisplayNameAttribute("Dismount on Arrival"));
            Properties["Location"] = new MetaProp("Location", typeof(string), new EditorAttribute(typeof(PropertyBag.LocationEditor), typeof(UITypeEditor)));

            Location = _loc.ToInvariantString();
            Dismount = true;

            Properties["Location"].PropertyChanged += LocationChanged;
        }

        void LocationChanged(object sender, MetaPropArgs e)
        {
            _loc = Util.StringToWoWPoint((string)((MetaProp)sender).Value);
            Properties["Location"].PropertyChanged -= LocationChanged;
            Properties["Location"].Value = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", _loc.X, _loc.Y, _loc.Z);
            Properties["Location"].PropertyChanged += LocationChanged;
            RefreshPropertyGrid();
        }

        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (ObjectManager.Me.Location.Distance(_loc) > 6)
                {
                    Flightor.MoveTo(_loc);
                    TreeRoot.StatusText = string.Format("Flying to location {0}", _loc);
                }
                else
                {
                    if (Dismount)
                        Mount.Dismount("Dismounting flying mount");
                    //Lua.DoString("Dismount() CancelShapeshiftForm()");
                    IsDone = true;
                    TreeRoot.StatusText = string.Format("Arrived at location {0}", _loc);
                }
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }

        public override string Name { get { return "Fly To"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} ", Name, Location);
            }
        }
        public override string Help
        {
            get
            {
                return "This action relies on Flightor, the 3d navigator used by Gatherbuddy2. This action will fly your character to the selected location and dismount on arrival if Dismount is set to true.Be sure to make the target location outdoors and not underneath any obsticles.";
            }
        }
        public override object Clone()
        {
            return new FlyToAction { Location = this.Location, Dismount = this.Dismount };
        }
    }
    #endregion
}
