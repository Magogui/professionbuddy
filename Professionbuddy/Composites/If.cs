using System;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Styx.Helpers;
using TreeSharp;
using System.Diagnostics;
using PrioritySelector = TreeSharp.PrioritySelector;


namespace HighVoltz.Composites
{

    public class If : CsharpCondition
    {
        virtual public bool IgnoreCanRun
        {
            get { return (bool)Properties["IgnoreCanRun"].Value; }
            set { Properties["IgnoreCanRun"].Value = value; }
        }
        public If()
            : base(CsharpCodeType.BoolExpression)
        {
            Properties["IgnoreCanRun"] = new MetaProp("IgnoreCanRun", typeof(bool), new DisplayNameAttribute("Ignore Condition until done"));
            IgnoreCanRun = true;
        }


        override public string Name { get { return "If Condition"; } }
        override public string Title
        {
            get
            {
                return string.Format("If {0}",
                    string.IsNullOrEmpty(Condition) ? "Condition" : "(" + Condition + ")");
            }
        }

        public override RunStatus Tick(object context)
        {
            if ((LastStatus == RunStatus.Running && IgnoreCanRun) || CanRun(null))
            {
                if (!DecoratedChild.IsRunning)
                    DecoratedChild.Start(null);
                LastStatus = DecoratedChild.Tick(null);
            }
            else
            {
                LastStatus = RunStatus.Failure;
            }
            return (RunStatus)LastStatus;
        }

        public override object Clone()
        {
            If pd = new If()
            {
                CanRunDelegate = this.CanRunDelegate,
                Condition = this.Condition,
                IgnoreCanRun = this.IgnoreCanRun
            };
            return pd;
        }

        override public string Help { get { return "'If Condition' will execute the actions it contains if the specified condition is true. 'Ignore Condition until done' basically will ignore the Condition if any of the actions it contains is running.If you need to repeat a set of actions then use 'While Condition' or nest this within a 'While Condition'"; } }

        #region XmlSerializer
        override public void ReadXml(XmlReader reader)
        {
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
            Condition = reader["Condition"];
            bool boolVal;
            bool.TryParse(reader["IgnoreCanRun"], out boolVal);
            IgnoreCanRun = boolVal;
            reader.MoveToAttribute("ChildrenCount");
            int count = reader.ReadContentAsInt();
            reader.ReadStartElement();
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    Type type = Type.GetType("HighVoltz.Composites." + reader.Name);
                    if (type != null)
                    {
                        IPBComposite comp = (IPBComposite)Activator.CreateInstance(type);
                        if (comp != null)
                        {
                            comp.ReadXml(reader);
                            ps.AddChild((Composite)comp);
                        }
                    }
                    else
                    {
                        Logging.Write(System.Drawing.Color.Red, "Failed to load type {0}", type.Name);
                    }
                }
                if (reader.NodeType == XmlNodeType.EndElement)
                    reader.ReadEndElement();
            }
        }

        override public void WriteXml(XmlWriter writer)
        {
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
            writer.WriteAttributeString("Condition", Condition);
            writer.WriteAttributeString("IgnoreCanRun", IgnoreCanRun.ToString());
            writer.WriteStartAttribute("ChildrenCount");
            writer.WriteValue(ps.Children.Count);
            writer.WriteEndAttribute();
            foreach (IPBComposite comp in ps.Children)
            {
                writer.WriteStartElement(comp.GetType().Name);
                ((IXmlSerializable)comp).WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        #endregion
    }
}
