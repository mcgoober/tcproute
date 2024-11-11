using System.Net;

namespace TraceRouting.Trace
{
    public class HopResult
    { 
        public IPAddress IpAddress { get; set; }

        public long Latency { get; set; }

        public bool IsTargetHop { get; set; }
    }
}
