using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class TicketStoreV2
    {
        public string TicketId { get; set; }
        public string CurrentState { get; set; }
        public string CurrentSeverity { get; set; }
        public string GDCOFaultCode { get; set; }
        public string GDCOFaultDescription { get; set; }
        public string MDMId { get; set; }
        public string workflowjobid { get; set; }
        public string DeliveryNumber { get; set; }
        public string DatacenterCode { get; set; }
        public string TemplateType { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string WasSLABreached { get; set; }
    }
}
