using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace LateNightStupidities.webdiff
{
    internal static class Filters
    {
        public static void Filter(string file, string filter)
        {
            MethodInfo method = typeof(Filters).GetMethod($"Filter_{filter}",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new Exception($"There is no filter method with name {filter}.");
            }

            method.Invoke(null, new object[] { file });
        }

        private static void Filter_DTSTAMP(string file)
        {
            string[] lines = File.ReadAllLines(file);
            List<string> newLines = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (!line.StartsWith("DTSTAMP:"))
                {
                    newLines.Add(line);
                }
            }

            File.WriteAllLines(file, newLines);
        }

        private static void Filter_lastBuildDate(string file)
        {
            string[] lines = File.ReadAllLines(file);
            List<string> newLines = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("<lastBuildDate>") && trimmed.EndsWith("</lastBuildDate>"))
                {
                    continue;
                }

                newLines.Add(line);
            }

            File.WriteAllLines(file, newLines);
        }
    }
}