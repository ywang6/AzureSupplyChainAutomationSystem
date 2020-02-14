using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class GdcoTicketProperty2
    {
        public string op { get; set; }
        public string Path { get; set; }
        public Value Value { get; set; }
    }

    public class Value
    {
        public long Id { get; set; }
        public string Rel { get; set; }
    }
}
