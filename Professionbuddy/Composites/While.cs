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
                PbDecorator.EndOfWhileLoopReturn = false;
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

        override public string Name { get { return "While Condition"; } }
        override public string Title
        {
            get
            {
                return string.Format("While {0}",
                    string.IsNullOrEmpty(Condition) ? "Condition" : "(" + Condition + ")");
            }
        }
        override public string Help { get { return "'While Condition' will execute the actions it contains if the specified condition is true. 'Ignore Condition until done' basically will ignore the Condition if any of the actions it contains is running. The difference between this and the 'If Condition' is that this will auto reset all actions within it and all nested 'If/While' Conditions"; } }
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
