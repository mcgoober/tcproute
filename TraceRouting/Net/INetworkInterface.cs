using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

using PacketDotNet;

namespace TraceRouting.Net
{
    public interface INetworkInterface
    {
        void Begin();

        PhysicalAddress MacAddress { get; }

        IPAddress IpAddress { get; }

        IPAddress GatewayIpAddress { get; }

        PhysicalAddress GetMacFromIp(IPAddress ip);

        string GetDeviceDescription();

        void CloseDevice();

        IPv4Packet GenerateIpv4Packet(IPAddress destination);

        void Send(Packet packet);

        Task<Conversation> SendAndReceiveAsync(Packet packet, Func<Packet, bool> filter, TimeSpan timeout);
    }
}