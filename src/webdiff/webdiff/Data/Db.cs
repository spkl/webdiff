using System;
using System.Collections.Generic;
using System.Linq;
using LateNightStupidities.XorPersist;
using LateNightStupidities.XorPersist.Attributes;

namespace LateNightStupidities.webdiff.Data
{
    [XorClass(nameof(Db))]
    internal class Db : XorObject
    {
        public object Lock => this.Entries;

        [XorProperty(XorMultiplicity.List)]
        public List<DbEntry> Entries { get; set; } = new List<DbEntry>();

        public DbEntry GetEntry(string url)
        {
            return this.Entries.FirstOrDefault(e => e.URL == url);
        }

        public DbEntry AddEntry(string url)
        {
            if (this.GetEntry(url) != null)
            {
                throw new Exception($"Duplicate key {url}.");
            }

            DbEntry entry = new DbEntry { URL = url };
            this.Entries.Add(entry);
            return entry;
        }
    }
}