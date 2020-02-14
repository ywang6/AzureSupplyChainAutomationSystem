using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataSync.Model
{
    public class TaskStatusTicketStore
    {
        public string DeliveryNumber { get; set; }
        public string Source { get; set; }
        public string TaskName { get; set; }
        public string TaskStateName { get; set; }
        public string TaskTicketId { get; set; }
        public string TicketId { get; set; }
        public string TicketUrl { get; set; }
        public string TicketStatus { get; set; }
        public string GDCOFaultCode { get; set; }
        public string GDCOFaultDescription { get; set; }
        public string TemplateType { get; set; }
    }
}
