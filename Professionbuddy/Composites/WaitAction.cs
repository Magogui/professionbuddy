//!CompilerOption:AddRef:System.Design.dll
using System.Diagnostics;
using System.Xml;
using System;
using TreeSharp;
using System.Threading;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;

namespace HighVoltz.Composites
{
    #region WaitAction
    public class WaitAction : CsharpAction
    {
        virtual public CanRunDecoratorDelegate CanRunDelegate { get; set; }
        [PbXmlAttribute()]
        public string Condition
        {
            get { return (string)Properties["Condition"].Value; }
            set { Properties["Condition"].Value = value; }
        }
        [PbXmlAttribute()]
        public int Timeout
        {
            get { return (int)Properties["Timeout"].Value; }
            set { Properties["Timeout"].Value = value; }
        }
        public WaitAction()
            : base(CsharpCodeType.BoolExpression)
        {
            Properties["Timeout"] = new MetaProp("Timeout", typeof(int));
            Properties["Condition"] = new MetaProp("Condition", typeof(string), new DisplayNameAttribute("Until Condition"), 
                new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)));

            Timeout = 2000;
            Condition = "false";
            CanRunDelegate = u => false;
        }
        Stopwatch timeout = new Stopwatch();
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (!timeout.IsRunning)
                    timeout.Start();
                try
                {
                    if (timeout.ElapsedMilliseconds >= Timeout || CanRunDelegate(null))
                    {
                        timeout.Stop();
                        timeout.Reset();
                        Professionbuddy.Debug("Wait Until {0} Completed",Condition);
                        IsDone = true;
                    }
                    else
                        return RunStatus.Success;
                }
                catch (Exception ex)
                {
                    if (ex.GetType() != typeof(ThreadAbortException))
                        Professionbuddy.Err("Wait:({0})\n{1}", Condition, ex);
                }
            }
            return RunStatus.Failure;
        }
        public override string Name { get { return "Wait until Condition"; } }
        public override string Title
        {
            get
            {
                return string.Format("Wait ({0}) timeout:{1}", Condition, Timeout);
            }
        }
        public override string Help
        {
            get
            {
                return "This action will wait for an amount of time or until condition becomes true before moving to next action. Timeout is in milliseconds";
            }
        }
        public override object Clone()
        {
            return new WaitAction() { Condition = this.Condition, Timeout = this.Timeout };
        }
        public override string Code
        {
            get
            {
                return Condition;
            }
        }

        public override Delegate CompiledMethod
        {
            get
            {
                return CanRunDelegate;
            }
            set
            {
                CanRunDelegate = (CanRunDecoratorDelegate)value;
            }
        }
    }
    #endregion
}
