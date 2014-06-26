using System;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace HighVoltz.Composites
{
	public class PbRootComposite : PrioritySelector
	{
		private bool _calledStart;
		public PbRootComposite(PbDecorator pbBotBase, BotBase secondaryBot)
			: base(pbBotBase, secondaryBot == null ? new PrioritySelector() : secondaryBot.Root)
		{
			SecondaryBot = secondaryBot;
		}

		public PbDecorator PbBotBase
		{
			get { return Children[0] as PbDecorator; }
			set { Children[0] = value; }
		}

		public BotBase SecondaryBot { get; set; }

		// hackish fix but needed.
		public void AddSecondaryBot()
		{
			_calledStart = false;
			Children[1] = CreateSeondaryBotBehavior();
		}

		Composite CreateSeondaryBotBehavior()
		{
			return new PrioritySelector(
				new Decorator(ctx => !_calledStart, 
					new Action(
						ctx =>
						{
							try
							{
								SecondaryBot.Start();
							}
							catch (Exception ex)
							{
								if (ex is NullReferenceException && ex.StackTrace.Contains("Gatherbuddy.Profile"))
								{
									Professionbuddy.Log("Attempting to recover from Gatherbuddy startup error. ");
									Professionbuddy.PreLoadHbProfile();
								}
								else
								{
									Logging.WriteDiagnostic(ex.ToString());
								}
							}
							finally
							{
								_calledStart = true;
							}})
				),
				SecondaryBot.Root);
		}
	}
}