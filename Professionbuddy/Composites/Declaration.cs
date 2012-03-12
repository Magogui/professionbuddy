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
            : base(HighVoltz.Dynamic.CsharpCodeType.Declaration)
        {
            Properties["Code"] = new MetaProp("Code", typeof(string), 
                new EditorAttribute(typeof(MultilineStringEditor), typeof(UITypeEditor)),
                new DisplayNameAttribute(Pb.Strings["Action_Common_Code"]));

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

        public override string Name { get { return Pb.Strings["Action_Declaration_Name"]; } }
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
                return Pb.Strings["Action_Declaration_Help"];
            }
        }
        public override object Clone()
        {
            return new Declaration { Code = this.Code };
        }
    }
    #endregion
}
