using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using LateNightStupidities.webdiff.Data;
using LateNightStupidities.XorPersist;

namespace LateNightStupidities.webdiff
{
    class Program
    {
        static void Main(string[] args)
        {
            ShowTitle();
            int exitCode = 0;
            try
            {
                string localDir = Util.AssemblyDir;
                string settingsFileName = Path.Combine(localDir, "settings.xml");
                string urlsFileName = Path.Combine(localDir, "urls.xml");
                string dbFileName = Path.Combine(localDir, "db.xml");

                CreateFilesIfNotExist(settingsFileName, urlsFileName, dbFileName);

                Settings settings;
                List<string> urls;
                Db db;
                Dictionary<string, string> urlFilters;
                LoadSettings(out settings, settingsFileName, urlsFileName, dbFileName, out urls, out db, out urlFilters);

                SetWorkingDir(settings);
                string diffToolPath = GetDiffToolPath(settings);

                DownloadFiles(urls, db, urlFilters, settings);
                PresentResults(db, urls, diffToolPath, settings);
                DeleteObsoleteFiles(db);
                SaveDb(db, dbFileName);
            }
            catch (WebdiffErrorException error)
            {
                using (Util.SetConsoleColor(ConsoleColor.Red))
                {
                    Console.Error.WriteLine("Error: " + error.Message);
                    Console.Error.WriteLine("Code: " + error.ExitCode);
                }

                exitCode = error.ExitCode;
            }
            catch (Exception e)
            {
                using (Util.SetConsoleColor(ConsoleColor.Red))
                {
                    Console.Error.WriteLine("An unexpected internal program error occurred: " + e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                }

                exitCode = -1;
            }

            if (args.Length < 1 || args[0] != "/q")
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            Environment.Exit(exitCode);
        }

        private static void ShowTitle()
        {
            using (Util.SetConsoleColor(ConsoleColor.DarkCyan))
            {
                AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
                Console.WriteLine($"{assemblyName.Name} {assemblyName.Version}, Copyright 2016 Sebastian Fischer");
            }
        }

        private static void SaveDb(Db db, string dbFileName)
        {
            Console.WriteLine();
            Console.Write("Saving data... ");
            XorController.Get().Save(db, dbFileName);
            Console.WriteLine("Done.");
        }

        private static void DeleteObsoleteFiles(Db db)
        {
            lock (db.Lock)
            {
                foreach (DbEntry entry in db.Entries.Where(e => e.Error == null))
                {
                    if (!string.IsNullOrEmpty(entry.ObsoleteFileName) && File.Exists(entry.ObsoleteFileName))
                    {
                        File.Delete(entry.ObsoleteFileName);
                        entry.ObsoleteFileName = entry.LastFileName;
                    }

                    entry.ObsoleteFileName = entry.LastFileName;
                    entry.LastFileName = entry.CurrentFileName;
                }
            }
        }

        private static void PresentResults(Db db, List<string> urls, string diffToolPath, Settings settings)
        {
            lock (db.Lock)
            {
                var errorEntries =
                    db.Entries.Where(e => e.Error != null && !string.IsNullOrEmpty(e.CurrentFileName)).ToList();
                if (errorEntries.Count > 0)
                {
                    using (Util.SetConsoleColor(ConsoleColor.Yellow))
                    {
                        Console.WriteLine();
                        Console.WriteLine("The following errors occurred:");
                        foreach (var errEntry in errorEntries)
                        {
                            Console.WriteLine($"{errEntry.URL}: {errEntry.Error}");
                        }
                    }
                }

                var newEntries =
                    db.Entries.Where(e => e.Error == null && e.LastHash == null && urls.Contains(e.URL)).ToList();
                if (newEntries.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("These URLs are new (no comparison possible):");
                    foreach (DbEntry entry in newEntries)
                    {
                        Console.WriteLine($"\t{entry.URL}");
                    }
                }

                var oldEntries = db.Entries.Where(e => !urls.Contains(e.URL)).ToList();
                if (oldEntries.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("These URLs are no longer in the list and will be deleted:");
                    foreach (DbEntry entry in oldEntries)
                    {
                        Console.WriteLine($"\t{entry.URL}");
                        db.Entries.RemoveAll(e => e.URL == entry.URL);
                        if (!string.IsNullOrEmpty(entry.ObsoleteFileName))
                        {
                            File.Delete(entry.ObsoleteFileName);
                        }
                        if (!string.IsNullOrEmpty(entry.LastFileName))
                        {
                            File.Delete(entry.LastFileName);
                        }
                    }
                }

                var changedEntries =
                    db.Entries.Where(
                        e =>
                            e.Error == null && e.LastHash != null && e.CurrentHash != null &&
                            !e.CurrentHash.SequenceEqual(e.LastHash)).ToList();
                if (changedEntries.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("The files at these URLs changed:");
                    foreach (DbEntry entry in changedEntries)
                    {
                        Console.WriteLine($"\t{entry.URL}");

                        string diffToolArgs = settings.DiffToolArgs;
                        diffToolArgs = diffToolArgs.Replace("{OldFile}", entry.LastFileName);
                        diffToolArgs = diffToolArgs.Replace("{NewFile}", entry.CurrentFileName);
                        diffToolArgs = diffToolArgs.Replace("{OldTitle}",
                            $"{entry.URL} ({new FileInfo(entry.LastFileName).LastWriteTime})");
                        diffToolArgs = diffToolArgs.Replace("{NewTitle}",
                            $"{entry.URL} ({new FileInfo(entry.CurrentFileName).LastWriteTime})");

                        ProcessStartInfo si = new ProcessStartInfo(diffToolPath)
                        {
                            UseShellExecute = false,
                            Arguments = diffToolArgs
                        };
                        Process.Start(si);
                    }
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("No changes detected!");
                }
            }
        }

        private static void DownloadFiles(List<string> urls, Db db, Dictionary<string, string> urlFilters, Settings settings)
        {
            Console.Write("Downloading specified files...");
            Parallel.ForEach(urls, (url, state) =>
            {
                DbEntry entry;
                lock (db.Lock)
                {
                    entry = db.GetEntry(url);
                    if (entry == null)
                    {
                        entry = db.AddEntry(url);
                    }
                }

                entry.CurrentFileName = $"webdiff_{Guid.NewGuid()}.wd";
                try
                {
                    entry.Error = null;

                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(url, entry.CurrentFileName);

                        if (settings.WriteSourceToFile)
                        {
                            Util.AddUrlToFile(entry.CurrentFileName, url);
                        }

                        string filter;
                        if (urlFilters.TryGetValue(url, out filter))
                        {
                            Filters.Filter(entry.CurrentFileName, filter);
                        }

                        entry.CurrentHash = Util.HashFile(entry.CurrentFileName);
                        entry.LastHash = Util.HashFile(entry.LastFileName);
                        Console.Write(".");
                    }
                }
                catch (Exception e)
                {
                    Console.Write("E");
                    entry.Error = e.Message;
                }
            });
            Console.Write(" Done.");
            Console.WriteLine();
        }

