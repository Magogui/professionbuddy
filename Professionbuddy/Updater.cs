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

        public static void CheckForUpdate()
        {
            try
            {
                Professionbuddy.Log("Checking for new version");
                int remoteRev = GetRevision();
                if (Professionbuddy.Instance.MySettings.CurrentRevision < remoteRev && 
                    Professionbuddy.Svn.Revision < remoteRev)
                {
                    Professionbuddy.Log("A new version was found.Downloading Update");
                    DownloadFilesFromSvn(new WebClient(), PbSvnUrl);
                    Professionbuddy.Log("Download complete.");
                    Professionbuddy.Instance.MySettings.CurrentRevision = remoteRev;
                    Professionbuddy.Instance.MySettings.Save();
                    Logging.Write(Color.Red,"A new version of ProfessionBuddy was installed. Please restart Honorbuddy");
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
                    Professionbuddy.Log("Downloading {0}", file);
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
    }
}
