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

    public class If : Decorator, IPBComposite, IXmlSerializable
    {
        virtual public CanRunDecoratorDelegate CanRunDelegate { get; set; }
        virtual public string Condition
        {
            get { return (string)Properties["Condition"].Value; }
            set { Properties["Condition"].Value = value; }
        }
        virtual public bool IgnoreCanRun
        {
            get { return (bool)Properties["IgnoreCanRun"].Value; }
            set { Properties["IgnoreCanRun"].Value = value; }
        }
        public If()
            : base(new PrioritySelector())
        {
            Properties = new PropertyBag();
            this.CanRunDelegate = c => false;
            Properties["Condition"] = new MetaProp("Condition", typeof(string));
            Properties["IgnoreCanRun"] = new MetaProp("IgnoreCanRun", typeof(bool), new DisplayNameAttribute("Ignore Condition until done"));
            Condition = "";
            IgnoreCanRun = true;
            Properties["Condition"].PropertyChanged += new EventHandler(PBDecorator_PropertyChanged);
        }
        void PBDecorator_PropertyChanged(object sender, EventArgs e)
        {
            Professionbuddy.Instance.CodeWasModified = true;
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
        virtual public System.Drawing.Color Color { get { return System.Drawing.Color.Blue; } }
        virtual public string Name { get { return "If Condition"; } }
        virtual public string Title
        {
            get
            {
                return string.Format("If {0}",
                    string.IsNullOrEmpty(Condition) ? "Condition" : "(" + Condition + ")");
            }
        }

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
                if (comp is If)
                    recursiveReset((PrioritySelector)((If)comp).DecoratedChild);
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
        public override RunStatus Tick(object context)
        {
            RunStatus status;
            if ((LastStatus == RunStatus.Running && IgnoreCanRun) || CanRun(null))
            {
                if (!DecoratedChild.IsRunning)
                    DecoratedChild.Start(null);
                status = DecoratedChild.Tick(null);
            }
            else
            {
                status = RunStatus.Failure;
            }
            LastStatus = status;
            return status;
        }

        public virtual object Clone()
        {
            If pd = new If()
            {
                CanRunDelegate = this.CanRunDelegate,
                Condition = this.Condition,
                IgnoreCanRun = this.IgnoreCanRun
            };
            return pd;
        }

        virtual public string Help { get { return "'If Condition' will execute the actions it contains if the specified condition is true. 'Ignore Condition until done' basically will ignore the Condition if any of the actions it contains is running.If you need to repeat a set of actions then use 'While Condition' or nest this within a 'While Condition'"; } }

        #region XmlSerializer
        virtual public void ReadXml(XmlReader reader)
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

        virtual public void WriteXml(XmlWriter writer)
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
        virtual public System.Xml.Schema.XmlSchema GetSchema() { return null; }
        #endregion
    }
}
