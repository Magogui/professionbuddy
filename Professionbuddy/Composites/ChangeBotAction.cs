using System.ComponentModel;
using TreeSharp;

namespace HighVoltz.Composites
{
    public sealed class ChangeBotAction : PBAction
    {
        [PbXmlAttribute]
        public string BotName
        {
            get { return (string)Properties["BotName"].Value; }
            set { Properties["BotName"].Value = value; }
        }

        public ChangeBotAction()
        {
            Properties["BotName"] = new MetaProp("BotName", typeof(string), new DisplayNameAttribute("Bot Name"));
            BotName = "";
        }

        protected override RunStatus Run(object context)
        {
            if (!IsDone)
            {
                try
                {
                    Professionbuddy.ChangeSecondaryBot(BotName) ;
                }
                finally
                {
                    IsDone = true;
                }
            }
            return RunStatus.Failure;
        }


        //static Timer _timer;
        //static Stopwatch _throttleSW = new Stopwatch();
        //static bool _botIsChanging = false;
        //static public void ChangeBot(string name)
        //{
        //    if (_botIsChanging)
        //    {
        //        Professionbuddy.Log("Must wait for previous ChangeBot to finish before calling ChangeBot again.");
        //        return;
        //    }
        //    BotBase bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Key.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) >= 0).Value;
        //    if (BotManager.Current == bot)
        //        return;
        //    if (bot != null)
        //    {
        //        // execute from GUI thread since this thread will get aborted when switching bot
        //        _botIsChanging = true;
        //        Application.Current.Dispatcher.BeginInvoke(
        //            new System.Action(() =>
        //            {
        //                BotManager.Instance.SetCurrent(bot);
        //                Professionbuddy.Log("Restarting HB in 3 seconds");
        //                _timer = new Timer(new TimerCallback((o) =>
        //                {
        //                    TreeRoot.Start();
        //                    Professionbuddy.Log("Restarting HB");
        //                }), null, 3000, Timeout.Infinite);
        //                _botIsChanging = false;

        //            }
        //        ));
        //        Professionbuddy.Log("Changing bot to {0}", name);
        //    }
        //    else
        //    {
        //        Professionbuddy.Err("Bot {0} does not exist", name);
        //    }
        //}

        public override string Name { get { return "Change Bot"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0} to :{1}", Name, BotName);
            }
        }
        public override string Help
        {
            get
            {
                return "This action will change to the bot specified with 'Bot Name' Property. 'Bot Name' can be a partial match";
            }
        }
        public override object Clone()
        {
            return new ChangeBotAction { BotName = this.BotName };
        }
    }
}
