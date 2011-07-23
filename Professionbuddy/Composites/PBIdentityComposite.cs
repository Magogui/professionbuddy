using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TreeSharp;
using System.Xml;
using System.Xml.Serialization;
using Styx.WoWInternals.WoWObjects;
using Styx.WoWInternals;
using Styx;
using Styx.Logic;

namespace HighVoltz.Composites
{

    #region PBIdentityComposite
    public class PBIdentityComposite : Decorator, IXmlSerializable
    {
        public PBIdentityComposite(PrioritySelector ps)
            : base(c => StyxWoW.IsInWorld && !ExitBehavior() && Professionbuddy.Instance.IsRunning, ps) { }

        static LocalPlayer Me = ObjectManager.Me;
        public override RunStatus Tick(object context)
        {
            if (CanRun(null))
            {
                try
                {
                    if (!base.IsRunning)
                        base.Start(null);
                    LastStatus = base.Tick(context) != RunStatus.Running ? RunStatus.Failure : RunStatus.Running;
                }
                catch 
                {
                    LastStatus = RunStatus.Failure;
                }
            }
            else
                LastStatus = RunStatus.Failure;
            return (RunStatus)LastStatus;
        }
        // returns true if in combat or dead or low hp %
        public static bool ExitBehavior()
        {
            return !Professionbuddy.Instance.IsEnabled ||
                ((Me.IsActuallyInCombat && !Me.Mounted) ||
                (Me.IsActuallyInCombat && Me.Mounted && !Me.IsFlying && Mount.ShouldDismount(Util.GetMoveToDestination()))) ||
                !Me.IsAlive || Me.HealthPercent <= 40;
        }

        public void ReadXml(XmlReader reader)
        {
            int count;
            reader.MoveToContent();
            int.TryParse(reader["ChildrenCount"], out count);
            reader.ReadStartElement("Professionbuddy");
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
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
                    Professionbuddy.Err("PB:Failed to load type {0}", type);
                }
            }
            if (reader.NodeType == XmlNodeType.EndElement)
                reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Professionbuddy");
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
            writer.WriteStartAttribute("ChildrenCount");
            writer.WriteValue(ps.Children.Count);
            writer.WriteEndAttribute();
            foreach (IPBComposite comp in ps.Children)
            {
                writer.WriteStartElement(comp.GetType().Name);
                ((IXmlSerializable)comp).WriteXml(writer);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        public System.Xml.Schema.XmlSchema GetSchema() { return null; }
    }
    #endregion
}
