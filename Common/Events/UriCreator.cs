using System;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Events
{
    public static class UriCreator
    {
        public static Uri Create(params (string header, string value)[] parts)
        {
            var builder = new UriBuilder
            {
                Scheme = "cdp",
                Path = parts.Aggregate(
                        new StringBuilder(),
                        (stringBuilder, pair) => stringBuilder.Append($"{pair.header}/{pair.value}/"))
                    .ToString()
            };

            return builder.Uri;
        }
    }
}
