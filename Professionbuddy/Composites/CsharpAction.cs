using System;
using System.ComponentModel;
using HighVoltz.Dynamic;

namespace HighVoltz.Composites
{
    //this is a PBAction derived abstract class that adds functionallity for dynamically compiled Csharp expression/statement

    public abstract class CsharpAction : PBAction, ICSharpCode
    {
        protected CsharpAction()
            : this(HighVoltz.Dynamic.CsharpCodeType.Statements) { }

        protected CsharpAction(HighVoltz.Dynamic.CsharpCodeType t)
        {
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            CodeType = t;
            Properties["CompileError"] = new MetaProp("CompileError", typeof(string), 
                new ReadOnlyAttribute(true),
                new DisplayNameAttribute(Pb.Strings["Action_CSharpAction_CompileError"]));
            CompileError = "";
            Properties["CompileError"].Show = false;
            Properties["CompileError"].PropertyChanged += CompileErrorPropertyChanged;
            // ReSharper restore DoNotCallOverridableMethodsInConstructor
        }

        string _lastError = "";
        void CompileErrorPropertyChanged(object sender, MetaPropArgs e)
        {
            if (CompileError != "" || (CompileError == "" && _lastError != ""))
            {
                MainForm.Instance.RefreshActionTree(this);
                Properties["CompileError"].Show = true;
            }
            else
                Properties["CompileError"].Show = false;
            RefreshPropertyGrid();
            _lastError = CompileError;
        }

        public int CodeLineNumber { get; set; }

        public string CompileError
        {
            get { return (string)Properties["CompileError"].Value; }
            set { Properties["CompileError"].Value = value; }
        }

        public HighVoltz.Dynamic.CsharpCodeType CodeType { get; protected set; }

        virtual public string Code { get; set; }
        public override System.Drawing.Color Color
        {
            get { return string.IsNullOrEmpty(CompileError) ? base.Color : System.Drawing.Color.Red; }
        }

        virtual public Delegate CompiledMethod { get; set; }

        public IPBComposite AttachedComposite
        {
            get { return this; }
        }
    }
}
