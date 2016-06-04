using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LateNightStupidities.webdiff
{
    internal static class Filters
    {
        public static void Filter(string file, string filter)
        {
            if (filter.Contains(";"))
            {
                string[] split = filter.Split(';');
                string assemblyFile = split[0];
                string typeName = split[1];
                string methodName = split[2];

                Assembly assembly = Assembly.LoadFrom(Util.GetRootedPath(assemblyFile));
                Type type = assembly.GetType(typeName);
                if (type == null)
                {
                    throw new Exception($"Filter error. Type {typeName} was not found in the specified assembly.");
                }

                MethodInfo method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] {typeof(string)},
                    null);
                if (method == null)
                {
                    throw new Exception($"Filter error. Method {methodName} (static, one string argument) was not found on type {typeName}.");
                }

                method.Invoke(null, new object[] { file });
            }
            else
            {
                MethodInfo method = typeof(Filters).GetMethod($"Filter_{filter}",
                BindingFlags.NonPublic | BindingFlags.Static);

                if (method == null)
                {
                    throw new Exception($"There is no builtin filter method with name {filter}.");
                }

                method.Invoke(null, new object[] { file });
            }
        }

        private static void Filter_DTSTAMP(string file)
        {
            FilterLineBased(file, "DTSTAMP:", null, false);
        }

        private static void Filter_lastBuildDate(string file)
        {
            FilterLineBased(file, "<lastBuildDate>", "</lastBuildDate>", true);
        }

        private static void Filter_updated(string file)
        {
            FilterLineBased(file, "<updated>", "</updated>", true);
        }

        private static void FilterLineBased(string file, string startsWith, string endsWith, bool trim)
        {
            string[] lines = File.ReadAllLines(file);
            List<string> newLines = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                string value = line;

                if (trim)
                {
                    value = value.Trim();
                }

                if ((startsWith == null || value.StartsWith(startsWith)) &&
                    (endsWith == null || value.EndsWith(endsWith)))
                {
                    continue;
                }

                newLines.Add(line);
            }

            File.WriteAllLines(file, newLines);
        }
    }
}