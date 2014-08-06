using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using HighVoltz.BehaviorTree;
using HighVoltz.Professionbuddy.ComponentBase;
using Styx;
using Styx.Common;

namespace HighVoltz.Professionbuddy
{
	[PBXmlElement("Professionbuddy")]
    public class PBBranch : Composite
    {
		public PBBranch(params Component[] children): base(children){}

		public async override Task<bool> Run()
		{
			if (!CanExecuteChildren())
				return false;

			foreach (var child in Children.SkipWhile(c => Selection != null && c != Selection))
			{
				var pbComp = child as IPBComponent;
				if (pbComp == null || pbComp.IsDone)
					continue;

				Selection = child;

				var coroutine = new Coroutine(async () =>await child.Run());
				try
				{
					while (true)
					{
						coroutine.Resume();

						if (coroutine.Status == CoroutineStatus.RanToCompletion)
							break;

						await Coroutine.Yield();
						if (!CanExecuteChildren())
							return false;
					}
				
					if ((bool)coroutine.Result)
						return true;
				}
				finally
				{
					coroutine.Dispose();
				}
			}

			return false;
		}

		public void Reset()
		{
			Selection = null;
			Children.OfType<IPBComponent>().ForEach(c => c.Reset());
		}

		private bool CanExecuteChildren()
		{
			return StyxWoW.IsInWorld && ProfessionbuddyBot.Instance.IsRunning
				&& (!StyxWoW.Me.IsActuallyInCombat || StyxWoW.Me.IsFlying)
				&& StyxWoW.Me.IsAlive && StyxWoW.Me.HealthPercent >= 40;
		}

    }
}