using System.Net;
using System.Threading.Tasks;

namespace TraceRouting.Location
{
    public interface ILocationResolver
    {
        Task<LocationInformation> ResolveLocationAsync(IPAddress ip);
    }
}
