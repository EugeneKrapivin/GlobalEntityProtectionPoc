using System;
using System.Collections.Generic;

namespace Config
{
    public class KafkaBrokersConfig
    {
        public List<string> Brokers { get; set; } = new List<string>();
    }
}
