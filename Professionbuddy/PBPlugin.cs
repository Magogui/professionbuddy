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
    public class SvnBase
    {
        int _rev = -1;

        protected virtual string RevString { get { return "0"; } }
        public int Revision
        {
            get
            {
                if (_rev == -1)
                    int.TryParse(RevString, out _rev);
                return _rev + 1;
            }
        }
    }

    public partial class Svn : SvnBase
    {

    }
   
}
