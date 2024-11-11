using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using PacketDotNet;
using PacketDotNet.Utils.Converters;
using TraceRouting.HostName;
using TraceRouting.Location;
using TraceRouting.Net;

namespace TraceRouting.Trace
{
    public class TcpRouter : ObservableRoute
    {
        private readonly INetworkInterface nic;
        private readonly IHostNameResolver hostNameResolver;
        private readonly ILocationResolver locationResolver;
        private readonly IPAddress destinationIp;
        private readonly ushort destinationPort;
        private readonly int maxHops;
        private readonly int maxRepetitions;
        private readonly bool resolveDns;
        private readonly int timeout;
        private readonly bool resolveLocation;

        public TcpRouter(INetworkInterface nic, IHostNameResolver hostNameResolver,
            ILocationResolver locationResolver, IPAddress destinationIp, ushort destinationPort,
            int maxHops, int maxRepetitions, bool resolveDns, bool resolveLocation, int timeout = 10)
        {
            this.nic = nic;
            this.hostNameResolver = hostNameResolver;
            this.locationResolver = locationResolver;
            this.destinationIp = destinationIp;
            this.destinationPort = destinationPort;
            this.maxHops = maxHops;
            this.maxRepetitions = maxRepetitions;
            this.resolveDns = resolveDns;
            this.timeout = timeout;
            this.resolveLocation = resolveLocation;
        }

        public override void Execute()
        {
            IList<HopInfoEvent> hops = new List<HopInfoEvent>();

            IList<Task> lookupTasks = new List<Task>();

            for (var x = 0; x < maxRepetitions; x++)
            {
                for (var i = 0; i < maxHops; i++)
                {
                    var sourcePort = (ushort)(65535 - maxHops + i);

                    var hop = new HopInfoEvent() { Position = i + 1, IsTarget = false, Latency = -1 };

                    Conversation p;
                    try
                    {
                        p = SendSyn(nic, destinationIp, sourcePort, destinationPort, (uint)i + 1,
                            (uint)timeout).Result;

                        // Send a FIN packet to clean up routers.
                        SendFin(nic, destinationIp, sourcePort);

                        hop.IpAddress = p.InPacket.PayloadPacket.Extract<IPv4Packet>().SourceAddress;
                        hop.Latency = (long)(p.Stop - p.Start).TotalMilliseconds;
                        hop.IsTarget = p.InPacket.PayloadPacket.Extract<IPv4Packet>().Protocol == PacketDotNet.ProtocolType.Tcp;
                    }
                    catch (AggregateException)
                    {
                        // Swallow and report as unknown hop.
                    }

                    EmitEvent(hop);
                    hops.Add(hop);

                    lookupTasks.Add(Task.Run(async () =>
                    {
                        await Task.WhenAll(
                            LookupHostNameAsync(hop.Position, hop.IpAddress),
                            LookupLocationAsync(hop.Position, hop.IpAddress));
                        EmitEvent(new HopCompleted() { Position = hop.Position });
                    }));

                    if (hop.IsTarget) break;
                }
            }

            Task.WhenAll(lookupTasks).Wait();

            EmitCompleted();
        }

        private async Task LookupHostNameAsync(int position, IPAddress ip)
        {
            if (resolveDns && ip != null)
            {
                try
                {
                    var hostname = await hostNameResolver.ResolveToHostNameAsync(ip);
                    var evnt = new DnsEvent()
                    {
                        IpAddress = ip,
                        DnsName = hostname
                    };

                    EmitEvent(evnt);
                }
                catch (SocketException)
                {
                    // Swallow   
                }
            }
        }

        private async Task LookupLocationAsync(int position, IPAddress ip)
        {
            if (resolveLocation && ip != null)
            {
                try
                {
                    var location = await locationResolver.ResolveLocationAsync(ip);
                    var evnt = new LocationEvent()
                    {
                        IpAddress = ip,
                        Location = location
                    };

                    EmitEvent(evnt);
                }
                catch (Exception)
                {
                    // Swallow
                }
            }
        }

        private async Task<Conversation> SendSyn(INetworkInterface nic, IPAddress address,
            ushort sourcePort, ushort destinationPort, uint timeToLive, uint timeout)
        {
            var ipPacket = nic.GenerateIpv4Packet(address);
            ipPacket.TimeToLive = (int)timeToLive;
            ipPacket.FragmentFlags = 0x02; // Don't fragment.
            ipPacket.Id = 1234;
            var tcpPacket = new TcpPacket(sourcePort, destinationPort)
            {
                Synchronize = true,
                SequenceNumber = timeToLive,
                WindowSize = 8192
            };
            ipPacket.PayloadPacket = tcpPacket;
            ipPacket.Checksum = ipPacket.CalculateIPChecksum();
            tcpPacket.Checksum = (ushort)tcpPacket.CalculateTcpChecksum();

            var response = await nic.SendAndReceiveAsync(ipPacket.ParentPacket, packet =>
            {
                var eth = packet.Extract<EthernetPacket>();
                if (eth.Type != EthernetType.IPv4) return false;
                if (!eth.DestinationHardwareAddress.Equals(nic.MacAddress)) return false;

                var ip = eth.Extract<IPv4Packet>();
                if (!ip.DestinationAddress.Equals(nic.IpAddress)) return false;

                if (ip.Protocol == PacketDotNet.ProtocolType.Icmp)
                {
                    var icmp = ip.Extract<IcmpV4Packet>();
                    var sequenceNumber = EndianBitConverter.Big.ToUInt32(icmp.PayloadData, 20 + 4);
                    if (sequenceNumber != timeToLive) return false;
                }
                else if (ip.Protocol == PacketDotNet.ProtocolType.Tcp)
                {
                    if (!ip.SourceAddress.Equals(address)) return false;
                    var tcp = ip.Extract<TcpPacket>();
                    if (tcp.DestinationPort != sourcePort) return false;
                }
                else
                {
                    return false;
                }

                return true;
            }, TimeSpan.FromSeconds(timeout));

            return response;
        }

        private void SendFin(INetworkInterface nic, IPAddress address, ushort port)
        {
            var ipPacket = nic.GenerateIpv4Packet(address);
            ipPacket.TimeToLive = 128;
            var tcpPacket = new TcpPacket(port, 443) { Finished = true, WindowSize = 8192 };
            ipPacket.PayloadPacket = tcpPacket;
            ipPacket.Checksum = ipPacket.CalculateIPChecksum();
            tcpPacket.Checksum = (ushort)tcpPacket.CalculateTcpChecksum();

            nic.Send(ipPacket.ParentPacket);
        }
    }
}
