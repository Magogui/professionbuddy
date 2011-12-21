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
using System.Diagnostics;
using Styx.Helpers;

namespace HighVoltz.Composites
{

    #region PBIdentityComposite
    public class PbDecorator : Decorator, IXmlSerializable
    {
        public PbDecorator(PrioritySelector ps)
            : base(c => StyxWoW.IsInWorld && !ExitBehavior() && Professionbuddy.Instance.IsRunning, ps) { }

        static LocalPlayer Me = ObjectManager.Me;
        //Stopwatch _profilerSW = new Stopwatch();
        public override RunStatus Tick(object context)
        {
            if (CanRun(null))
            {
                try
                {
                    if (!base.IsRunning)
                        base.Start(null);
                    // _profilerSW.Reset(); _profilerSW.Start();
                    LastStatus = base.Tick(context) == RunStatus.Running ? RunStatus.Running : RunStatus.Failure;
                    //if (LastStatus == RunStatus.Running)
                    //    Logging.Write("PbBehavior execution: {0}. PB behavior is running", _profilerSW.ElapsedMilliseconds);
                    //else
                    //    Logging.Write("PbBehavior execution: {0}. SecondaryBot is running", _profilerSW.ElapsedMilliseconds);
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

        //public override RunStatus Tick(object context)
        //{
        //    lock (Locker)
        //    {
        //        try
        //        {
        //            // _profilerSW.Reset(); _profilerSW.Start();
        //            LastStatus = base.Tick(context) == RunStatus.Running && !While.EndOfLoopReturn ? RunStatus.Running : RunStatus.Failure;
        //            //if (LastStatus == RunStatus.Running)
        //            //    Logging.Write("PbBehavior execution: {0}. PB behavior is running", _profilerSW.ElapsedMilliseconds);
        //            //else
        //            //    Logging.Write("PbBehavior execution: {0}. SecondaryBot is running", _profilerSW.ElapsedMilliseconds);
        //        }
        //        catch
        //        {
        //            LastStatus = RunStatus.Failure;
        //        }
        //        return LastStatus.Value;
        //    }
        //}

        //protected override IEnumerable<RunStatus> Execute(object context)
        //{
        //    if (!CanRun(context))
        //    {
        //        yield return RunStatus.Failure;
        //        yield break;
        //    }
        //    if (!DecoratedChild.IsRunning)
        //        DecoratedChild.Start(null);
        //    while (DecoratedChild.Tick(context) == RunStatus.Running)
        //    {
        //        if (While.EndOfLoopReturn)
        //            yield return RunStatus.Failure;
        //        yield return RunStatus.Running;
        //    }

        //    DecoratedChild.Stop(context);
        //    if (DecoratedChild.LastStatus == RunStatus.Failure)
        //    {
        //        yield return RunStatus.Failure;
        //        yield break;
        //    }

        //    yield return RunStatus.Success;
        //    yield break;
        //}
        //public override void Stop(object context)
        //{
        //    if (!While.EndOfLoopReturn)
        //        base.Stop(context);
        //}
        // returns true if in combat or dead or low hp %
        public static bool ExitBehavior()
        {
            return ((Me.IsActuallyInCombat && !Me.Mounted) ||
                (Me.IsActuallyInCombat && Me.Mounted && !Me.IsFlying && Mount.ShouldDismount(Util.GetMoveToDestination()))) ||
                !Me.IsAlive || Me.HealthPercent <= 40;
        }

        public void ReadXml(XmlReader reader)
        {
            reader.MoveToContent();
            reader.ReadStartElement("Professionbuddy");
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
            while (reader.NodeType == XmlNodeType.Element || reader.NodeType == XmlNodeType.Comment)
            {
                if (reader.NodeType == XmlNodeType.Comment)
                {
                    ps.AddChild(new Comment(reader.Value));
                    reader.Skip();
                }
                else
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
            }
            if (reader.NodeType == XmlNodeType.Element)
                reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement("Professionbuddy");
            PrioritySelector ps = (PrioritySelector)DecoratedChild;
            foreach (IPBComposite comp in ps.Children)
            {
                if (comp is Comment)
                {
                    writer.WriteComment(((Comment)comp).Text);
                }
                else
                {
                    writer.WriteStartElement(comp.GetType().Name);
                    ((IXmlSerializable)comp).WriteXml(writer);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }
        public System.Xml.Schema.XmlSchema GetSchema() { return null; }
    }
    #endregion
}
