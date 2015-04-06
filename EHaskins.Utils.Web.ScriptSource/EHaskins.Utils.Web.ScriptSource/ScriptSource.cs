using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace EHaskins.Utils.Web
{
    public class ScriptSource : IScriptSource
    {
        public string SearchPath { get; set; }
        public string SearchPattern { get; set; }
        public string AbsoluteSearchPath { get; set; }
        public FileSystemWatcher Watcher { get; set; }
        public List<string> Scripts { get; set; }
        public bool IsValid { get; set; }
        private object searchLock = new object();
        public ScriptSource(string searchPath, bool min = false)
        {
            if (min)
                throw new NotImplementedException();
            SearchPath = searchPath;
            SearchPattern = "*.js";
            AbsoluteSearchPath = PathResolver.ResolveAppRelativePath(SearchPath);
            IsValid = false;
            Subscribe();
            Search();
        }
        public IEnumerable<string> GetScripts()
        {
            lock (searchLock)
            {
                if (!IsValid)
                {
                    Search();
                }
            }
            return Scripts;
        }
        private void Search()
        {
            var relativeRoot = new Uri(AbsoluteSearchPath, UriKind.Absolute);
            var files = Search(AbsoluteSearchPath).Select(f =>
            {
                string hash;
                using (var fs = new FileStream(f, FileMode.Open))
                using (var bs = new BufferedStream(fs))
                {
                    using (var sha1 = new SHA1CryptoServiceProvider())
                    {
                        hash = Convert.ToBase64String(sha1.ComputeHash(bs));
                    }
                }
                return new { Path = f, Hash = hash };
            });

            Scripts = files.Select(f => "/" + relativeRoot.MakeRelativeUri(new Uri(f.Path, UriKind.Absolute)).ToString() + "?" + f.Hash.Replace("=", string.Empty).Replace('+', '-').Replace('/', '_')).ToList();
            IsValid = true;
        }
        private IEnumerable<string> Search(string path)
        {
            var dirName = Path.GetFileName(path);
            var files = new List<string>();
            string dirMainFile = Path.Combine(path, dirName + ".js");
            if (File.Exists(dirMainFile))
            {
                files.Add(dirMainFile);
            }
            files.AddRange(Directory.GetFiles(path, SearchPattern).Where(f => f != dirMainFile));
            files.AddRange(Directory.GetDirectories(path).SelectMany(f => Search(f)));
            return files;
        }
        private void Subscribe()
        {
            Watcher = new FileSystemWatcher(AbsoluteSearchPath, SearchPattern);
            Watcher.IncludeSubdirectories = true;
            Watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

            Watcher.Changed += Watcher_Callback;
            Watcher.Created += Watcher_Callback;
            Watcher.Deleted += Watcher_Callback;
            Watcher.Renamed += Watcher_Callback;

            Watcher.Error += Watcher_Error;
            Watcher.EnableRaisingEvents = true;
        }
        private void Unsubscribe()
        {
            if (Watcher != null)
            {
                Watcher.EnableRaisingEvents = false;
                Watcher.Changed -= Watcher_Callback;
                Watcher.Created -= Watcher_Callback;
                Watcher.Deleted -= Watcher_Callback;
                Watcher.Renamed -= Watcher_Callback;

                Watcher.Error -= Watcher_Error;
            }
        }
        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            Unsubscribe();
            Subscribe();
        }
        private void Watcher_Callback(object sender, FileSystemEventArgs e)
        {
            IsValid = false;
        }
    }
}
