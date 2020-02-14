using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class GdcoTableTicket
    {
        public long Id { get; set; }
        public string ProjectId { get; set; }
        public string GdcoTicket { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
