using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace TraceRouting.HostName
{
    public class ReverseCachingHostNameResolver : HostNameResolver
    {
        private readonly Dictionary<string, IPHostEntry> hostEntryCache = [];

        protected override async Task<IPHostEntry> GetHostEntryAsync(string ipOrHost)
        {
            // Use cache if it exists
            if (hostEntryCache.TryGetValue(ipOrHost, out var entry))
            {
                return entry;
            }

            var hostEntry = await base.GetHostEntryAsync(ipOrHost);

            // Add each address into cache
            foreach(var ip in hostEntry.AddressList)
            {
                hostEntryCache.Add(ip.ToString(), hostEntry);
            }
            return hostEntry;
        }

        protected override async Task<IPHostEntry> GetHostEntryAsync(IPAddress ip)
        {
            // Use cache if it exists
            if (hostEntryCache.TryGetValue(ip.ToString(), out var entry))
            {
                return entry;
            }
            return await base.GetHostEntryAsync(ip);
        }
    }
}
