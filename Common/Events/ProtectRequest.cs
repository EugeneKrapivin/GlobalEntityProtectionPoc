using System;
using System.Collections.Generic;

namespace Events
{
    public class ProtectRequest
    {
        public ProtectRequest(Uri source, Uri target)
        {
            Source = source;
            Target = target;
        }

        public Uri Source { get; set; }
        
        public Uri Target { get; set; }
        
        public DateTime RequestAt { get; set; } = DateTime.UtcNow;

        public List<string> Comments { get; set; } = new List<string>();
    }
}