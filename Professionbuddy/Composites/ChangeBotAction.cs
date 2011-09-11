﻿using System;
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
using System.Windows;

namespace HighVoltz.Composites
{
    public class ChangeBotAction : PBAction
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
                try
                {
                    ChangeBot();
                }
                finally
                {
                    IsDone = true;
                }
            }
            return RunStatus.Failure;
        }

        public void ChangeBot()
        {
            ChangeBot(BotName);
        }

        static public void ChangeBot(string name)
        {
            BotBase bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Key.Contains(name)).Value;
            if (bot != null)
            {
                // execute from GUI thread since this thread will get aborted when switching bot
                Application.Current.Dispatcher.BeginInvoke(
                    new System.Action(() => {
                        BotManager.Instance.SetCurrent(bot);
                        TreeRoot.Start();
                    }
                ));
                Professionbuddy.Log("Changing bot to {0}", name);
            }
            else
            {
                Professionbuddy.Err("Bot {0} does not exist", name);
            }
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
