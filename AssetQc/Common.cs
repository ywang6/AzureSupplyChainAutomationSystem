using AssetQc.Model;

namespace AssetQc
{
    public class Common
    {
        public static AllEngineeringGroupsResult CreateResultObject(string ProjectId, string MdmId, string WorkOrderName, string QcName, string DeviceName, string DeviceType, string MsAssetValue, string SkuDocValue, string TestStatus, string Comments, string RackName, string SkuDoc)
        {
            return new AllEngineeringGroupsResult
            {
                ProjectId = ProjectId,
                MdmId = MdmId,
                WorkOrderName = WorkOrderName,
                QcName = QcName,
                DeviceName = DeviceName,
                DeviceType = DeviceType,
                MsAssetValue = MsAssetValue,
                SkuDocValue = SkuDocValue,
                TestStatus = TestStatus,
                Comments = Comments,
                RackName = RackName,
                SkuDoc = SkuDoc
            };
        }
    }
}
