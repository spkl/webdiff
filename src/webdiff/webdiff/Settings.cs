using System;
using System.IO;
using System.Reflection;
using LateNightStupidities.XorPersist;
using LateNightStupidities.XorPersist.Attributes;

namespace LateNightStupidities.webdiff
{
    [XorClass(nameof(Settings))]
    internal class Settings : XorObject
    {
        public string WorkingPath { get; } =
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "cache");

        [XorProperty]
        public string DiffToolPath { get; set; } = string.Empty;
    }
}