//!CompilerOption:AddRef:System.Design.dll
using System;
using System.Xml;
using Styx.Helpers;
using TreeSharp;
using System.Threading;
using System.Drawing.Design;
using System.ComponentModel;
using System.ComponentModel.Design;

namespace HighVoltz.Composites
{
    #region Declaration
    public class Declaration : CsharpAction
    {
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
            Properties["Code"].PropertyChanged += new EventHandler(Code_PropertyChanged);
        }

        void Code_PropertyChanged(object sender, EventArgs e)
        {
            DynamicCode.CodeWasModified = true;
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
                return string.Format("{0}:({1})",Name,Code);
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
            return new Declaration() { Code = this.Code };
        }


        #region XmlSerializer
        public override void ReadXml(XmlReader reader)
        {
            Code = reader["Code"];
            reader.ReadStartElement();
        }
        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("Code", Code.ToString());
        }
        #endregion
    }
    #endregion
}
