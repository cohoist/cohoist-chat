using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WellsChat.Shared
{
    public class Message
    {
        public string Payload { get; set; }
        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string IV { get; set; }
    }
}
