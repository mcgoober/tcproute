using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

using TraceRouting.Trace;
using TraceRouting.Location;

namespace TraceRouting
{
    public abstract class RouteObserver : IObserver<HopEvent>
    {
        protected List<HopInformation> Hops;

        protected ConcurrentDictionary<IPAddress, string> DnsLookups;

        protected ConcurrentDictionary<IPAddress, LocationInformation> LocationLookup;

        public RouteObserver()
        {
            Hops = new List<HopInformation>();
            DnsLookups = new ConcurrentDictionary<IPAddress, string>();
            LocationLookup = new ConcurrentDictionary<IPAddress, LocationInformation>();
        }

        public virtual void OnCompleted()
        {
            Update();
        }

        public virtual void OnError(Exception error)
        {
            // TODO: handle errors. 
        }

        public virtual void OnNext(HopEvent value)
        {
            if (value is PositionalEvent posEvent)
            {
                EnsureHopStorage(posEvent.Position);
                var hop = Hops[posEvent.Position - 1];

                if (value is HopInfoEvent hopInfo)
                {
                    hop.Position = posEvent.Position;
                    hop.Results.Add(new HopResult() { IpAddress = hopInfo.IpAddress, Latency = hopInfo.Latency, IsTargetHop = hopInfo.IsTarget });
                }
            }
            else if (value is IpEvent ipEvent)
            {
                if (value is DnsEvent dnsInfo)
                {
                    if (!DnsLookups.ContainsKey(dnsInfo.IpAddress))
                    {
                        DnsLookups.TryAdd(dnsInfo.IpAddress, dnsInfo.DnsName);
                    }
                }

                if (value is LocationEvent locationInfo)
                {
                    if (!LocationLookup.ContainsKey(locationInfo.IpAddress))
                    {
                        LocationLookup.TryAdd(locationInfo.IpAddress, locationInfo.Location);
                    }                    
                }
            }
            
            Update();
        }

        private void EnsureHopStorage(int hops)
        {
            lock(Hops)
            {
                var hopDifference = hops - Hops.Count;
                if (hopDifference > 0)
                {
                    for (var i = 0; i < hopDifference; i++)
                    {
                        Hops.Add(new HopInformation());
                    }
                }
            }
        }

        protected abstract void Update();

        protected int FindGreatestCompletedHop()
        {
            for (var i = 0; i < Hops.Count; i++)
            {
                if (Hops[i].Position == 0) return i;                
            }
            return Hops.Count;
        }
    }
}
