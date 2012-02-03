using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Styx.Helpers;

namespace HighVoltz
{
    static public class Updater
    {
        const string PbSvnUrl = "http://professionbuddy.googlecode.com/svn/trunk/Professionbuddy/";
        private const string PbChangeLogUrl = "http://code.google.com/p/professionbuddy/source/detail?r=";

        public static void CheckForUpdate()
        {
            try
            {
                Professionbuddy.Log("Checking for new version");
                int remoteRev = GetRevision();
                if (GlobalPBSettings.Instance.CurrentRevision < remoteRev &&
                    Professionbuddy.Svn.Revision < remoteRev)
                {
                    Professionbuddy.Log("A new version was found.Downloading Update");
                    DownloadFilesFromSvn(new WebClient(), PbSvnUrl);
                    Professionbuddy.Log("Download complete.");
                    GlobalPBSettings.Instance.CurrentRevision = remoteRev;
                    GlobalPBSettings.Instance.Save();
                    Professionbuddy.Log("************* Change Log ****************");
                    Professionbuddy.Log(GetChangeLog(remoteRev));
                    Professionbuddy.Log("*****************************************");
                    Logging.Write(Color.Red, "A new version of ProfessionBuddy was installed. Please restart Honorbuddy");
                }
                else
                {
                    Professionbuddy.Log("No updates found");
                }
            }
            catch (Exception ex)
            {
                Professionbuddy.Err(ex.ToString());
            }
        }

        static int GetRevision()
        {
            var client = new WebClient();
            string html = client.DownloadString(PbSvnUrl);
            var pattern = new Regex(@" - Revision (?<rev>\d+):", RegexOptions.CultureInvariant);
            Match match = pattern.Match(html);
            if (match.Success && match.Groups["rev"].Success)
                return int.Parse(match.Groups["rev"].Value);
            throw new Exception("Unable to retreive revision");
        }

        static Regex _linkPattern = new Regex(@"<li><a href="".+"">(?<ln>.+(?:..))</a></li>", RegexOptions.CultureInvariant);
        static void DownloadFilesFromSvn(WebClient client, string url)
        {
            string html = client.DownloadString(url);
            var results = _linkPattern.Matches(html);

            IEnumerable<Match> matches = from match in results.OfType<Match>()
                                         where match.Success && match.Groups["ln"].Success
                                         select match;
            foreach (Match match in matches)
            {
                string file = RemoveXmlEscapes(match.Groups["ln"].Value);
                string newUrl = url + file;
                if (newUrl[newUrl.Length - 1] == '/') // it's a directory...
                {
                    DownloadFilesFromSvn(client, newUrl);
                }
                else // its a file.
                {
                    string filePath, dirPath;
                    if (url.Length > PbSvnUrl.Length)
                    {
                        string relativePath = url.Substring(PbSvnUrl.Length);
                        dirPath = Path.Combine(Professionbuddy.BotPath, relativePath);
                        filePath = Path.Combine(dirPath, file);
                    }
                    else
                    {
                        dirPath = Environment.CurrentDirectory;
                        filePath = Path.Combine(Professionbuddy.BotPath, file);
                    }
                    Professionbuddy.Debug("Downloading {0}", file);
                    if (!Directory.Exists(dirPath))
                        Directory.CreateDirectory(dirPath);
                    client.DownloadFile(newUrl, filePath);
                }
            }
        }
        static string RemoveXmlEscapes(string xml)
        {
            return xml.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
        }

        static Regex _changelogPattern = new Regex("<h4 style=\"margin-top:0\">Log message</h4>\r?\n?<pre class=\"wrap\" style=\"margin-left:1em\">(?<log>.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?.+\r?\n?)</pre>", RegexOptions.CultureInvariant);
        static string GetChangeLog(int revision)
        {
            var client = new WebClient();
            string html = client.DownloadString(PbChangeLogUrl + revision);
            Match match = _changelogPattern.Match(html);
            if (match.Success && match.Groups["log"].Success)
                return RemoveXmlEscapes(match.Groups["log"].Value);
            return null;
        }
    }
}
