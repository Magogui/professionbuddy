using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("SubRoutine")]
    public sealed class SubRoutineComposite : PBComposite
    {
		public SubRoutineComposite() : this(new UberBehaviorTree.Component[0]) { }
        public SubRoutineComposite(params UberBehaviorTree.Component[] children): base(children)
        {		
            Properties["SubRoutineName"] = new MetaProp("SubRoutineName", typeof (string),
                                                        new DisplayNameAttribute(
                                                            ProfessionbuddyBot.Instance.Strings[
                                                                "Action_SubRoutine_SubroutineName"]));
            SubRoutineName = "";
        }

        [PBXmlAttribute]
        public string SubRoutineName
        {
            get { return Properties.GetValue<string>("SubRoutineName"); }
            set { Properties["SubRoutineName"].Value = value; }
        }

		public async Task<bool> Execute()
		{
			foreach (var child in Children.Where(c => !((IPBComponent)c).IsDone))
			{
				if (await child)
					return true;
			}
			IsDone = true;
			return false;
		}

		#region PBComposite Members

		override public Color Color
        {
            get { return Color.Blue; }
        }

		override public string Name
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_SubRoutine_Name"]; }
        }

		override public string Title
        {
            get { return string.Format("Sub {0}", SubRoutineName); }
        }

        override public string Help
        {
            get { return ProfessionbuddyBot.Instance.Strings["Action_SubRoutine_Help"]; }
        }

		public override IPBComponent DeepCopy()
		{
			return new SubRoutineComposite(DeepCopyChildren())
			{
				SubRoutineName = SubRoutineName,
			};
		}

		public async override Task<bool> Run()
		{
			return false;
		}

	    #endregion
    }
}