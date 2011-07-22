using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using TreeSharp;
using System.Windows.Forms;
using System.ComponentModel;

namespace HighVoltz.Composites
{
    public abstract class CsharpCondition : Decorator, ICSharpCode, IPBComposite
    {
        virtual public CanRunDecoratorDelegate CanRunDelegate { get; set; }
        virtual public string Condition
        {
            get { return (string)Properties["Condition"].Value; }
            set { Properties["Condition"].Value = value; }
        }
        public CsharpCondition()
        {
            CodeType = CsharpCodeType.BoolExpression;
        }
        protected PropertyGrid propertyGrid { get { return MainForm.IsValid ? MainForm.Instance.ActionGrid : null; } }
        protected void RefreshPropertyGrid()
        {
            if (propertyGrid != null)
            {
                propertyGrid.Refresh();
            }
        }

        public CsharpCondition(CsharpCodeType t)
            : base(new PrioritySelector())
        {
            CodeType = t;
            Properties = new PropertyBag();
            this.CanRunDelegate = c => false;
            Properties["Condition"] = new MetaProp("Condition", typeof(string));
            Properties["CompileError"] = new MetaProp("CompileError", typeof(string), new ReadOnlyAttribute(true));

            Condition = "";
            CompileError = "";
            Properties["CompileError"].Show = false;

            Properties["Condition"].PropertyChanged += new EventHandler(Condition_PropertyChanged);
            Properties["CompileError"].PropertyChanged += new EventHandler(CompileError_PropertyChanged);
        }

        void Condition_PropertyChanged(object sender, EventArgs e)
        {
            Professionbuddy.Instance.CodeWasModified = true;
        }

        string lastError = "";
        void CompileError_PropertyChanged(object sender, EventArgs e)
        {
            if (CompileError != "" || (CompileError == "" && lastError != ""))
                MainForm.Instance.RefreshActionTree(this);
            if (CompileError != "")
                Properties["CompileError"].Show = true;
            else
                Properties["CompileError"].Show = false;
            RefreshPropertyGrid();
            lastError = CompileError;
        }

        protected override bool CanRun(object context)
        {
            try
            {
                return CanRunDelegate(context);
            }
            catch (Exception ex)
            {
                Professionbuddy.Err("If Condition: {0} ,Err:{1}", Condition, ex);
                return false;
            }
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

        public CsharpCodeType CodeType { get; private set; }

        virtual public System.Drawing.Color Color
        {
            get { return string.IsNullOrEmpty(CompileError) ? System.Drawing.Color.Blue : System.Drawing.Color.Red; }
        }

        virtual public string Name { get { return "CSharpCondition"; } }
        virtual public string Title { get { return "CSharpCondition"; } }

        virtual public PropertyBag Properties { get; private set; }
        virtual public void Reset()
        {
            recursiveReset((PrioritySelector)DecoratedChild);
        }
        void recursiveReset(PrioritySelector ps)
        {
            foreach (IPBComposite comp in ps.Children)
            {
                comp.Reset();
                if (comp is CsharpCondition)
                    recursiveReset((PrioritySelector)((CsharpCondition)comp).DecoratedChild);
            }
        }
        public bool IsDone
        {
            get
            {
                PrioritySelector ps = (PrioritySelector)DecoratedChild;
                return ps.Children.Count(c => ((IPBComposite)c).IsDone) == ps.Children.Count || !CanRun(null);
            }
        }

        virtual public object Clone()
        {
            throw new NotImplementedException();
        }

        virtual public string Help { get { return null; } }

        public System.Xml.Schema.XmlSchema GetSchema() { return null; }

        virtual public void ReadXml(System.Xml.XmlReader reader)
        {
            throw new NotImplementedException();
        }

        virtual public void WriteXml(System.Xml.XmlWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
