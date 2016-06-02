using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace LateNightStupidities.webdiff
{
    internal class Util
    {
        public static byte[] HashFile(string file)
        {
            try
            {
                using (FileStream fs = File.Open(file, FileMode.Open))
                using (MD5 md5 = MD5.Create())
                {
                    return md5.ComputeHash(fs);
                }
            }
            catch
            {
                return null;
            }
        }

        public static void AddUrlToFile(string file, string url)
        {
            List<string> lines = File.ReadAllLines(file).ToList();
            lines.Insert(0, $"<!-- {url} -->");
            File.WriteAllLines(file, lines);
        }
    }
}