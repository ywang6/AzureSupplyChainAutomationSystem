using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GdcoTicket.Model
{
    [JsonObject]
    public class TicketingList
    {
        [JsonProperty("Count")]
        public int count { get; set; }

        [JsonProperty("Tickets")]
        public List<TicketingObject> Tickets { get; } = new List<TicketingObject>();
    }
}
