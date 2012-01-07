//!CompilerOption:AddRef:System.Design.dll

using TreeSharp;
using System.Drawing.Design;
using System.ComponentModel;
using System.ComponentModel.Design;
using HighVoltz.Dynamic;

namespace HighVoltz.Composites
{
    #region Declaration
    public sealed class Declaration : CsharpAction
    {
        [PbXmlAttribute]
        override public string Code
        {
            get { return (string)Properties["Code"].Value; }
            set { Properties["Code"].Value = value; }
        }

        public Declaration()
            : base(CsharpCodeType.Declaration)
        {
            Properties["Code"] = new MetaProp("Code", typeof(string), new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)));
            Code = "";
            Properties["Code"].PropertyChanged += Code_PropertyChanged;
        }

        void Code_PropertyChanged(object sender, MetaPropArgs e)
        {
            DynamicCodeCompiler.CodeWasModified = true;
        }

        protected override RunStatus Run(object context)
        {
            return RunStatus.Failure;
        }

        public override string Name { get { return "Declaration"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0}:({1})", Name, Code);
            }
        }
        public override string Help
        {
            get
            {
                return "This is useful to declare fields, properties, methods, classes or any other types.";
            }
        }
        public override object Clone()
        {
            return new Declaration { Code = this.Code };
        }
    }
    #endregion
}
