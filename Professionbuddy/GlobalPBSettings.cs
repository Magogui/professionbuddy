using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Styx.Helpers;

namespace HighVoltz
{
    public class GlobalPBSettings : Settings
    {
        public static GlobalPBSettings Instance { get; private set; }
        public GlobalPBSettings(string settingsPath)
            : base(settingsPath)
        {
            Instance = this;
            Load();
        }
       
        [Setting, DefaultValue(0)]
        public int CurrentRevision { get; set; }

        [Setting, DefaultValue(0u)]
        public uint KnownSpellsPtr { get; set; }

        [Setting, DefaultValue(null)]
        public string DataStoreTable { get; set; }

        [Setting, DefaultValue("")]
        public string WowVersion { get; set; }

    }
}
