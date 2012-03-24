//!CompilerOption:AddRef:System.Design.dll
using System.Collections.Generic;
using System.Linq;
using TreeSharp;

namespace HighVoltz.Composites
{

    class While : If
    {
     
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if ((_isRunning && IgnoreCanRun) || CanRun(context))
            {
                _isRunning = true;
                bool shouldBreak = false;
                foreach (Composite child in Children.SkipWhile(c => Selection != null && c != Selection))
                {
                    child.Start(context);
                    Selection = child;
                    while (child.Tick(context) == RunStatus.Running)
                    {
                        if (!IgnoreCanRun && !CanRun(context))
                        {
                            shouldBreak = true;
                            break;
                        }
                        yield return RunStatus.Running;
                    }
                    if (shouldBreak)
                        break;
                    if (child.LastStatus == RunStatus.Success)
                        yield return RunStatus.Success;
                }
                Reset();
                Selection = null;
                if (!shouldBreak && CanRun(context))
                {
                    PbDecorator.EndOfWhileLoopReturn = true;
                    yield return RunStatus.Success;
                }
                _isRunning = false;
            }
            yield return RunStatus.Failure;
        }

        override public string Name { get { return Professionbuddy.Instance.Strings["FlowControl_While_LongName"]; } }

        override public string Title
        {
            get
            {
                return string.IsNullOrEmpty(Condition) ?
                    Professionbuddy.Instance.Strings["FlowControl_While_LongName"] :
                    (Professionbuddy.Instance.Strings["FlowControl_While_Name"] + " (" + Condition + ")");
            }
        }
        override public string Help { get { return Professionbuddy.Instance.Strings["FlowControl_While_Help"]; } }
        public override object Clone()
        {
            var w = new While
                        {
                CanRunDelegate = this.CanRunDelegate,
                Condition = this.Condition,
                IgnoreCanRun = this.IgnoreCanRun
            };
            return w;
        }
    }
}
