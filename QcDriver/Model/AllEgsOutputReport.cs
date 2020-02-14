using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QcDriver.Model
{
    public class AllEgsOutputReport
    {
        public DateTime ExecutionDate { get; set; }
        public string PropertyGroupName { get; set; }
        public string ProjectId { get; set; }
        public string MdmId { get; set; }
        public string WorkOrder { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public string DetailReport { get; set; }
        public List<GdcoTicket.Model.GdcoTicket> gdcoTickets { get; set; }
    }
}
