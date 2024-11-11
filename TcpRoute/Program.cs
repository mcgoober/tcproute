using System;
using System.Linq;
using System.Net;

using CommandLine;
using SharpPcap;
using SharpPcap.LibPcap;
using TraceRouting.HostName;
using TraceRouting.Location;
using TraceRouting.Net;
using TraceRouting.Trace;

namespace TcpRoute
{
    public class Program
    {
        [Verb("netroute", HelpText = "Shows the dynamic network route from your localhost to a destination")]
        public class NetRouteOptions
        {
            [Option('l', "nolocation", Required = false,
                HelpText = "Whether to skip trying to resolve the geo-location of ip addresses")]
            public bool NoLocation { get; set; }

            [Option('n', "numeric", Required = false, Default = false,
                HelpText = "Disables DNS resolution of target IPs enroute and displays only IP address.")]
            public bool Numeric { get; set; }

            [Option('i', "interfaceid", Required = false, Default = -1,
                HelpText = "The index of the network interface to use to send out packets.  Not compatible with \"sourceip\" option.")]
            public int InterfaceId { get; set; }

            [Option('m', "maxhops", Required = false, Default = 30,
                HelpText = "The maximum number of hops that will be tried by net route")]
            public int MaxHops { get; set; }

            [Option('r', "maxrepetitions", Required = false, Default = 1,
                HelpText = "The maximum number of repetitions of the route that will be tried")]
            public int MaxRepetitions { get; set; }

            [Option('s', "sourceip", Required = false, Default = "",
                HelpText = "The source IP address from which to send requests from.  By default if this is not supplied the program will scan your network interfaces and use one that can connect to your local gateway.")]
            public string SourceIP { get; set; }

            [Option('t', "timeout", Required = false, Default = 10,
                HelpText = "The timeout for a packet send and recieve operation in seconds")]
            public int Timeout { get; set; }

            [Option('v', "verbose", Required = false, 
                HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Value(0, MetaName = "destination",
                HelpText = "Destination IP or domain name with designated port e.g. 8.8.8.8:443",
                Required = true)]
            public string Destination { get; set; }
        }

        [Verb("info", HelpText = "Gets info about your networking setup on this device")]
        class InfoOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }
        }

        private static int RunNetRouteAndReturnExitCode(NetRouteOptions opts)
        {
            // Parse the destination into IP address and port
            if (!opts.Destination.Contains(':'))
            {
                throw new ArgumentException("Desintation address does not contain a port number", "destination");
            }

            // Check for input device selection options
            if (!opts.SourceIP.Equals("") && opts.InterfaceId > 0)
            {
                throw new ArgumentException("Cannot use both \"--interfaceid\" and \"sourceip\" at the same time.");
            }

            // Get the desintation IP and port
            var dnsResolver = new ReverseCachingHostNameResolver();

            var destParts = opts.Destination.Split(':');
            IPAddress destinationIp = dnsResolver.ResolveToIpAddressAsync(destParts[0]).Result;
            ushort destintationPort = ushort.Parse(destParts[1]);
            

            /* Retrieve the device list */
            INetworkInterface nic = null;

            if (opts.InterfaceId > 0)
            {
                nic = new NetworkInterface(CaptureDeviceList.Instance[opts.InterfaceId]);
            }
            else if (!opts.SourceIP.Equals(""))
            {
                nic = NetworkInterface.CreateFromIp(opts.SourceIP);
            }
            else
            {
                nic = NetworkInterface.Create();
            }

            // Open the device for capturing
            Console.WriteLine($"Tracing routing to {opts.Destination} [{destinationIp}]");
            Console.WriteLine($"over a maximum of {opts.MaxHops} hops:\n");
            nic.Begin();

            // Ensure Gateway is in ARP table
            nic.GetMacFromIp(nic.GatewayIpAddress);

            var locationResolver = new LocationResolver();

            // Run the TCP Route 
            var tcpRoute = new TcpRouter(nic, dnsResolver, locationResolver, destinationIp, 
                destintationPort, opts.MaxHops, opts.MaxRepetitions, !opts.Numeric, !opts.NoLocation, opts.Timeout);
            using (tcpRoute.Subscribe(new ConsoleRouteObserver()))
            {
                tcpRoute.Execute();
            }

            // Close the pcap device
            nic.CloseDevice();

            Console.WriteLine("\nTrace Complete.");

            return 0;
        }

        private static int RunInfoAndReturnExitCode(InfoOptions opts)
        {
            var ver = Pcap.SharpPcapVersion;
            /* Print SharpPcap version */
            Console.WriteLine("SharpPcap {0}, TCPRoute", ver);
            Console.WriteLine();

            /* Retrieve the device list */
            var devices = CaptureDeviceList.Instance;

            /*If no device exists, print error */
            if (devices.Count < 1)
            {
                Console.WriteLine("No device found on this machine");
                return 0;
            }

            Console.WriteLine("The following devices are available on this machine:");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine();

            var n = 0;
            /* Scan the list printing every entry */
            foreach (var dev in devices)
            {
                var winpcap = (LibPcapLiveDevice)dev;
                if (winpcap.Loopback)
                {
                    continue;
                }
                var ips = string.Join(", ", winpcap.Addresses.Where(a => a.Addr.ipAddress != null).Select(a => a.Addr.ipAddress.ToString()));

                /* Description */
                Console.WriteLine("{0}) {1} {2} {3}", n++, dev.Name, dev.Description, ips);
            }

            return 0;
        }

        public static void Main(string[] args)
        {
            var results = CommandLine.Parser.Default.ParseArguments<NetRouteOptions, InfoOptions>(args)
                .MapResult(
                  (NetRouteOptions opts) => RunNetRouteAndReturnExitCode(opts),
                  (InfoOptions opts) => RunInfoAndReturnExitCode(opts),
                  errs => 1);
        }   
    }    
}