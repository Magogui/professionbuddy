using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TreeSharp;
using System.Xml;
using System.Xml.Serialization;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx;
using Styx.Logic;
using System.Diagnostics;
using Styx.Helpers;
using System.ComponentModel;

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
        static LocalPlayer Me = ObjectManager.Me;
        //Stopwatch _profilerSW = new Stopwatch();

        //public override RunStatus Tick(object context)
        //{
        //    if (CanRun)
        //    {
        //        try
        //        {
        //            if (!base.IsRunning)
        //                base.Start(context);
        //            // _profilerSW.Reset(); _profilerSW.Start();
        //            LastStatus = base.Tick(context) == RunStatus.Success ? RunStatus.Success : RunStatus.Failure;
        //            //if (LastStatus == RunStatus.Success)
        //            //    Logging.Write("PbBehavior execution: {0}. PB behavior is running", _profilerSW.ElapsedMilliseconds);
        //            //else
        //            //    Logging.Write("PbBehavior execution: {0}. SecondaryBot is running", _profilerSW.ElapsedMilliseconds);
        //        }
        //        catch
        //        {
        //            LastStatus = RunStatus.Failure;
        //        }
        //    }
        //    else
        //        LastStatus = RunStatus.Failure;
        //    return (RunStatus)LastStatus;
        //}

        public static bool EndOfWhileLoopReturn = false;
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (CanRun)
            {
                bool shouldBreak = false;
                foreach (Composite child in Children.SkipWhile(c => Selection != null ? c != Selection : false))
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
