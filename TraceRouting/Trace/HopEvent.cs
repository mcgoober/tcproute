using System.Net;

using TraceRouting.Location;

namespace TraceRouting.Trace
{
    public class HopEvent { }

    public class PositionalEvent : HopEvent
    {
        public int Position { get; set; }        
    }

    public class HopInfoEvent : PositionalEvent
    {
        public IPAddress IpAddress { get; set; }

        public long Latency { get; set; }

        public bool IsTarget { get; set; }
    }

    public class IpEvent : HopEvent
    {
        public IPAddress IpAddress { get; set; }
    }

    public class DnsEvent : IpEvent
    {
        public string DnsName { get; set; }
    }

    public class LocationEvent : IpEvent
    {
        public LocationInformation Location { get; set; }

    }

    public class HopCompleted : PositionalEvent { }
}
