//!CompilerOption:AddRef:System.Design.dll
using System;
using System.Xml;
using Styx.Helpers;
using TreeSharp;
using System.Threading;
using System.Drawing.Design;
using System.ComponentModel.Design;
using System.ComponentModel;
using HighVoltz.Dynamic;


namespace HighVoltz.Composites
{
    #region CustomAction
    class CustomAction : CsharpAction
    {
        public System.Action<object> Action { get; set; }
        [PbXmlAttribute]
        override public string Code
        {
            get { return (string)Properties["Code"].Value; }
            set { Properties["Code"].Value = value; }
        }

        public CustomAction()
            : base(CsharpCodeType.Statements)
        {
            this.Action = c => { ;};
            Properties["Code"] = new MetaProp("Code", typeof(string),
                new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)),
                new DisplayNameAttribute("Action_Common_Code"));
            Code = "";
            Properties["Code"].PropertyChanged += CustomAction_PropertyChanged;
        }

        void CustomAction_PropertyChanged(object sender, MetaPropArgs e)
        {
            DynamicCodeCompiler.CodeWasModified = true;
        }

        protected override RunStatus Run(object context)
        {
            try
            {
                if (!IsDone)
                {
                    try
                    {
                        Action(this);
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() != typeof(ThreadAbortException))
                            Professionbuddy.Err("{0}:({1})\n{1}", Pb.Strings["Action_CustomAction_Name"], Code, ex);
                    }
                    IsDone = true;
                }
                return RunStatus.Failure;
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ThreadAbortException))
                    Logging.Write(System.Drawing.Color.Red, "There was an exception while executing a CustomAction\n{0}", ex);
            }
            return RunStatus.Failure;
        }
        public override string Name { get { return Pb.Strings["Action_CustomAction_Name"]; } }
        public override string Title
        {
            get
            {
                return string.Format("{0}:({1})", Pb.Strings["Action_CustomAction_Name"], Code);
            }
        }
        public override string Help
        {
            get
            {
                return Pb.Strings["Action_CustomAction_Help"];
            }
        }
        public override object Clone()
        {
            return new CustomAction() { Code = this.Code };
        }

        public override Delegate CompiledMethod
        {
            get
            {
                return Action;
            }
            set
            {
                Action = (System.Action<object>)value;
            }
        }
    }
    #endregion
}
