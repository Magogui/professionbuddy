//!CompilerOption:AddRef:System.Design.dll
using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using TreeSharp;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;
using System.ComponentModel.Design;
using System.Drawing.Design;
using HighVoltz.Dynamic;


namespace HighVoltz.Composites
{

    public class If : GroupComposite, ICSharpCode, IPBComposite
    {
        #region Properties
        [XmlIgnore]
        virtual public PropertyBag Properties { get; private set; }
        protected PropertyGrid PropertyGrid { get { return MainForm.IsValid ? MainForm.Instance.ActionGrid : null; } }
        protected void RefreshPropertyGrid()
        {
            if (PropertyGrid != null)
            {
                PropertyGrid.Refresh();
            }
        }
        #endregion
        protected readonly static object LockObject = new object();
        virtual public CanRunDecoratorDelegate CanRunDelegate { get; set; }
        [PbXmlAttribute]
        virtual public string Condition
        {
            get { return (string)Properties["Condition"].Value; }
            set { Properties["Condition"].Value = value; }
        }
        [PbXmlAttribute]
        virtual public bool IgnoreCanRun
        {
            get { return (bool)Properties["IgnoreCanRun"].Value; }
            set { Properties["IgnoreCanRun"].Value = value; }
        }

        public If()
        {
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            Properties = new PropertyBag();
            Properties["IgnoreCanRun"] = new MetaProp("IgnoreCanRun", typeof(bool),
                new DisplayNameAttribute(Professionbuddy.Instance.Strings["FlowControl_If_IgnoreCanRun"]));

            Properties["Condition"] = new MetaProp("Condition",
                typeof(string), new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)),
                new DisplayNameAttribute(Professionbuddy.Instance.Strings["FlowControl_If_Condition"]));

            Properties["CompileError"] = new MetaProp("CompileError", typeof(string), new ReadOnlyAttribute(true),
                new DisplayNameAttribute(Professionbuddy.Instance.Strings["Action_CSharpAction_CompileError"]));

            CanRunDelegate = c => false;
            Condition = "";
            CompileError = "";
            Properties["CompileError"].Show = false;

            Properties["Condition"].PropertyChanged += Condition_PropertyChanged;
            Properties["CompileError"].PropertyChanged += CompileErrorPropertyChanged;
            IgnoreCanRun = true;
            // ReSharper restore DoNotCallOverridableMethodsInConstructor
        }

        void Condition_PropertyChanged(object sender, EventArgs e)
        {
            DynamicCodeCompiler.CodeWasModified = true;
        }

        string _lastError = "";
        void CompileErrorPropertyChanged(object sender, EventArgs e)
        {
            if (CompileError != "" || (CompileError == "" && _lastError != ""))
                MainForm.Instance.RefreshActionTree(this);
            Properties["CompileError"].Show = CompileError != "";
            RefreshPropertyGrid();
            _lastError = CompileError;
        }

        protected virtual bool CanRun(object context)
        {
            try
            {
                return CanRunDelegate(context);
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ThreadAbortException))
                    Professionbuddy.Err("{0}: {1}\nErr:{2}", Professionbuddy.Instance.Strings["FlowControl_If_LongName"], Condition, ex);
                return false;
            }
        }

        // ReSharper disable InconsistentNaming
        protected bool _isRunning = false;
        // ReSharper restore InconsistentNaming
        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (!IsDone && ((_isRunning && IgnoreCanRun) || CanRun(context)))
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
                Selection = null;
                IsDone = true;
                _isRunning = false;
            }
            yield return RunStatus.Failure;
        }

        public virtual Delegate CompiledMethod
        {
            get { return CanRunDelegate; }
            set { CanRunDelegate = (CanRunDecoratorDelegate)value; }
        }

        public virtual string Code
        {
            get { return Condition; }
            set { Condition = value; }
        }

        public int CodeLineNumber { get; set; }

        public string CompileError
        {
            get { return (string)Properties["CompileError"].Value; }
            set { Properties["CompileError"].Value = value; }
        }

        public CsharpCodeType CodeType { get { return CsharpCodeType.BoolExpression; } }

        virtual public void Reset()
        {
            _isRunning = IsDone = false;
            Selection = null;
            recursiveReset(this);
        }
        void recursiveReset(If gc)
        {
            foreach (IPBComposite comp in gc.Children)
            {
                comp.Reset();
                if (comp is If)
                    recursiveReset(comp as If);
            }
        }
        /// <summary>
        /// Returns true if the If Condition is finished executing its children or condition isn't met.
        /// </summary>
        virtual public bool IsDone { get; set; }

        virtual public System.Drawing.Color Color
        {
            get { return string.IsNullOrEmpty(CompileError) ? System.Drawing.Color.Blue : System.Drawing.Color.Red; }
        }

        virtual public string Name { get { return Professionbuddy.Instance.Strings["FlowControl_If_LongName"]; } }
        virtual public string Title
        {
            get
            {
                return string.IsNullOrEmpty(Condition) ?
                    Professionbuddy.Instance.Strings["FlowControl_If_LongName"]:
                    (Professionbuddy.Instance.Strings["FlowControl_If_Name"] + " (" + Condition + ")");
            }
        }


        public virtual object Clone()
        {
            var pd = new If
                         {
                             CanRunDelegate = this.CanRunDelegate,
                             Condition = this.Condition,
                             IgnoreCanRun = this.IgnoreCanRun
                         };
            return pd;
        }

        virtual public string Help { get { return Professionbuddy.Instance.Strings["FlowControl_If_Help"]; } }

        public void OnProfileLoad(System.Xml.Linq.XElement element) { }

        public void OnProfileSave(System.Xml.Linq.XElement element) { }

        public IPBComposite AttachedComposite { get { return this; } }
    }
}
