using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Winista.Text.HtmlParser;
using Winista.Text.HtmlParser.Tags;
using Winista.Text.HtmlParser.Filters;
using Winista.Text.HtmlParser.Util;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;

namespace NuGetDown
{
    public class DownNupkg
    {

        static public string PackageUrlFormat = "https://www.nuget.org/packages/{0}/";

        static public string VersionTableBeginHTML = "<table class=\"sexy-table\">";
        static public string VersionTableEndHTML = "</table>";
        static public string RealVersionUrl = "https://api.nuget.org/packages/{0}.{1}.nupkg";
        static public string NupkgFileName = "{0}.{1}.nupkg";

        string packageName;
        DirectoryInfo NuPkgFileOutFolder;

        string packageUrl;
        List<String> versionList;

        public DownNupkg(string packageName, DirectoryInfo nuPkgFileOutFolder)
        {

            this.packageName = packageName;
            packageUrl = string.Format(PackageUrlFormat, this.packageName);
            Console.WriteLine("##Package Version Url : {0}", packageUrl);
        }

        public void GetAllVersion()
        {
            Console.WriteLine("#Begin Get All Version#");
            versionList = new List<string>();
            byte[] pageData;
            try
            {
                Console.WriteLine("#Get Version Page#");
                var c = new WebClient();
                pageData = c.DownloadData(packageUrl);
            }
            catch (Exception e)
            {
                Console.WriteLine("get all version is error from package page url ");
                return;
            }

            if (pageData.Length == 0)
            {
                Console.WriteLine("can't get package url page content.");
                return;
            }
            string pageHtml = Encoding.UTF8.GetString(pageData);
            var beginIndex = pageHtml.IndexOf(VersionTableBeginHTML);
            if (beginIndex <= 0)
            {
                Console.WriteLine("can't find version talbe begin html section in package url page content.");
                return;
            }
            Console.WriteLine("#Get HTML Version Table#");
            var versionTable = pageHtml.Substring(beginIndex);
            var endIndex = versionTable.IndexOf(VersionTableEndHTML);
            if (endIndex <= 0)
            {
                Console.WriteLine("can't find version talbe end html section in package url page content.");
                return;
            }
            //clean version table
            versionTable = versionTable.Substring(0, endIndex + VersionTableEndHTML.Length);
            //get a tag in  version table 
            Console.WriteLine("#Get a tag in HTML Version Table#");
            AutoResetEvent waitHandler = new AutoResetEvent(false);
            waitHandler.Set();
            var parser = Parser.CreateParser(versionTable, null);
            var nodes = parser.ExtractAllNodesThatMatch(new TagNameFilter("a"));
            waitHandler.WaitOne();
            Console.WriteLine("#Extract Version#");
            for (int i = 0; i < nodes.Count; i++)
            {
                var linkStr = (nodes[i] as ATag).Link;
                var lastSpanIndex = linkStr.LastIndexOf('/');
                var version = linkStr.Substring(lastSpanIndex + 1);
                version = version.Trim();

                if (version.Length <= 0 || version.Contains("-pre"))
                    continue;
                //Console.WriteLine("get version : {0}", version);
                versionList.Add(version);
            }
            Console.WriteLine("#version info count : {0}#", versionList.Count);
        }

        public void Down()
        {
            Console.WriteLine("#Begin Down nupkg File#");
            if (versionList == null || versionList.Count <= 0)
            {
                Console.WriteLine("version info is not exist.");
                return;
            }
            AutoResetEvent waitHandler = new AutoResetEvent(false);
            foreach (var version in versionList)
            {


                var url = string.Format(RealVersionUrl, packageName.ToLower(), version);
                var filename = string.Format(NupkgFileName, packageName.ToLower(), version);
                var fullpath = NuPkgFileOutFolder + filename;
                FileInfo f = new FileInfo(fullpath);
                if (f.Exists)
                {
                    Console.WriteLine("# {0} exist.", fullpath);
                    continue;
                }
                Console.Write("#{0} downloading...", filename);
                string message = string.Empty;
                try
                {
                    waitHandler.Set();
                    using (var c = new WebClient())
                    {
                        c.DownloadFile(new Uri(url, UriKind.Absolute), fullpath);
                    }
                    waitHandler.WaitOne();
                }
                catch (Exception ex)
                {
                    message = string.Format("{0} downloading error: {1}", version, ex.Message);
                }
                if (string.IsNullOrEmpty(message))
                    message = string.Format("{0} down OK.", version);
                Console.WriteLine(message);
                var r = new Random();
                var time = r.Next(5, 10);
                System.Threading.Thread.Sleep(time * 1000);

                //////////////////////////////////
                //multi thread down
                //var bg = new BackgroundWorker();
                //bg.DoWork += Bg_DoWork;
                //bg.RunWorkerCompleted += Bg_RunWorkerCompleted;
                //bg.RunWorkerAsync(version);
            }


        }

        private void Bg_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine(e.Result.ToString());
        }

        private void Bg_DoWork(object sender, DoWorkEventArgs e)
        {
            var version = e.Argument.ToString();
            var url = string.Format(RealVersionUrl, packageName.ToLower(), version);
            var filename = string.Format(NupkgFileName, packageName.ToLower(), version);
            var fullpath = NuPkgFileOutFolder + filename;
            e.Result = "";
            try
            {
                using (var c = new WebClient())
                {
                    c.DownloadFileAsync(new Uri(url, UriKind.Absolute), fullpath);
                }
            }
            catch (Exception ex)
            {
                e.Result = string.Format("{0} downloading error: {1}", version, ex.Message);
            }
            if (e.Result.ToString() == "")
                e.Result = string.Format("{0} down OK.", version);
        }
    }


    class Program
    {


        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("call format 1 : NuGetDown packagename nupkgTargetFolder");
                Console.WriteLine("call format 2 : NuGetDown -f packagefilename");
                return;
            }

            DirectoryInfo dir;

            if (args[0] == "-f")
            {
                Console.WriteLine("####down package from file");
                if (args.Length != 2)
                {
                    Console.WriteLine("-f need valid filename");
                    return;
                }

                var fileFullPath = new FileInfo(System.Environment.CurrentDirectory + "\\" + args[1]);
                Console.WriteLine("package file full path : {0}", fileFullPath);
                if (!fileFullPath.Exists)
                {
                    Console.WriteLine("packagefilename must exist");
                    return;
                }
                dir = new DirectoryInfo(System.Environment.CurrentDirectory);
                var packages = File.ReadAllLines(fileFullPath.FullName);
                for (int i = 0; i < packages.Length; i++)
                {
                    var packageName = packages[i];
                    Console.WriteLine("# down package : {0} {1}", i, packageName);
                    var d = new DownNupkg(packageName, dir);
                    d.GetAllVersion();
                    d.Down();
                }
                return;
            }
            else
            {
                Console.WriteLine("####down one package");
                if (args.Length == 1)
                    dir = new DirectoryInfo(System.Environment.CurrentDirectory);
                else
                    dir = new DirectoryInfo(args[1]);
                if (!dir.Exists)
                {
                    Console.WriteLine("nupkg target folder is not exist");
                    return;
                }
                var d = new DownNupkg(args[0], dir);

                d.GetAllVersion();
                d.Down();

            }
            Console.WriteLine("#END#");
        }
    }


}
