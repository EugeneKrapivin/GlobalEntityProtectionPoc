using System;

namespace Events
{
    public class UnprotectRequest
    {
        public UnprotectRequest(Uri source, Uri target)
        {
            Source = source;
            Target = target;
        }

        public Uri Source { get; set; }

        public Uri Target { get; set; }
    }
}