        private static string GetDiffToolPath(Settings settings)
        {
            if (string.IsNullOrEmpty(settings.DiffToolPath))
            {
                throw new WebdiffErrorException(
                    "The path to the diff tool is not set. Edit settings.xml and set DiffToolPath.", 4);
            }

            string diffToolPath = Util.GetRootedPath(settings.DiffToolPath);
            if (!File.Exists(diffToolPath))
            {
                throw new WebdiffErrorException("The diff tool does not exist: " + diffToolPath, 5);
            }
            return diffToolPath;
        }

        private static void SetWorkingDir(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.WorkingPath))
            {
                throw new WebdiffErrorException(
                    "The working path is not set. Check settings.xml and set WorkingPath.", 3);
            }

            string workingDir = Path.GetFullPath(Util.GetRootedPath(settings.WorkingPath));
            Console.WriteLine($"Working path: {workingDir}.");
            Directory.CreateDirectory(workingDir);
            Environment.CurrentDirectory = workingDir;
        }

        private static void LoadSettings(out Settings settings, string settingsFileName, string urlsFileName, string dbFileName, out List<string> urls, out Db db, out Dictionary<string, string> urlFilters)
        {
            Console.Write("Loading settings... ");
            settings = XorController.Get().Load<Settings>(settingsFileName);
            urls = XorController.Get().Load<Urls>(urlsFileName).URLs;
            db = XorController.Get().Load<Db>(dbFileName);

            urlFilters = new Dictionary<string, string>();
            foreach (string url in urls.ToList())
            {
                if (!url.Contains("|"))
                {
                    continue;
                }

                string[] split = url.Split('|');
                string realUrl = split[0];
                string filter = split[1];

                urls.Remove(url);
                urls.Add(realUrl);
                urlFilters[realUrl] = filter;
            }

            urls = urls.Distinct().ToList();

            Console.WriteLine("Done.");
        }

        private static void CreateFilesIfNotExist(string settingsFileName, string urlsFileName, string dbFileName)
        {
            bool createdFile = false;

            if (!File.Exists(settingsFileName))
            {
                createdFile = true;
                Console.WriteLine($"Creating {settingsFileName}...");
                XorController.Get().Save(new Settings(), settingsFileName);
            }

            if (!File.Exists(urlsFileName))
            {
                createdFile = true;
                Console.WriteLine($"Creating {urlsFileName}...");
                var newUrls = new Urls();
                newUrls.URLs.Add("http://add");
                newUrls.URLs.Add("http://your");
                newUrls.URLs.Add("http://URLs");
                newUrls.URLs.Add("http://here");
                XorController.Get().Save(newUrls, urlsFileName);
            }

            if (!File.Exists(dbFileName))
            {
                createdFile = true;
                Console.WriteLine($"Creating {dbFileName}...");
                XorController.Get().Save(new Db(), dbFileName);
            }

            if (createdFile)
            {
                throw new WebdiffErrorException(
                    "A required file did not exist and was created. Check file contents and try again.", 2);
            }
        }
    }
}
