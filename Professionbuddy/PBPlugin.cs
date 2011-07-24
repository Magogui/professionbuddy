//!CompilerOption:Optimize:On
//!CompilerOption:AddRef:WindowsBase.dll
// Professionbuddy plugin by HighVoltz

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.IO.Packaging;

using Styx;
using TreeSharp;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.Logic;
using Styx.Logic.Combat;
using System.Diagnostics;
using Styx.Patchables;
using Styx.Plugins;
using Styx.Plugins.PluginClass;
using Styx.Logic.Pathing;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using Styx.Combat.CombatRoutine;
using Styx.Logic.POI;
using HighVoltz.Composites;
using System.Reflection;
using Styx.Logic.Profiles;
using System.Collections.Specialized;
using Action = TreeSharp.Action;
using ObjectManager = Styx.WoWInternals.ObjectManager;

namespace HighVoltz
{
    // this class is mainly used to use virual methods to import svn revision using keyword substitution
    public class PBPlugin : HBPlugin
    {
        int _rev = -1;

        protected virtual string RevString { get { return "0"; } }
        public int Revision
        {
            get
            {
                if (_rev == -1)
                    int.TryParse(RevString, out _rev);
                return _rev;
            }
        }

        public PBPlugin()
        {
        }

        public override string Name { get { return ""; } }

        public override string Author { get { return "HighVoltz"; } }

        public override Version Version { get { return new Version(1, Revision); } }

        public override bool WantButton { get { return true; } }

        public override string ButtonText { get { return Name; } }

        public override void Pulse()
        {
        }

        public override void Initialize()
        {
        }
    }
}
