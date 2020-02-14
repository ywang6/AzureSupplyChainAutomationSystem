using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetQc.Model
{
    public class AllEngineeringGroupsResult
    {
        public string ProjectId { get; set; }
        public string MdmId { get; set; }
        public string WorkOrderName { get; set; }
        public string QcName { get; set; }
        public string RackName { get; set; }
        public string DeviceName { get; set; }
        public string DeviceType { get; set; }
        public string MsAssetValue { get; set; }
        public string SkuDocValue { get; set; }
        public string SkuDoc { get; set; }
        public string TestStatus { get; set; }
        public string Comments { get; set; }
    }
}
