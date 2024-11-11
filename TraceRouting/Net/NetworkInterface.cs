using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Linq;

using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace TraceRouting.Net
{
    public class NetworkInterface : INetworkInterface
    {
        private readonly ILiveDevice _captureDevice;

        private readonly PcapAddress _ip;

        private readonly PcapInterface _interface;

        private readonly Dictionary<IPAddress, PhysicalAddress> ArpTable = [];

        private readonly List<Conversation> _conversationList = [];

        public NetworkInterface(ILiveDevice captureDevice)
        {
            _captureDevice = captureDevice;

            int readTimeoutMilliseconds = 1000;
            _captureDevice.Open(DeviceModes.Promiscuous, readTimeoutMilliseconds);

            _captureDevice.Filter = "ip or arp";
            _captureDevice.OnPacketArrival += OnPacketArrival;

            _ip = ((LibPcapLiveDevice)_captureDevice).Addresses.Where(a => a.Addr.ipAddress != null && a.Addr.ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();
            _interface = ((LibPcapLiveDevice)_captureDevice).Interface;
        }

        public static NetworkInterface CreateFromIp(string sourceIp)
        { 
            // Retrieve the device list
            var devices = CaptureDeviceList.Instance;

            // If no device exists, print error
            if (devices.Count < 1)
            {
                throw new InvalidProgramException("No network devices found on this host");
            }

            ILiveDevice device = null;
            // Scan the list printing every entry
            foreach (var dev in devices)
            {
                var winpcap = (LibPcapLiveDevice)dev;
                if (winpcap.Loopback)
                {
                    continue;
                }

                var ips = winpcap.Addresses.Where(a => a.Addr.ipAddress != null).Select(a => a.Addr.ipAddress.ToString());
                
                foreach (var ip in ips)
                {
                    if (ip.Equals(sourceIp))
                    {
                        device = dev;
                        break;
                    }
                }
            }

            if (device == null)
            {
                throw new InvalidProgramException("Can't find a device with the specified IP: " + sourceIp);
            }

            return new NetworkInterface(device);
        }

        public static NetworkInterface Create()
        {
            // Retrieve the device list
            var devices = CaptureDeviceList.Instance;

            // If no device exists, print error
            if (devices.Count < 1)
            {
                throw new InvalidProgramException("No network devices found on this host");
            }

            ILiveDevice device = null;
            // Scan the list printing every entry
            foreach (var dev in devices)
            {
                var winpcap = (LibPcapLiveDevice)dev;
                if (winpcap.Loopback)
                {
                    continue;
                }

                if (winpcap.Interface.GatewayAddresses != null && winpcap.Interface.GatewayAddresses.Count > 0)
                {
                    device = dev;
                    break;
                }
            }

            return new NetworkInterface(device);
        }

        public string GetDeviceDescription()
        {
            return this._captureDevice.Description;
        }

        public void CloseDevice()
        {
            this._captureDevice.Close();
        }

        public PhysicalAddress MacAddress => _captureDevice.MacAddress;

        public IPAddress IpAddress => _ip.Addr.ipAddress;

        public IPAddress GatewayIpAddress => _interface.GatewayAddresses?[0];

        //public Task<PhysicalAddress> LookupMacAddressAsync(IPAddress address)

        public void Begin()
        {
            int readTimeoutMilliseconds = 1000;
            _captureDevice.Open(DeviceModes.Promiscuous, readTimeoutMilliseconds);

            _captureDevice.StartCapture();
        }

        public void Send(Packet packet)
        {
            _captureDevice.SendPacket(packet);
        }

        public Task<Conversation> SendAndReceiveAsync(Packet packet, Func<Packet, bool> filter, TimeSpan timeout)
        {
            var conversation = new Conversation(filter);

            // Insert filter
            _conversationList.Add(conversation);

            conversation.OutPacket = packet;
            conversation.Timeout = timeout;

            // Send packet
            conversation.Start = DateTime.UtcNow;
            _captureDevice.SendPacket(packet);

            // Wait for result
            return conversation.CompletionSource.Task;
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            var p = e.GetPacket();
            var time = p.Timeval.Date;
            var len = p.Data.Length;

            var packet = Packet.ParsePacket(p.LinkLayerType, p.Data);
            IList<Conversation> completeConversations = new List<Conversation>();
            foreach (var conversation in _conversationList)
            {
                var duration = DateTime.UtcNow - conversation.Start;
                if (duration > conversation.Timeout)
                {
                    completeConversations.Add(conversation);
                    conversation.CompletionSource.SetException(new TimeoutException());
                }
                if (conversation.Filter.Invoke(packet))
                {
                    completeConversations.Add(conversation);
                    conversation.InPacket = packet;
                    conversation.Stop = time;
                    conversation.CompletionSource.SetResult(conversation);
                }
            }
            foreach (var conversation in completeConversations)
            {
                _conversationList.Remove(conversation);
            }
        }

        public static PhysicalAddress MacBroadcast = PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF");

        public PhysicalAddress GetMacFromIp(IPAddress ip)
        {
            // If the IP address is in our subnet then we can ARP directly. 
            // If not then we need the gateway IP, cos it's going to be routed.
            ip = ip.IsInSameSubnet(_ip.Addr.ipAddress, _ip.Netmask.ipAddress) ? ip : _interface.GatewayAddresses[0];

            if (ArpTable.TryGetValue(ip, out var physicalAddress))
            {
                return physicalAddress;
            }

            var p = GenerateEthernetPacket(MacBroadcast, EthernetType.Arp);
            p.PayloadPacket = new ArpPacket(ArpOperation.Request, 
                PhysicalAddress.Parse("00-00-00-00-00-00"), ip, MacAddress, _ip.Addr.ipAddress);

            var a = SendAndReceiveAsync(p, packet =>
            {
                var eth = packet.Extract<EthernetPacket>();
                if (eth.Type != EthernetType.Arp) return false;
                if (!eth.DestinationHardwareAddress.Equals(MacAddress)) return false;
                var arp = eth.PayloadPacket.Extract<ArpPacket>();
                return true;
            }, TimeSpan.FromSeconds(10)).Result;

            var mac = a.InPacket.PayloadPacket.Extract<ArpPacket>().SenderHardwareAddress;
            ArpTable.Add(ip, mac);
            return mac;
        }

        public EthernetPacket GenerateEthernetPacket(PhysicalAddress destination, EthernetType packetType)
        {
            return new EthernetPacket(MacAddress, destination, EthernetType.IPv4);
        }

        public IPv4Packet GenerateIpv4Packet(IPAddress destination)
        {
            var ep = GenerateEthernetPacket(GetMacFromIp(destination), EthernetType.IPv4);
            var ip = new IPv4Packet(IpAddress, destination);
            ep.PayloadPacket = ip;
            
            return ip;
        }
    }
}