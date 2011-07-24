using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using TreeSharp;
using System.Threading;
using Styx.Logic.BehaviorTree;
using Styx;
using System.Xml;
using Styx.Helpers;

namespace HighVoltz.Composites
{
    class ChangeBotAction : PBAction
    {
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
                ChangeBot();
                IsDone = true;
            }
            return RunStatus.Failure;
        }

        public bool ChangeBot()
        {
            BotBase bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Key.Contains(BotName)).Value;
            if (bot == null)
            {
                Professionbuddy.Err("ChangeBotAction was unable to find the following bot {0}",BotName);
                return false;
            }
            Professionbuddy.Log("ChangeBotAction: Switching to {0}",BotName);
            new Thread(() => {
                try
                {
                    TreeRoot.Stop();
                }
                catch (Exception ex)
                {
                    Logging.Write("ChangeBot: " + ex.ToString());
                }
                finally
                {
                    BotManager.Instance.SetCurrent(bot);
                    Thread.Sleep(3000);
                    TreeRoot.Start();
                }
            }).Start();
            return true;
        }

        public override string Name { get { return "Change Bot"; } }
        public override string Title
        {
            get
            {
                return string.Format("{0} to :{1}",Name,BotName);
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
            return new ChangeBotAction() { BotName = this.BotName };
        }
        #region XmlSerializer
        public override void ReadXml(XmlReader reader)
        {
            BotName = reader["BotName"];
            reader.ReadStartElement();
        }
        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString("BotName", BotName.ToString());
        }
        #endregion
    }
}
