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
            Console.WriteLine($"webdiff {Assembly.GetExecutingAssembly().GetName().Version}, Copyright 2016 Sebastian Fischer");
            int exitCode = 0;

            string localDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsFileName = Path.Combine(localDir, "settings.xml");
            string urlsFileName = Path.Combine(localDir, "urls.xml");
            string dbFileName = Path.Combine(localDir, "db.xml");

            if (CreateFilesIfNotExist(settingsFileName, urlsFileName, dbFileName))
            {
                exitCode = 2;
            }
            else
            {
                Console.Write("Loading settings... ");
                var settings = XorController.Get().Load<Settings>(settingsFileName);
                var urls = XorController.Get().Load<Urls>(urlsFileName).URLs;
                var db = XorController.Get().Load<Db>(dbFileName);
                Console.WriteLine("Done.");

                Console.WriteLine($"Working path: {settings.WorkingPath}.");
                Directory.CreateDirectory(settings.WorkingPath);
                Environment.CurrentDirectory = settings.WorkingPath;

                Console.Write("Downloading specified files...");
                Parallel.ForEach(urls, (url, state) =>
                {
                    DbEntry entry;
                    lock (Db.Lock)
                    {
                        entry = db.GetEntry(url);
                        if (entry == null)
                        {
                            entry = db.AddEntry(url);
                        }
                    }

                    entry.CurrentFileName = $"webdiff{Guid.NewGuid()}.tmp";
                    try
                    {
                        entry.Error = null;

                        using (WebClient wc = new WebClient())
                        {
                            wc.DownloadFile(url, entry.CurrentFileName);
                            Util.AddUrlToFile(entry.CurrentFileName, url);
                            Console.Write(".");
                            entry.CurrentHash = Util.HashFile(entry.CurrentFileName);
                            entry.LastHash = Util.HashFile(entry.LastFileName);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Write("E");
                        entry.Error = e.Message;
                    }
                });
                Console.WriteLine();

                lock (Db.Lock)
                {
                    var errorEntries = db.Entries.Where(e => e.Error != null && !string.IsNullOrEmpty(e.CurrentFileName)).ToList();
                    if (errorEntries.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("The following errors occurred:");
                        foreach (var errEntry in errorEntries)
                        {
                            Console.WriteLine($"{errEntry.URL}: {errEntry.Error}");
                        }
                    }

                    var newEntries = db.Entries.Where(e => e.Error == null && e.LastHash == null && urls.Contains(e.URL)).ToList();
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
                        bool openDiffTool = !string.IsNullOrEmpty(settings.DiffToolPath);

                        Console.WriteLine();
                        Console.WriteLine("The files at these URLs changed:");
                        foreach (DbEntry entry in changedEntries)
                        {
                            Console.WriteLine($"\t{entry.URL}");
                            if (openDiffTool)
                            {
                                ProcessStartInfo si = new ProcessStartInfo("cmd.exe");
                                si.ErrorDialog = true;
                                si.UseShellExecute = false;
                                si.CreateNoWindow = true;
                                si.Arguments = string.Format("/c \"{0} \"{1}\" \"{2}\"\"", settings.DiffToolPath,
                                    Path.Combine(Environment.CurrentDirectory, entry.LastFileName),
                                    Path.Combine(Environment.CurrentDirectory, entry.CurrentFileName));
                                Process.Start(si);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("No changes detected!");
                    }

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

                Console.WriteLine();
                Console.Write("Saving data... ");
                XorController.Get().Save(db, dbFileName);
                Console.WriteLine("Done.");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

        private static bool CreateFilesIfNotExist(string settingsFileName, string urlsFileName, string dbFileName)
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

            return createdFile;
        }
    }
}
