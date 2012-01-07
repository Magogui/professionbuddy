using System.Collections.Generic;
using System.Linq;
using TreeSharp;
using System.Xml.Serialization;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx;
using Styx.Logic;

namespace HighVoltz.Composites
{
    [XmlRoot("Professionbuddy")]
    public class PbDecorator : PrioritySelector
    {
        public PbDecorator(params Composite[] children) : base(children) { }

        bool CanRun
        {
            get
            {
                return StyxWoW.IsInWorld && !ExitBehavior() && Professionbuddy.Instance.IsRunning;
            }
        }

        static LocalPlayer Me { get { return ObjectManager.Me; } }

        public static bool EndOfWhileLoopReturn;
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (CanRun)
            {
                bool shouldBreak = false;
                foreach (Composite child in Children.SkipWhile(c => Selection != null && c != Selection))
                {
                    child.Start(context);
                    Selection = child;
                    while (child.Tick(context) == RunStatus.Running)
                    {
                        if (!CanRun)
                        {
                            shouldBreak = true;
                            break;
                        }
                        yield return RunStatus.Running;
                    }
                    if (shouldBreak)
                        break;
                    if (EndOfWhileLoopReturn)
                        yield return RunStatus.Failure;
                    if (child.LastStatus == RunStatus.Success)
                        yield return RunStatus.Success;
                }
                Selection = null;
            }
            yield return RunStatus.Failure;
        }

        public void Reset()
        {
            EndOfWhileLoopReturn = false;
            Selection = null;
            foreach (IPBComposite comp in Children)
            {
                comp.Reset();
            }
        }
        public static bool ExitBehavior()
        {
            return ((Me.IsActuallyInCombat && !Me.Mounted) ||
                (Me.IsActuallyInCombat && Me.Mounted && !Me.IsFlying && Mount.ShouldDismount(Util.GetMoveToDestination()))) ||
                !Me.IsAlive || Me.HealthPercent <= 40;
        }

    }
}
