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
        [XorProperty]
        public string WorkingPath { get; set; } = @".\cache\";

        [XorProperty]
        public string DiffToolPath { get; set; } = string.Empty;

        [XorProperty]
        public string DiffToolArgs { get; set; } = string.Empty;

        [XorProperty]
        public bool WriteSourceToFile { get; set; } = true;
    }
}