using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetQc.Model
{
    public class AllEgsOutputModel
    {
        public string PropertyGroupName { get; set; }
        public string ProjectId { get; set; }
        public string MdmId { get; set; }
        public string DataCenterCode { get; set; }
        public string WorkOrderName { get; set; }
        public string GroupType { get; set; }
        public StringBuilder Error { get; set; }
        public List<AllEngineeringGroupsResult> AllEgsOutput { get; set; }
    }
}
