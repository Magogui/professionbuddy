using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Styx.WoWInternals;
using System.Xml;
using System.Runtime.Serialization;
using Styx.Helpers;
using TreeSharp;
using ObjectManager = Styx.WoWInternals.ObjectManager;

namespace HighVoltz
{
    public class ProfessionBuddySettings : Styx.Helpers.Settings
    {
        public static ProfessionBuddySettings Instance { get; private set; }
        public ProfessionBuddySettings(string settingsPath)
            : base(settingsPath)
        {
            Instance = this;
            Load();
        }
        [Setting, DefaultValue("")]
        public string LastProfile { get; set; }

        [Setting, DefaultValue(null)]
        public string DataStoreTable { get; set; }

        [Setting, DefaultValue("")]
        public string WowVersion { get; set; }

        [Setting, DefaultValue(0u)]
        public uint TradeskillFrameOffset { get; set; }
        
        [Setting, DefaultValue("")]
        public string LastBotBase { get; set; }
    }

    public class PbProfileSettingEntry
    {
        public object Value { get; set; }
        public string Summary { get; set; }
        public string Category { get; set; }
    }

    public class PbProfileSettings
    {
        //public Dictionary<string, object> Settings { get; private set; }
        //public Dictionary<string, string> Summaries { get; private set; }
        //public Dictionary<string, string> Categories { get; private set; }
        public Dictionary<string, PbProfileSettingEntry> Settings { get; private set; }

        public PbProfileSettings()
        {
            Settings = new Dictionary<string, PbProfileSettingEntry>();
            //Summaries = new Dictionary<string, string>();
        }
        public object this[string name]
        {
            get
            {
                return Settings.ContainsKey(name) ? Settings[name].Value : null;
            }
            set
            {
                Settings[name].Value = value;
                if (Professionbuddy.Instance.CurrentProfile != null)
                    Save();
            }
        }
        string ProfileName
        {
            get
            {
                return Professionbuddy.Instance.CurrentProfile != null ?
                    Path.GetFileNameWithoutExtension(Professionbuddy.Instance.CurrentProfile.XmlPath) : "";
            }
        }
        string SettingsPath
        {
            get
            {
                return Path.Combine(Logging.ApplicationPath,
                    string.Format("Settings\\ProfessionBuddy\\{0}[{1}-{2}].xml", ProfileName,
                    ObjectManager.Me.Name, Lua.GetReturnVal<string>("return GetRealmName()", 0)));
            }
        }
        public void Save()
        {
            if (Professionbuddy.Instance.CurrentProfile != null)
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(SettingsPath, settings))
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(Dictionary<string, object>));
                    serializer.WriteObject(writer, Settings);
                }
            }
        }

        public void Load()
        {
            if (Professionbuddy.Instance.CurrentProfile != null)
            {
                Settings = new Dictionary<string, PbProfileSettingEntry>();
                if (File.Exists(SettingsPath))
                {
                    using (XmlReader reader = XmlReader.Create(SettingsPath))
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(Dictionary<string, object>));
                        var temp = (Dictionary<string, object>)serializer.ReadObject(reader);
                        if (temp != null)
                        {
                            foreach (var kv in temp)
                            {
                                Settings[kv.Key] = new PbProfileSettingEntry() { Value = kv.Value };
                            }
                        }
                    }
                }
                LoadDefaultValues();
            }
        }

        public void LoadDefaultValues()
        {
            List<Composites.Settings> settingsList = GetDefaultSettings(Professionbuddy.Instance.CurrentProfile.Branch);
            foreach (var setting in settingsList)
            {
                if (!Settings.ContainsKey(setting.SettingName))
                    Settings[setting.SettingName] = new PbProfileSettingEntry() { Value = GetValue(setting.Type, setting.DefaultValue) };
                Settings[setting.SettingName].Summary = setting.Summary;
                Settings[setting.SettingName].Category = setting.Category;
             }
            // remove unused settings..
            Settings = Settings.Where(kv => settingsList.Any(s => s.SettingName == kv.Key)).ToDictionary(kv => kv.Key,kv => kv.Value);
        }

        object GetValue(TypeCode code, string value)
        {
            try
            {
                switch (code)
                {
                    case TypeCode.Boolean:
                        return bool.Parse(value);
                    case TypeCode.Byte:
                        return byte.Parse(value);
                    case TypeCode.Char:
                        return char.Parse(value);
                    case TypeCode.DateTime:
                        return DateTime.Parse(value);
                    case TypeCode.Decimal:
                        return decimal.Parse(value);
                    case TypeCode.Double:
                        return double.Parse(value);
                    case TypeCode.Int16:
                        return short.Parse(value);
                    case TypeCode.Int32:
                        return int.Parse(value);
                    case TypeCode.Int64:
                        return long.Parse(value);
                    case TypeCode.SByte:
                        return sbyte.Parse(value);
                    case TypeCode.Single:
                        return float.Parse(value);
                    case TypeCode.String:
                        return value;
                    case TypeCode.UInt16:
                        return ushort.Parse(value);
                    case TypeCode.UInt32:
                        return uint.Parse(value);
                    case TypeCode.UInt64:
                        return ulong.Parse(value);
                    default:
                        return new object();
                }
            }
            catch (Exception ex) { Logging.WriteException(ex); return null; }
        }

        public List<Composites.Settings> GetDefaultSettings(Composite comp)
        {
            List<Composites.Settings> list = new List<Composites.Settings>();
            GetProfileSettings(comp, ref list);
            return list;
        }
        // recursively get all profile settings
        void GetProfileSettings(Composite comp, ref List<Composites.Settings> list)
        {
            if (comp is Composites.Settings)
                list.Add(comp as Composites.Settings);
            if (comp is GroupComposite)
            {
                foreach (var child in ((GroupComposite)comp).Children)
                    GetProfileSettings(child, ref list);
            }
        }
    }
}
