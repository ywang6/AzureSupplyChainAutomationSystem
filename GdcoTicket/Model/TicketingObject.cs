using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GdcoTicket.Model
{
    [JsonObject]
    public class TicketingObject
    {
        [JsonProperty("Id")]
        public string TicketId { get; set; }
        [JsonProperty("State")]
        public string StateName { get; set; }
        [JsonProperty("Severity")]
        public string CurrentSeverity { get; set; }
        [JsonProperty("Code")]
        public string GDCOFaultCode { get; set; }
        [JsonProperty("CodeDescription")]
        public string GDCOFaultDescription { get; set; }
        [JsonProperty("MDMIdList")]
        public string MDMIdList { get; set; }
        [JsonProperty("FulfillmentId")]
        public string FulfillmentId { get; set; }
        [JsonProperty("Title")]
        public string TicketTitle { get; set; }
        [JsonProperty("Cluster")]
        public string ClusterName { get; set; }
        [JsonProperty("DatacenterCode")]
        public string DatacenterCode { get; set; }
        [JsonProperty("PropertyGroup")]
        public string PropertyGroupName { get; set; }
        public string DeliveryNumber { get; set; }
        public string TemplateType { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? AssignedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string WasSLABreached { get; set; }
    }
}
