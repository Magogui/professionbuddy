using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Linq;
using System.Xml;
using Styx;
using Styx.Logic.Inventory.Frames.MailBox;
using Styx.Logic.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.Helpers;
using TreeSharp;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using System.Xml.Serialization;

namespace HighVoltz.Composites
{
    public class Settings : PBAction
    {
        [PbXmlAttribute()]
        public string DefaultValue
        {
            get { return (string)Properties["DefaultValue"].Value; }
            set { Properties["DefaultValue"].Value = value; }
        }
        [PbXmlAttribute()]
        public TypeCode Type
        {
            get { return (TypeCode)Properties["Type"].Value; }
            set { Properties["Type"].Value = value; }
        }

        [PbXmlAttribute("Name")]
        public string SettingName
        {
            get { return (string)Properties["Name"].Value; }
            set { Properties["Name"].Value = value; }
        }
        [PbXmlAttribute()]
        public string Summary
        {
            get { return (string)Properties["Summary"].Value; }
            set { Properties["Summary"].Value = value; }
        }
        [PbXmlAttribute()]
        public string Category
        {
            get { return (string)Properties["Category"].Value; }
            set { Properties["Category"].Value = value; }
        }
        [PbXmlAttribute()]
        public bool Global
        {
            get { return (bool)Properties["Global"].Value; }
            set { Properties["Global"].Value = value; }
        }
        [PbXmlAttribute()]
        public bool Hidden
        {
            get { return (bool)Properties["Hidden"].Value; }
            set { Properties["Hidden"].Value = value; }
        }

        public Settings()
        {
            Properties["DefaultValue"] = new MetaProp("DefaultValue", typeof(string), new DisplayNameAttribute("Default Value"));
            Properties["Type"] = new MetaProp("Type", typeof(TypeCode));
            Properties["Name"] = new MetaProp("Name", typeof(string));
            Properties["Summary"] = new MetaProp("Summary", typeof(string));
            Properties["Category"] = new MetaProp("Category", typeof(string));
            Properties["Global"] = new MetaProp("Global", typeof(bool));
            Properties["Hidden"] = new MetaProp("Hidden", typeof(bool));

            DefaultValue = "true";
            Type = TypeCode.Boolean;
            SettingName = "SettingName";
            Summary = "This is a summary of what this setting does";
            Category = "Misc";
            Global = false;
            Hidden = false;
        }

        public override object Clone()
        {
            return new Settings() 
            { DefaultValue = this.DefaultValue,
                SettingName = this.SettingName,
                Type = this.Type,
                Summary = this.Summary,
                Category = this.Category,
                Global = this.Global,
                Hidden = this.Hidden
            };
        }
        public override string Help
        {
            get
            {
                return "This action adds a user customizable setting to Professionbuddy profiles";
            }
        }
        public override string Name
        {
            get
            {
                return "Settings";
            }
        }
        public override string Title
        {
            get
            {
                return string.Format("{0}: {1} {2}={3}",Name,Type,SettingName,DefaultValue);
            }
        }
        public override bool IsDone
        {
            get
            {
                return true;
            }
        }
    }
}
