using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class ExternalTicket
    {
        public long TicketId { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}
