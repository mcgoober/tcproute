using System.Net;
using System.Threading.Tasks;

namespace TraceRouting.HostName
{
    public interface IHostNameResolver
    {
        Task<string> ResolveToHostNameAsync(string ip);

        Task<string> ResolveToHostNameAsync(IPAddress ip);

        Task<IPAddress> ResolveToIpAddressAsync(string hostname);

        Task<IPAddress[]> ResolveToIpAddressesAsync(string hostname);
    }
}
