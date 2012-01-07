using System.Collections.Generic;
using System.Linq;
using TreeSharp;

namespace HighVoltz.Composites
{
    sealed class SubRoutine : GroupComposite, IPBComposite
    {
        [PbXmlAttribute]
        public string SubRoutineName
        {
            get { return (string)Properties["SubRoutineName"].Value; }
            set { Properties["SubRoutineName"].Value = value; }
        }
        public SubRoutine()
        {
            Properties = new PropertyBag();
            Properties["SubRoutineName"] = new MetaProp("SubRoutineName", typeof(string));
            SubRoutineName = "";
        }

        public System.Drawing.Color Color { get { return System.Drawing.Color.Blue; } }
        public string Name { get { return "SubRoutine"; } }
        public string Title { get { return string.Format("Sub {0}", SubRoutineName); } }

        public PropertyBag Properties { get; private set; }

        protected override IEnumerable<RunStatus> Execute(object context)
        {
            if (context == null || !(context is string) || (string)context != SubRoutineName)
                yield return RunStatus.Failure;
            foreach (Composite child in Children.SkipWhile(c => Selection != null && c != Selection))
            {
                child.Start(context);
                Selection = child;
                while (child.Tick(context) == RunStatus.Running)
                {
                    yield return RunStatus.Running;
                }
                if (child.LastStatus == RunStatus.Success)
                    yield return RunStatus.Success;
            }
            Selection = null;
            IsDone = true;
            yield return RunStatus.Failure;
        }

        public void Reset()
        {
            Selection = null;
            IsDone = false;
            recursiveReset(this);
        }
        void recursiveReset(GroupComposite gc)
        {
            foreach (IPBComposite comp in gc.Children)
            {
                comp.Reset();
                if (comp is GroupComposite)
                    recursiveReset(comp as GroupComposite);
            }
        }

        public bool IsDone { get; set; }

        public object Clone()
        {
            var pd = new SubRoutine
                         {
                             SubRoutineName = this.SubRoutineName,
                         };
            return pd;
        }

        public string Help { get { return "SubRoutine can contain multiple actions which which you can execute using the 'Call SubRoutine' action"; } }



        public void OnProfileLoad(System.Xml.Linq.XElement element) { }

        public void OnProfileSave(System.Xml.Linq.XElement element) { }
    }
}
