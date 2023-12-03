using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CohoistChat.Shared
{
    public enum MessageTypeEnum { Connected, Disconnected, Info, Me, NotMe }
    public class Message
    {
        public string Payload { get; set; }
        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string IV { get; set; }
        public MessageTypeEnum MessageType { get; set; }
    }
}
