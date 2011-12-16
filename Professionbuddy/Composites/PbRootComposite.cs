using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TreeSharp;
using Styx;

namespace HighVoltz.Composites
{
    public class PbRootComposite : PrioritySelector
    {
        public PbRootComposite(PbDecorator pbBotBase, BotBase secondaryBot) : base(pbBotBase, secondaryBot == null ? new PrioritySelector() : secondaryBot.Root) { _secondaryBot = secondaryBot; }
        
        public PbDecorator PbBotBase
        {
            get { return Children[0] as PbDecorator; }
            set { Children[0] = value; }
        }
        BotBase _secondaryBot;
        public BotBase SecondaryBot
        {
            get { return _secondaryBot; }
            set { _secondaryBot = value; Children[1] = value.Root; }
        }
    }
}
