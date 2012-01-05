using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using HighVoltz.Dynamic;

namespace HighVoltz.Composites
{
    //this is a PBAction derived abstract class that adds functionallity for dynamically compiled Csharp expression/statement

    public abstract class CsharpAction : PBAction, ICSharpCode
    {
        public CsharpAction()
            : this(CsharpCodeType.Statements) { }

        public CsharpAction(CsharpCodeType t)
        {
            CodeType = t;
            Properties["CompileError"] = new MetaProp("CompileError", typeof(string), new ReadOnlyAttribute(true));

            CompileError = "";

            Properties["CompileError"].Show = false;
            Properties["CompileError"].PropertyChanged += CompileError_PropertyChanged;
        }

        string lastError = "";
        void CompileError_PropertyChanged(object sender, MetaPropArgs e)
        {
            if (CompileError != "" || (CompileError == "" && lastError != ""))
            {
                MainForm.Instance.RefreshActionTree(this);
                Properties["CompileError"].Show = true;
            }
            else
                Properties["CompileError"].Show = false;
            RefreshPropertyGrid();
            lastError = CompileError;
        }

        public int CodeLineNumber { get; set; }

        public string CompileError
        {
            get { return (string)Properties["CompileError"].Value; }
            set { Properties["CompileError"].Value = value; }
        }

        public CsharpCodeType CodeType { get; protected set; }

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
