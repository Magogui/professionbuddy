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
        //    while (CanRun(context))
        //    {
        //        foreach (Composite node in Children)
        //        {
        //            node.Start(context);
        //            while (node.Tick(context) == RunStatus.Running &&
        //                (IgnoreCanRun || (!IgnoreCanRun && CanRun(context))))
        //            {
        //                Selection = node;
        //                yield return RunStatus.Running;
        //            }
        //            Selection = null;
        //        }
        //        Reset();
        //        while (Professionbuddy.Instance.SecondaryBot.Root.Tick(null) == RunStatus.Running)
        //            yield return RunStatus.Running;
        //    }
        //    yield return RunStatus.Failure;
        //    yield break;
        //}
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            //lock (Locker)
            //{
            if (!CanRun(null))
            {
                yield return RunStatus.Failure;
                yield break;
            }
            foreach (Composite node in Children)
            {
                node.Start(context);
                // Keep stepping through the enumeration while it's returning RunStatus.Running
                // or until CanRun() returns false if IgnoreCanRun is false..
                while ((IgnoreCanRun || (CanRun(context) && !IgnoreCanRun)) &&
                    node.Tick(context) == RunStatus.Running)
                {
                    Selection = node;
                    yield return RunStatus.Running;
                }
                Selection = null;
                if (node.LastStatus == RunStatus.Success)
                {
                    yield return RunStatus.Success;
                    yield break;
                }
            }
            Reset();
            if (!CanRun(context))
            {
                yield return RunStatus.Failure;
                yield break;
            }
            else
            {
                yield return RunStatus.Running;
            }
            //}
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
