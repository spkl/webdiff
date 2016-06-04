using LateNightStupidities.XorPersist;
using LateNightStupidities.XorPersist.Attributes;

namespace LateNightStupidities.webdiff.Data
{
    [XorClass(nameof(DbEntry))]
    internal class DbEntry : XorObject
    {
        [XorProperty]
        public string URL { get; set; }

        [XorProperty]
        public string ObsoleteFileName { get; set; }

        [XorProperty]
        public string LastFileName { get; set; }

        public byte[] LastHash { get; set; }

        public string CurrentFileName { get; set; }

        public byte[] CurrentHash { get; set; }

        [XorProperty]
        public string Error { get; set; }
    }
}