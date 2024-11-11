using System;
using System.Net;
using System.Threading;

using TraceRouting;
using TraceRouting.Location;

namespace TcpRoute
{
    public class ConsoleRouteObserver : RouteObserver
    {
        private int drawnHops;

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public ConsoleRouteObserver() : base()
        {
            drawnHops = 0;
        }

        private static string RenderIpAddressAndDnsName(IPAddress ipAddress, string dnsName)
        {
            var result = "???";
            if (ipAddress != null) result = ipAddress.ToString();
            if (dnsName != null)
            {
                result = dnsName + $" [{result}]";
            }
            return result;
        }

        private static string RenderLocation(LocationInformation location)
        {
            string result = "";
            if (location != null && location.Country != null)
            {
                string loc = location.Country;
                if (location.City != null)
                {
                    loc = $"{location.City}, {loc}";
                }
                result = $"({loc})";
            }
            return result;
        }

        private static string RenderLatency(long latency)
        {
            string result = "*   ";
            if (latency != -1)
            {
                result = latency + " ms";
            }
            return result;
        }

        protected override void Update()
        {
            _semaphoreSlim.Wait();
            try
            {
                var lastHop = FindGreatestCompletedHop();
                var tableFormatString = FindTableFormatString(lastHop);

                Console.SetCursorPosition(0, Console.CursorTop - drawnHops);

                for (var i = 0; i < lastHop; i++)
                {
                    var hop = Hops[i];
                    if (hop.Results.Count > 0) {
                        var res = hop.Results[0];
                        var latency = RenderLatency(hop.AverageLatency);
                        var ip = RenderIpAddressAndDnsName(res.IpAddress, DnsLookups.GetOrDefault(res.IpAddress));
                        var location = RenderLocation(LocationLookup.GetOrDefault(res.IpAddress));

                        Console.WriteLine(tableFormatString, hop.Position, latency, ip, location);
                    }
                    else
                    {
                        Console.WriteLine(tableFormatString, hop.Position, "0", "?", "?");
                    }
                }
                drawnHops = lastHop;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private string FindTableFormatString(int lastHop)
        {
            int length = 0;
            for (var i = 0; i < lastHop; i++)
            {
                var hop = Hops[i];
                if (hop.Results.Count > 0)
                {
                    var res = hop.Results[0];
                    int thisLength = RenderIpAddressAndDnsName(res.IpAddress, DnsLookups.GetOrDefault(res.IpAddress)).Length;
                    if (length < thisLength) length = thisLength;
                }
            }

            return "{0,3} {1,8}  {2,-" + length + "} {3}";
        }
    }
}
