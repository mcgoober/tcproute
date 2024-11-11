using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace TraceRouting.HostName
{
    public class HostNameResolver : IHostNameResolver
    {
        protected async virtual Task<IPHostEntry> GetHostEntryAsync(string ipOrHost)
        {
            return await Dns.GetHostEntryAsync(ipOrHost);
        }

        protected async virtual Task<IPHostEntry> GetHostEntryAsync(IPAddress ip)
        {
            return await Dns.GetHostEntryAsync(ip);
        }

        public async Task<string> ResolveToHostNameAsync(string ip)
        {
            return (await GetHostEntryAsync(ip)).HostName;
        }

        public async Task<string> ResolveToHostNameAsync(IPAddress ip)
        {
            return (await GetHostEntryAsync(ip)).HostName;
        }

        public async Task<IPAddress> ResolveToIpAddressAsync(string ipOrHost)
        {
            return (await ResolveToIpAddressesAsync(ipOrHost)).FirstOrDefault();
        }

        public async Task<IPAddress[]> ResolveToIpAddressesAsync(string ipOrHost)
        {
            if (IPAddress.TryParse(ipOrHost, out var ip))
            {
                return [ip];
            }
            return (await GetHostEntryAsync(ipOrHost)).AddressList;
        }
    }
}
