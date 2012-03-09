//!CompilerOption:AddRef:System.Design.dll
using System.Diagnostics;
using System;
using TreeSharp;
using System.Threading;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using HighVoltz.Dynamic;

namespace HighVoltz.Composites
{
    public sealed class WaitAction : CsharpAction
    {
        public CanRunDecoratorDelegate CanRunDelegate { get; set; }
        [PbXmlAttribute]
        public string Condition
        {
            get { return (string)Properties["Condition"].Value; }
            set { Properties["Condition"].Value = value; }
        }
        [PbXmlAttribute]
        public int Timeout
        {
            get { return (int)Properties["Timeout"].Value; }
            set { Properties["Timeout"].Value = value; }
        }
        public WaitAction()
            : base(CsharpCodeType.BoolExpression)
        {
            Properties["Timeout"] = new MetaProp("Timeout", typeof(int),
                new DisplayNameAttribute(Pb.Strings["Action_Common_Timeout"]));

            Properties["Condition"] = new MetaProp("Condition", typeof(string), 
                new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)),
                 new DisplayNameAttribute(Pb.Strings["Action_WaitAction_Condition"]));

            Timeout = 2000;
            Condition = "false";
            CanRunDelegate = u => false;
        }

        readonly Stopwatch _timeout = new Stopwatch();
        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                if (!_timeout.IsRunning)
                    _timeout.Start();
                try
                {
                    if (_timeout.ElapsedMilliseconds >= Timeout || CanRunDelegate(null))
                    {
                        _timeout.Stop();
                        _timeout.Reset();
                        Professionbuddy.Debug("Wait Until {0} Completed",Condition);
                        IsDone = true;
                    }
                    else
                        return RunStatus.Success;
                }
                catch (Exception ex)
                {
                    if (ex.GetType() != typeof(ThreadAbortException))
                        Professionbuddy.Err("{0}:({1})\n{2}", Pb.Strings["Action_WaitAction_Name"], Condition, ex);
                }
            }
            return RunStatus.Failure;
        }
        public override string Name { get { return Pb.Strings["Action_WaitAction_LongName"]; } }

        public override string Title
        {
            get
            {
                return string.Format("{0} ({1}) {2}:{3}",
                    Pb.Strings["Action_WaitAction_Name"], Condition, Pb.Strings["Action_Common_Timeout"], Timeout);
            }
        }
        public override string Help
        {
            get
            {
                return Pb.Strings["Action_WaitAction_Help"];
            }
        }
        public override object Clone()
        {
            return new WaitAction { Condition = this.Condition, Timeout = this.Timeout };
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
}
