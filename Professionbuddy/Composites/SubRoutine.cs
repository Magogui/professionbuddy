using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using TreeSharp;
using System.Xml;
using Styx.Helpers;

namespace HighVoltz.Composites
{
    class SubRoutine : Decorator, IPBComposite, IXmlSerializable
    {
        virtual public string SubRoutineName {
            get { return (string)Properties["SubRoutineName"].Value; }
            set { Properties["SubRoutineName"].Value = value; }
        }
        public SubRoutine()
            : base(c=> false,new PrioritySelector()) {
            Properties = new PropertyBag();
            Properties["SubRoutineName"] = new MetaProp("SubRoutineName", typeof(string));
            SubRoutineName = "";
        }

        virtual public System.Drawing.Color Color { get { return System.Drawing.Color.Blue; } }
        virtual public string Name { get { return "SubRoutine"; } }
        virtual public string Title { get { return string.Format("Sub {0}", SubRoutineName); } }

        virtual public PropertyBag Properties { get; private set; }
        //public PrioritySelector Behavior { get; private set; }

        virtual public void Reset() {
            recursiveReset(DecoratedChild as PrioritySelector);
        }
        void recursiveReset(PrioritySelector ps) {
            foreach (IPBComposite comp in ps.Children)
            {
                comp.Reset();
                if (comp is If)
                    recursiveReset((PrioritySelector)((If)comp).DecoratedChild);
            }
        }
        public bool IsDone {
            get {
                return ((PrioritySelector)DecoratedChild).Children.Count(c => ((IPBComposite)c).IsDone) == ((PrioritySelector)DecoratedChild).Children.Count;
            }
        }

        public virtual object Clone() {
            SubRoutine pd = new SubRoutine()
            {
                SubRoutineName = this.SubRoutineName,
            };
            return pd;
        }

        virtual public string Help { get { return "SubRoutine can contain multiple actions which which you can execute using the 'Call SubRoutine' action"; } }

        #region XmlSerializer
        virtual public void ReadXml(XmlReader reader) {
            SubRoutineName = reader["SubRoutineName"];
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
                            ((PrioritySelector)DecoratedChild).AddChild((Composite)comp);
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

        virtual public void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("SubRoutineName", SubRoutineName);
            writer.WriteAttributeString("ChildrenCount", ((PrioritySelector)DecoratedChild).Children.Count.ToString());

            foreach (IPBComposite comp in ((PrioritySelector)DecoratedChild).Children)
            {
                writer.WriteStartElement(comp.GetType().Name);
                ((IXmlSerializable)comp).WriteXml(writer);
                writer.WriteEndElement();
            }
        }
        virtual public System.Xml.Schema.XmlSchema GetSchema() { return null; }
        #endregion
    }
}
