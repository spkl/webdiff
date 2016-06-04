using System.Collections.Generic;
using LateNightStupidities.XorPersist;
using LateNightStupidities.XorPersist.Attributes;

namespace LateNightStupidities.webdiff
{
    [XorClass(nameof(Urls))]
    internal class Urls : XorObject
    {
        [XorProperty(XorMultiplicity.List)]
        public List<string> URLs { get; set; } = new List<string>();
    }
}