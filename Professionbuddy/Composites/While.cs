//!CompilerOption:AddRef:System.Design.dll
using System.Collections.Generic;
using System.Text;
using System;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Styx.Helpers;
using TreeSharp;
using System.Diagnostics;
using PrioritySelector = TreeSharp.PrioritySelector;
namespace HighVoltz.Composites
{

    class While : If
    {
        //protected override IEnumerable<RunStatus> Execute(object context)
        //{
        //    //lock (Locker)
        //    //{
        //    if (!CanRun(null))
        //    {
        //        yield return RunStatus.Failure;
        //        yield break;
        //    }
        //    bool breakIterationEarly = false;
        //    foreach (Composite node in Children.SkipWhile(c => Selection != null ? c != Selection : false))
        //    {
        //        node.Start(context);
        //        // Keep stepping through the enumeration while it's returning RunStatus.Success
        //        // or until CanRun() returns false if IgnoreCanRun is false..
        //        while (node.Tick(context) == RunStatus.Success)
        //        {
        //            if (!IgnoreCanRun && !CanRun(context))
        //            {
        //                breakIterationEarly = true;
        //                break;
        //            }
        //            Selection = node;
        //            yield return RunStatus.Success;
        //        }
        //        if (breakIterationEarly == true)
        //            break;
        //        if (node.LastStatus == RunStatus.Success)
        //        {
        //            yield return RunStatus.Success;
        //            yield break;
        //        }
        //        else
        //            Selection = null;
        //    }
        //    Reset();
        //    if (CanRun(context))
        //    {
        //        yield return RunStatus.Success;
        //        yield break;
        //    }
        //    //}
        //}

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if ((_isRunning && IgnoreCanRun) || CanRun(context))
            {
                _isRunning = true;
                PbDecorator.EndOfWhileLoopReturn = false;
                bool shouldBreak = false;
                foreach (Composite child in Children.SkipWhile(c => Selection != null ? c != Selection : false))
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
            While w = new While()
            {
                CanRunDelegate = this.CanRunDelegate,
                Condition = this.Condition,
                IgnoreCanRun = this.IgnoreCanRun
            };
            return w;
        }
    }
}
