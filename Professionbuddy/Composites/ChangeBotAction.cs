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
