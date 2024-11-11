using System;
using System.Threading.Tasks;

using PacketDotNet;

namespace TraceRouting.Net
{
    public class Conversation
    {
        public Conversation(Func<Packet, bool> filter)
        {
            Filter = filter;
        }

        public Func<Packet, bool> Filter { get; }

        public DateTime Start { get; set; }

        public DateTime Stop { get; set; }

        public Packet OutPacket { get; set; }

        public Packet InPacket { get; set; }

        public TimeSpan Timeout { get; set; }

        public TaskCompletionSource<Conversation> CompletionSource { get; } = new();
    }
}
