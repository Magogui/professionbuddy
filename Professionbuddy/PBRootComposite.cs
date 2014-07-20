using System;
using System.Threading.Tasks;
using CommonBehaviors.Actions;
using HighVoltz.UberBehaviorTree;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Action = HighVoltz.UberBehaviorTree.Action;

namespace HighVoltz.Professionbuddy
{
	/// <summary>
	/// Internal Root class for all Professionbuddy operations.
	/// </summary>
    public sealed class PBRootComposite : PrioritySelector
	{
		private BotBase _secondaryBot;
		private PBBranch _branch;

		public PBRootComposite(PBBranch branch, BotBase secondaryBot)
			: base(branch, new SecondaryBotExecutorAction(secondaryBot))
		{
			_branch = branch;
			_secondaryBot = secondaryBot;
		}

		/// <summary>Gets or sets the branch. This contains the behavior of the Professionbuddy profile.</summary>
		public PBBranch Branch 
		{
			get { return _branch; }
			set
			{
				if (_branch == value)
					return;
				Children[0] = _branch = value;
			} 
		}

		public BotBase SecondaryBot
		{
			get { return _secondaryBot; }
			set
			{
				if (_secondaryBot == value)
					return;
				_secondaryBot = value;
				SecondaryBotExecutor = new SecondaryBotExecutorAction(value);
			}
		}

		SecondaryBotExecutorAction SecondaryBotExecutor
		{
			get { return (SecondaryBotExecutorAction)Children[1]; }
			set { Children[1] = value; }
		}

		public void Reset()
		{
			ResetBranch();
			ResetSecondaryBot();
		}

		public void ResetBranch()
		{
			Branch.Reset();
		}

		public void ResetSecondaryBot()
		{
			SecondaryBotExecutor.Reset();
		}

		sealed class SecondaryBotExecutorAction : Action
		{
			private bool _calledStart;
			private readonly BotBase _botbase;
			public SecondaryBotExecutorAction(BotBase botbase)
			{
				_botbase = botbase;
			}

			public void Reset()
			{
				_calledStart = false;
			}

			public override async Task<bool> Run()
			{
				if (_botbase == null || _botbase.Root == null)
					return false;

				if (!_calledStart)
					StartSecondaryBot();
				return await _botbase.Root.ExecuteCoroutine();
			}

			private void StartSecondaryBot()
			{
				try
				{
					_botbase.Start();
				}
				finally
				{
					_calledStart = true;
				}
			}
		}
	}
}