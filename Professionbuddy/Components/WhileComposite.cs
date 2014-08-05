using System;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.UberBehaviorTree;

namespace HighVoltz.Professionbuddy.Components
{
	[PBXmlElement("While")]
	public sealed class WhileComposite : FlowControlComposite
	{
		public WhileComposite() : this(new Component[0]) { }
		private WhileComposite(Component[] children) : base(children) { }

		#region IPBComponent Members

		public override string Name
		{
			get { return ProfessionbuddyBot.Instance.Strings["FlowControl_While_LongName"]; }
		}

		public override string Title
		{
			get
			{
				return string.IsNullOrEmpty(Condition)
						   ? ProfessionbuddyBot.Instance.Strings["FlowControl_While_LongName"]
						   : (ProfessionbuddyBot.Instance.Strings["FlowControl_While_Name"] + " (" + Condition + ")");
			}
		}

		public override string Help
		{
			get { return ProfessionbuddyBot.Instance.Strings["FlowControl_While_Help"]; }
		}

		public override async Task<bool> Run()
		{
			if ((!IsRunning || !IgnoreCanRun) && !CanRun())
			{
				IsDone = true;
				return false;
			}

			IsRunning = true;
			foreach (var child in Children.SkipWhile(c => Selection != null && c != Selection))
			{
				var pbComp = child as IPBComponent;
				if (pbComp == null || pbComp.IsDone)
					continue;
				Selection = pbComp;

				var coroutine = new Coroutine(async () => await child.Run());
				try
				{
					while (true)
					{
						coroutine.Resume();

						if (coroutine.Status == CoroutineStatus.RanToCompletion)
							break;

						await Coroutine.Yield();
						if (!IgnoreCanRun && !CanRun())
						{
							IsDone = true;
							return false;
						}
					}

					if ((bool) coroutine.Result)
						return true;
				}
				finally
				{
					coroutine.Dispose();
				}
			}

			if (CanRun())
			{
				PB.Branch.YieldToSecondaryBot = true;
				Reset();
				return true;
			}
			IsDone = true;
			return false;
		}
		
		public override IPBComponent DeepCopy()
		{
			return new WhileComposite(DeepCopyChildren())
			{
				CanRunDelegate = CanRunDelegate,
				Condition = Condition,
				IgnoreCanRun = IgnoreCanRun,
			};
		}

		#endregion

    }
}