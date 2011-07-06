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
namespace HighVoltz.Composites {
    class While : If {
        public override RunStatus Tick(object context) {
            bool canRun = CanRun(null);
            if ((LastStatus == RunStatus.Running && IgnoreCanRun) || canRun) {
                if (!DecoratedChild.IsRunning)
                    DecoratedChild.Start(null);
                LastStatus = DecoratedChild.Tick(null);
                if (IsDone) {
                    Reset();
                }
                else
                    return RunStatus.Running;
            }
            return RunStatus.Failure;
        }

        override public string Name { get { return "While Condition"; } }
        override public string Title {
            get {
                return string.Format("While {0}",
                    string.IsNullOrEmpty(Condition) ? "Condition" : "(" + Condition + ")");
            }
        }
        override public string Help { get { return "'While Condition' will execute the actions it contains if the specified condition is true. 'Ignore Condition until done' basically will ignore the Condition if any of the actions it contains is running. The difference between this and the 'If Condition' is that this will auto reset all actions within it and all nested 'If/While' Conditions"; } }
        public override object Clone() {
            While w = new While() {
                CanRunDelegate = this.CanRunDelegate,
                Condition = this.Condition,
                IgnoreCanRun = this.IgnoreCanRun
            };
            return w;
        }
    }
}
