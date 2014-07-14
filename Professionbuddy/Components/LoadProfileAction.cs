using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using HighVoltz.Professionbuddy.ComponentBase;
using HighVoltz.Professionbuddy.PropertyGridUtilities;
using HighVoltz.Professionbuddy.PropertyGridUtilities.Editors;
using Styx.Common.Helpers;
using Styx.CommonBot.Profiles;

namespace HighVoltz.Professionbuddy.Components
{
	public enum LoadProfileType
	{
		Honorbuddy,
		Professionbuddy
	}

	[PBXmlElement("LoadProfile", new []{"LoadProfileAction"})]
	public sealed class LoadProfileAction : PBAction
	{

		private readonly WaitTimer _loadProfileTimer = new WaitTimer(TimeSpan.FromSeconds(5));
		private bool _loadedProfile;

		public LoadProfileAction()
		{
			Properties["Path"] = new MetaProp(
				"Path",
				typeof (string),
				new EditorAttribute(
					typeof (FileLocationEditor),
					typeof (UITypeEditor)),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_Common_Path"]));

			Properties["ProfileType"] = new MetaProp(
				"ProfileType",
				typeof (LoadProfileType),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_ProfileType"]));

			Properties["IsLocal"] = new MetaProp(
				"IsLocal",
				typeof (bool),
				new DisplayNameAttribute(ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_IsLocal"]));

			Path = "";
			ProfileType = LoadProfileType.Honorbuddy;
			IsLocal = true;
		}

		[PBXmlAttribute]
		public LoadProfileType ProfileType
		{
			get { return Properties.GetValue<LoadProfileType>("ProfileType"); }
			set { Properties["ProfileType"].Value = value; }
		}

		[PBXmlAttribute]
		public string Path
		{
			get { return Properties.GetValue<string>("Path"); }
			set { Properties["Path"].Value = value; }
		}

		[PBXmlAttribute]
		public bool IsLocal
		{
			get { return Properties.GetValue<bool>("IsLocal"); }
			set { Properties["IsLocal"].Value = value; }
		}

		public string AbsolutePath
		{
			get
			{
				if (!IsLocal)
					return Path;

				return string.IsNullOrEmpty(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath)
					? string.Empty
					: System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ProfessionbuddyBot.Instance.CurrentProfile.XmlPath), Path);
			}
		}

		public override string Name
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_Name"]; }
		}

		public override string Title
		{
			get { return string.Format("{0}: {1}", Name, Path); }
		}

		public override string Help
		{
			get { return ProfessionbuddyBot.Instance.Strings["Action_LoadProfileAction_Help"]; }
		}

		protected async override Task Run()
		{
			if (!_loadedProfile)
			{
				if (Load())
				{
					_loadProfileTimer.Reset();
				}
				_loadedProfile = true;
			} 
			// We need to wait for a profile to load because the profile might be loaded asynchronously
			if (_loadProfileTimer.IsFinished ||
				(!string.IsNullOrEmpty(ProfileManager.XmlLocation) && ProfileManager.XmlLocation.Equals(AbsolutePath)))
			{
				IsDone = true;
			}
		}

		public bool Load()
		{
			var absPath = AbsolutePath;

			if (IsLocal && !string.IsNullOrEmpty(ProfileManager.XmlLocation) &&
				ProfileManager.XmlLocation.Equals(absPath, StringComparison.CurrentCultureIgnoreCase))
				return false;
			try
			{
				ProfessionbuddyBot.Debug(
					"Loading Profile :{0}, previous profile was {1}",
					Path,
					ProfileManager.XmlLocation ?? "[No Profile]");
				if (string.IsNullOrEmpty(Path))
				{
					ProfileManager.LoadEmpty();
				}
				else if (!IsLocal)
				{
					var req = WebRequest.Create(Path);
					req.Proxy = null;
					using (WebResponse res = req.GetResponse())
					{
						using (var stream = res.GetResponseStream())
						{
							ProfileManager.LoadNew(stream);
						}
					}
				}
				else if (File.Exists(absPath))
				{
					ProfileManager.LoadNew(absPath, true);
				}
				else
				{
					ProfessionbuddyBot.Warn("{0}: {1}", ProfessionbuddyBot.Instance.Strings["Error_UnableToFindProfile"], Path);
					return false;
				}
			}
			catch (Exception ex)
			{
				ProfessionbuddyBot.Warn("{0}", ex);
				return false;
			}
			return true;
		}

		public override IPBComponent DeepCopy()
		{
			return new LoadProfileAction {Path = Path, ProfileType = ProfileType, IsLocal = IsLocal};
		}

		public override void Reset()
		{
			_loadedProfile = false;
			base.Reset();
		}

	}
}