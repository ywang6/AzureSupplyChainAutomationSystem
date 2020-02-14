using AssetQc.Model;
using GdcoTicket;
using MsAsset;
using MsAsset.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Utilities;
using System.Net;

namespace AssetQc
{
    public class AllEgsQc
    {
        private string ProjectId;
        private string MdmId;
        private string WorkOrderName;
        private AllEgsOutputModel AllEgsOutputModel;
        private List<PhysicalAssetListValue> MsAssetPhysicalAssetList;
        private List<PhysicalAssetListValue> MsAssetPhysicalRackList;
        private Dictionary<string, SkuInfo> SkuDictionary;

        private const string Pass = "Passed";
        private const string Fail = "Failed";
        private const string NotFound = "NotFound";

        private const string ApprovedDcLocationValidation = "Approved DC Location Validation";
        private const string InvalidLocationMessage = "Invalid Location";

        private const string TechSpecDeviceCountValidation = "Tech Spec Device Count Validation";
        private const string DeviceCountIncorrectMessage = "Device Count Incorrect";

        private const string TechSpecDskuValidation = "Tech Spec Discrete SKU Validation";
        private const string DskuMismatchMessage = "DSKU mismatch";

        private const string PhysicalRackLayoutValidation = "Physical RackLayout Slot Validation";
        private const string LayoutValidationFailureMessage = "Layout is Incorrect";

        private List<string> rackSecurityClassificationEgs = new List<string>{ "BOSG - SPO - S", "SharePoint Shared MT", "FOPE", "FAST Search" };
        private List<string> SPOegs = new List<string> { "BOSG - SPO - S", "SharePoint Shared MT", "BOSG - Federal Sharepoint", "FAST Search", "FOPE", "FOPE - GSGO", "CDDS", "IS", "Core Platform Services" };
        private List<string> rackSecurityClassificationEgExclusions = new List<string> { "Federal", "GSGO", "ITAR" };

        public AllEgsOutputModel PerformAllEgsSkuMsAssetValidation(string Id, string WorkOrderName, string PropertyGroupName, string Mode, string projectCategory = null, string templateDetails = null, string dcCode = null, string GroupType = null, Dictionary<string, string> CpsFidList = null, string mdmidList = null, string purchaseOrderNumber = null, bool exoFlag = false)
        {
            AllEgsOutputModel = new AllEgsOutputModel
            {
                AllEgsOutput = new List<AllEngineeringGroupsResult>()
            };
            MsAssetDataHandler msAssetDataHandler = new MsAssetDataHandler();
            List<PhysicalAssetListValue> physicalAssetListOdata = null;
            var Error = new StringBuilder(string.Empty);
            this.WorkOrderName = WorkOrderName;
            int maxRetry = 2;

            AllEgsOutputModel.PropertyGroupName = PropertyGroupName;
            AllEgsOutputModel.WorkOrderName = WorkOrderName;
            GDCOTicketStoreHandler ticketStore = new GDCOTicketStoreHandler();
            string[] MDMIDArray = null;

            if (!String.IsNullOrEmpty(mdmidList))
            {
                MDMIDArray = mdmidList.Split(';');
            }
            HashSet<string> MDMIDSet = MDMIDArray == null ? new HashSet<string>() : new HashSet<string>(MDMIDArray);
            
            if (Mode.Equals("M"))
            {
                AllEgsOutputModel.MdmId = Id;
                MdmId = Id;

                if (string.IsNullOrEmpty(MdmId))
                {
                    Error.AppendLine("Input MdmId is null or Empty");
                    AllEgsOutputModel.Error = Error;
                    return AllEgsOutputModel;
                }

                // Pull the MsAsset data for the project Id
                var retryCount = 0;
                var retry = false;
                do
                {
                    retry = false;
                    try
                    {
                        physicalAssetListOdata = msAssetDataHandler.GetPhysicalAssetListByMdmId(MdmId);
                        if (physicalAssetListOdata == null || !physicalAssetListOdata.Any())
                        {
                            Error.AppendLine("MSAsset returned no output for MdmId: " + MdmId);
                            AllEgsOutputModel.Error = Error;
                            retry = true;
                            retryCount++;
                            Thread.Sleep(10000);
                            if (retryCount >= maxRetry)
                            {
                                // Cut the ticket to DCS when no pid/mdmid found in MSAsset
                                string errMsg = "Assets are present in MSAsset but the MDMID: " + MdmId + " is not associated to the asset";

                                if (dcCode != null)
                                {
                                    AllEgsOutputModel.DataCenterCode = dcCode;
                                    if (SPOegs.Contains(PropertyGroupName))
                                    {
                                        //Cut tickets to IcM and OA
                                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Missing FID/MDMID", null, null,
                                                        null, null, Fail, errMsg, null, null));
                                    }
                                    else
                                    {
                                        //Cut ticket to just OA
                                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Mdmid missing in MSAsset", null, null,
                                                        null, null, Fail, errMsg, null, null));
                                    }
                                }
                                else
                                {
                                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Cannot find DC code, Property Group: " + PropertyGroupName, "PerformAllEgsSkuMsAssetValidation: Cannot find DC code for MDM Id = " + MdmId + ", Property Group: " + PropertyGroupName); 
                                }
                                return AllEgsOutputModel;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Error.AppendLine(ex.ToString());
                        AllEgsOutputModel.Error = Error;
                        retry = true;
                        retryCount++;
                        Thread.Sleep(10000);
                        if (retryCount >= maxRetry)
                            return AllEgsOutputModel;
                    }
                } while (retry == true && retryCount < maxRetry);
            }
            else if (Mode.Equals("D"))
            {
                AllEgsOutputModel.MdmId = Id;
                MdmId = Id;

                // Exclude all of the migration Fids
                if (Constants.MigrationFidList.Contains(Id))
                {
                    AllEgsOutputModel.Error = Error;
                    return AllEgsOutputModel;
                }

                // We don't care for MsAsset associations for Network, Container, or Discrete projects, so we just return from here
                if (PropertyGroupName.IndexOf("FOPE", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    if (GroupType.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 || GroupType.IndexOf("Container", StringComparison.OrdinalIgnoreCase) >= 0 || (PropertyGroupName.IndexOf("PSO KMS", StringComparison.OrdinalIgnoreCase) >= 0 && GroupType.Trim().IndexOf("DiscreteServer", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        AllEgsOutputModel.Error = Error;
                        return AllEgsOutputModel;
                    }
                }
                else
                {
                    if (GroupType.Trim().IndexOf("DiscreteServer", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AllEgsOutputModel.Error = Error;
                        return AllEgsOutputModel;
                    }
                }

                if (string.IsNullOrEmpty(MdmId))
                {
                    Error.AppendLine("Input FulfillmentID is null or Empty");
                    AllEgsOutputModel.Error = Error;
                    return AllEgsOutputModel;
                }

                // Pull the MsAsset data for the project Id
                var retryCount = 0;
                var retry = false;
                do
                {
                    retry = false;
                    try
                    {
                        if (GroupType.Trim().Equals("EngineeringGroupNetwork", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                physicalAssetListOdata = msAssetDataHandler.GetPhysicalAssetListByMdmId(Id);
                            }
                            catch (WebException ex)
                            {
                                if (ex.Message.IndexOf("404") >= 0)
                                {
                                    // ignore 404 error
                                }
                                else
                                {
                                    throw new Exception("Error happened during GetPhysicalAssetListByMdmId");
                                }
                            }

                            // TEMP CHANGE: when mdmid not working, use projectID API instead
                            if (physicalAssetListOdata == null || !physicalAssetListOdata.Any())
                            {
                                try
                                {
                                    physicalAssetListOdata = msAssetDataHandler.GetPhysicalAssetListByProjectId(Id);
                                }
                                catch (WebException ex)
                                {
                                    if (ex.Message.IndexOf("404") >= 0)
                                    {
                                        // ignore 404 error
                                    }
                                    else
                                    {
                                        throw new Exception("Error happened during GetPhysicalAssetListByProjectId");
                                    }
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                physicalAssetListOdata = msAssetDataHandler.GetPhysicalAssetListByMdmId(Id);
                            }
                            catch (WebException ex)
                            {
                                if (ex.Message.IndexOf("404") >= 0)
                                {
                                    // ignore 404 error
                                }
                                else
                                {
                                    throw new Exception("Error happened during GetPhysicalAssetListByMdmId");
                                }
                            }

                            // when fid not working, we try each mdmID
                            if (physicalAssetListOdata == null || !physicalAssetListOdata.Any())
                            {
                                if (MDMIDSet != null)
                                {
                                    foreach (var mdmid in MDMIDSet)
                                    {
                                        try
                                        {
                                            physicalAssetListOdata = msAssetDataHandler.GetPhysicalAssetListByMdmId(mdmid);
                                        }
                                        catch (WebException ex)
                                        {
                                            if (ex.Message.IndexOf("404") >= 0)
                                            {
                                                // ignore 404 error
                                                continue;
                                            }
                                            else
                                            {
                                                throw new Exception("Error happened during GetPhysicalAssetListByMdmId");
                                            }
                                        }
                                        if (physicalAssetListOdata != null && physicalAssetListOdata.Any())
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Error.AppendLine(ex.ToString());
                        AllEgsOutputModel.Error = Error;
                        retry = true;
                        retryCount++;
                        Thread.Sleep(1000);
                        if (retryCount >= maxRetry)
                        {
                            // cut ticket to DCS
                            if (physicalAssetListOdata == null || !physicalAssetListOdata.Any())
                            {
                                retry = true;
                                retryCount++;
                                Thread.Sleep(10000);
                                if (retryCount >= maxRetry)
                                {
                                    // Cut the ticket to DCS when no pid/mdmid found in MSAsset
                                    string errMsg = "Assets are present in MSAsset but the MDMID: " + MdmId + " is not associated to the asset. ";

                                    if (PropertyGroupName.IndexOf("Exchange", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        errMsg = "Assets are present in MSAsset but the MDMID(s): " + mdmidList + " not associated to the asset. ";
                                    }

                                    // Look for PO information and append it to the error description
                                    List<string> PoList = ticketStore.GetPoNumbersByDeliveryNumber(MdmId);
                                    StringBuilder sb = new StringBuilder();
                                    if (PoList != null && PoList.Any())
                                    {
                                        foreach (string PO in PoList)
                                        {
                                            sb.AppendLine(PO);
                                        }
                                        errMsg += "PO information: " + sb.ToString() + " <EOM>";
                                    }
                                    else
                                    {
                                        if (purchaseOrderNumber != null)
                                        {
                                            errMsg += "Purchase Order Number: " + purchaseOrderNumber + " <EOM>";
                                        }
                                        else
                                        {
                                            Error.AppendLine("MSAsset returned no output for FulfillmentID: " + MdmId + " and No PO found for this Fid.");
                                            AllEgsOutputModel.Error = Error;
                                        }
                                    }

                                    if (dcCode != null)
                                    {
                                        AllEgsOutputModel.DataCenterCode = dcCode;
                                        if (SPOegs.Contains(PropertyGroupName))
                                        {
                                            //Cut tickets to IcM and OA
                                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Missing FID/MDMID", null, null,
                                                            null, null, Fail, errMsg, null, null));
                                        }
                                        else
                                        {
                                            //Cut ticket to OA
                                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Mdmid missing in MSAsset", null, null,
                                                            null, null, Fail, errMsg, null, null));
                                        }
                                    }
                                    else
                                    {
                                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Cannot find DC code, Property Group: " + PropertyGroupName, "PerformAllEgsSkuMsAssetValidation: Cannot find DC code for MDM Id = " + MdmId + ", Property Group: " + PropertyGroupName);
                                    }

                                    AllEgsOutputModel.Error = Error;
                                    return AllEgsOutputModel;
                                }
                            }
                        }
                            
                    }
                } while (retry == true && retryCount < maxRetry);
            }

            // cut ticket to DCS
            if (physicalAssetListOdata == null || !physicalAssetListOdata.Any())
            {
                // Cut the ticket to DCS when no pid/mdmid found in MSAsset
                string errMsg = "Assets are present in MSAsset but the MDMID: " + MdmId + " is not associated to the asset. ";

                if (PropertyGroupName.IndexOf("Exchange", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    errMsg = "Assets are present in MSAsset but the MDMID(s): " + mdmidList + " not associated to the asset. ";
                }

                // Look for PO information and append it to the error description
                List<string> PoList = ticketStore.GetPoNumbersByDeliveryNumber(MdmId);
                StringBuilder sb = new StringBuilder();
                if (PoList != null && PoList.Any())
                {
                    foreach (string PO in PoList)
                    {
                        sb.AppendLine(PO);
                    }
                    errMsg += "PO information: " + sb.ToString() + " <EOM>";
                }
                else
                {
                    if (purchaseOrderNumber != null)
                    {
                        errMsg += "Purchase Order Number: " + purchaseOrderNumber + " <EOM>";
                    }
                    else
                    {
                        Error.AppendLine("MSAsset returned no output for FulfillmentID: " + MdmId + " and No PO found for this Fid.");
                        AllEgsOutputModel.Error = Error;
                    }
                }

                if (dcCode != null)
                {
                    AllEgsOutputModel.DataCenterCode = dcCode;
                    if (SPOegs.Contains(PropertyGroupName))
                    {
                        //Cut tickets to IcM and OA
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Missing FID/MDMID", null, null,
                                        null, null, Fail, errMsg, null, null));
                    }
                    else
                    {
                        //Cut ticket to OA
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Mdmid missing in MSAsset", null, null,
                                        null, null, Fail, errMsg, null, null));
                    }
                }
                else
                {
                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Cannot find DC code, Property Group: " + PropertyGroupName, "PerformAllEgsSkuMsAssetValidation: Cannot find DC code for MDM Id = " + MdmId + ", Property Group: " + PropertyGroupName);
                }

                AllEgsOutputModel.Error = Error;
                return AllEgsOutputModel;
            }

            try
            {
                // Assign MsAssetPhysicalAssetList
                MsAssetPhysicalAssetList = physicalAssetListOdata;
                var assetDcCode = MsAssetPhysicalAssetList.Where(ms => !string.IsNullOrEmpty(ms.DataCenterCode)).FirstOrDefault();
                if (assetDcCode != null)
                {
                    AllEgsOutputModel.DataCenterCode = assetDcCode.DataCenterCode;
                }

                AllEgsOutputModel.PropertyGroupName = PropertyGroupName;
                AllEgsOutputModel.WorkOrderName = WorkOrderName;

                // Extract distinct Rack SKU
                var distinctRackSkuName = MsAssetPhysicalAssetList.Where(r => !string.IsNullOrEmpty(r.ItemType) && !string.IsNullOrEmpty(r.SKUMSFId) && r.ItemType.Trim().Equals("Rack", StringComparison.OrdinalIgnoreCase)).GroupBy(rack => rack.SKUMSFId).Select(grp => grp.First().SKUMSFId).ToList();

                SkuDictionary = new Dictionary<string, SkuInfo>();
                if (distinctRackSkuName != null)
                {
                    foreach (var rackSkuName in distinctRackSkuName)
                    {
                        if (!string.IsNullOrEmpty(rackSkuName))
                        {
                            SkuDictionary[rackSkuName.Trim()] = new FetchSku().FetchSkuInfo(rackSkuName.Trim());
                        }
                    }
                }

                // Validations
                // Validate Device State
                ValidateDeviceState(MsAssetPhysicalAssetList, exoFlag, PropertyGroupName);

                if (projectCategory != null && exoFlag && projectCategory.Trim().Equals("Network", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNetworkProjectCategory(MsAssetPhysicalAssetList);
                }

                // Vlan QC
                if (projectCategory != null && templateDetails != null && ((templateDetails.IndexOf("vlan", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (projectCategory.IndexOf("vlan", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    // Perform Vlan QC
                    new VlanQc().VlanValidation(ProjectId, WorkOrderName, AllEgsOutputModel);
                }

                // Extract the Rack List
                var rackList = MsAssetPhysicalAssetList.Where(r => !string.IsNullOrEmpty(r.ItemType) && r.ItemType.Trim().Equals("Rack", StringComparison.OrdinalIgnoreCase)).ToList();
                var spoQc = new SpoQc();
                if (rackList != null && rackList.Any())
                {
                    foreach (var rack in rackList)
                    {
                        try
                        {
                            MsAssetPhysicalRackList = msAssetDataHandler.GetPhysicalAssetListByRackId(rack.Id);
                        }
                        catch (WebException ex)
                        {
                            if (ex.Message.IndexOf("404") >= 0)
                            {
                                // ignore 404 error
                            }
                            else
                            {
                                throw new Exception("Error happened during GetPhysicalAssetListByRackId");
                            }
                        }


                        if (rack != null && !string.IsNullOrEmpty(rack.Name) && MsAssetPhysicalRackList != null)
                        {
                            try
                            {
                                // Extract Chassis, Server and NetworkDevices
                                var (ServerList, ChassisList, NetworkDeviceList, PowerUpsList) = RackListExtraction(rack);
                                // Validate Tech Spec
                                ValidateTechSpec(rack, ServerList, ChassisList, NetworkDeviceList, PowerUpsList, WorkOrderName, exoFlag);
                                // Validate Physical Rack Layout
                                ValidatePhysicalRackLayout(rack, ServerList, ChassisList, NetworkDeviceList, PowerUpsList, exoFlag);
                            }
                            catch(Exception ex)
                            {
                                throw ex;
                            }
                            
                        }
                    }
                    
                    if (PropertyGroupName != null && !exoFlag && rackSecurityClassificationEgs.Any(s=>(PropertyGroupName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) && !(rackSecurityClassificationEgExclusions.Any(s => (PropertyGroupName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))))
                    {
                        spoQc.RackSecurityClassificationQc(ProjectId, MdmId, WorkOrderName, rackList.ToList(), AllEgsOutputModel);
                    }
                    if (PropertyGroupName != null && exoFlag && !(rackSecurityClassificationEgExclusions.Any(s => (PropertyGroupName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))))
                    {
                        RackSecurityClassificationQc(ProjectId, MdmId, WorkOrderName, PropertyGroupName, rackList.ToList(), AllEgsOutputModel);
                    }
                }

                // Check CPS List to see if current project is CPS
                if (!exoFlag && CpsFidList != null && CpsFidList.ContainsKey(MdmId) && CpsFidList[MdmId].IndexOf("FI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CpsQc cpsQc = new CpsQc();
                    cpsQc.CpsValidation(ProjectId, MdmId, WorkOrderName, MsAssetPhysicalAssetList, AllEgsOutputModel);
                }

                if (!exoFlag && PropertyGroupName != null && PropertyGroupName.IndexOf("FOPE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        GroupType != null && (GroupType.IndexOf("PreRack", StringComparison.OrdinalIgnoreCase) >= 0 || GroupType.IndexOf("Discrete", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // Check duplicate asset tags & serial numbers 
                    spoQc.FopeDuplicateCheck(MsAssetPhysicalAssetList, ProjectId, MdmId, WorkOrderName, AllEgsOutputModel);
                }
                
                if (!exoFlag && PropertyGroupName.IndexOf("FOPE", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    spoQc.ValidateFope(MsAssetPhysicalAssetList, ProjectId, MdmId, WorkOrderName, GroupType, AllEgsOutputModel);
                }

                // Perform FOPE Discrete QC
                if (!exoFlag && PropertyGroupName != null && PropertyGroupName.IndexOf("FOPE", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(projectCategory)
                    && projectCategory.Trim().IndexOf("Discrete", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FopeDiscreteQc fopeQc = new FopeDiscreteQc();
                    fopeQc.FopeDiscreteCheck(Id, PropertyGroupName, WorkOrderName, AllEgsOutputModel);
                }

                // SPO WCS checks for PRD
                if (!exoFlag && GroupType.Equals("PreRack", StringComparison.OrdinalIgnoreCase))
                {
                    // SPO ServerName checks
                    spoQc.spoServerNameCheck(MsAssetPhysicalAssetList, ProjectId, MdmId, PropertyGroupName, WorkOrderName, AllEgsOutputModel);

                    // Perform SPO WCS Vlan check
                    spoQc.spoKustoCheck(ProjectId, MdmId, PropertyGroupName, WorkOrderName, AllEgsOutputModel);
                }
            }
            catch (Exception e)
            {
                Error.AppendLine(e.ToString());
                return null;
            }
            AllEgsOutputModel.Error = Error;

            // create dummy AllEgsOutput if there is no errors so that we can see these projects in the report
            if (!AllEgsOutputModel.AllEgsOutput.Any())
            {
                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDskuValidation, null, null, null, null, Pass, null, null, null));
            }

            return AllEgsOutputModel;
        }
        
        private void ValidateTechSpec(PhysicalAssetListValue Rack, List<PhysicalAssetListValue> ServerList, List<PhysicalAssetListValue> ChassisList, List<PhysicalAssetListValue> NetworkDeviceList, List<PhysicalAssetListValue> PowerUpsList, string WorkOrderName, bool exoFlag = false)
        {
            if (exoFlag)
            {
                if (Rack != null && !string.IsNullOrEmpty(Rack.DiscreteSKUMSFId) && !string.IsNullOrEmpty(Rack.Name) && !string.IsNullOrEmpty(Rack.ItemType) && !string.IsNullOrEmpty(Rack.SKUMSFId) && SkuDictionary.ContainsKey(Rack.SKUMSFId) && SkuDictionary[Rack.SKUMSFId] != null && SkuDictionary[Rack.SKUMSFId].RackSpec != null && SkuDictionary[Rack.SKUMSFId].RackSpec.DskuName != null)
                {
                    if (!(ExtractMSF(SkuDictionary[Rack.SKUMSFId].RackSpec.DskuName).Equals(ExtractMSF(Rack.DiscreteSKUMSFId.Trim()), StringComparison.OrdinalIgnoreCase)))
                    {
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDskuValidation, Rack.Name, Rack.ItemType, Rack.DiscreteSKUMSFId, SkuDictionary[Rack.SKUMSFId].RackSpec.DskuName, Fail, DskuMismatchMessage, Rack.Name, Rack.SKUMSFId));
                    }
                }
            }

            List<PhysicalAssetListValue> ServerOnlyList = null;
            List<PhysicalAssetListValue> UpsList = null;
            List<PhysicalAssetListValue> ChassisManagerList = null;
            int quantityPerRack;

            if (ServerList != null && ServerList.Any())
            {
                (ServerOnlyList, UpsList, ChassisManagerList) = DeviceCountServerListBuild(ServerList, exoFlag);
            }

            // Gen 9 Servers
            if (UpsList != null && !UpsList.Any() && ChassisManagerList != null && !ChassisManagerList.Any())
            {
                ServerOnlyList = ServerList;
            }

            // Gen 9 Servers with UPS and Chassis Manager
            if (PowerUpsList != null && PowerUpsList.Any())
            {
                UpsList = PowerUpsList;
            }

            if (!string.IsNullOrEmpty(Rack.SKUMSFId) && SkuDictionary.ContainsKey(Rack.SKUMSFId) && SkuDictionary[Rack.SKUMSFId] != null)
            {
                if (SkuDictionary[Rack.SKUMSFId].ServerSpec != null)
                {
                    quantityPerRack = SkuDictionary[Rack.SKUMSFId].ServerSpec.QuantityPerRack;
                    MissingAssetCheck(ServerOnlyList, Rack, "Server", quantityPerRack);
                    DeviceCountValidation(ServerOnlyList, Rack, "Server", quantityPerRack);
                    ServerDskuValidation(ServerOnlyList, Rack, SkuDictionary[Rack.SKUMSFId].ServerSpec, exoFlag);
                }
                if (SkuDictionary[Rack.SKUMSFId].UpsSpec != null)
                {
                    quantityPerRack = SkuDictionary[Rack.SKUMSFId].UpsSpec.QuantityPerRack;
                    MissingAssetCheck(UpsList, Rack, "UPS", quantityPerRack);
                    if (UpsList.Any() && !UpsList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                    {
                        DeviceCountValidation(UpsList, Rack, "UPS", quantityPerRack);
                        DskuValidation(UpsList, Rack, SkuDictionary[Rack.SKUMSFId].UpsSpec, exoFlag);
                    }
                }
                if (SkuDictionary[Rack.SKUMSFId].ChassisManagerSpec != null)
                {
                    quantityPerRack = SkuDictionary[Rack.SKUMSFId].ChassisManagerSpec.QuantityPerRack;
                    MissingAssetCheck(ChassisManagerList, Rack, "Chassis Manager", quantityPerRack);
                    if (ChassisManagerList.Any() && !ChassisManagerList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                    {
                        DeviceCountValidation(ChassisManagerList, Rack, "Chassis Manager", quantityPerRack);
                        DskuValidation(ChassisManagerList, Rack, SkuDictionary[Rack.SKUMSFId].ChassisManagerSpec, exoFlag);
                    }
                }
                if (SkuDictionary[Rack.SKUMSFId].ChassisSpec != null)
                {
                    quantityPerRack = SkuDictionary[Rack.SKUMSFId].ChassisSpec.QuantityPerRack;
                    MissingAssetCheck(ChassisList, Rack, "Chassis", quantityPerRack);
                    if (ChassisList.Any() && !ChassisList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                    {
                        DeviceCountValidation(ChassisList, Rack, "Chassis", quantityPerRack);
                        DskuValidation(ChassisList, Rack, SkuDictionary[Rack.SKUMSFId].ChassisSpec, exoFlag);
                    }
                }

                if (NetworkDeviceList != null && NetworkDeviceList.Any())
                {
                    var (LoadBalancerList, SwitchList, IloList, DigiList) = DeviceCountNetworkListBuild(NetworkDeviceList, exoFlag);

                    if (SkuDictionary[Rack.SKUMSFId].LoadBalancerSpec != null)
                    {
                        quantityPerRack = SkuDictionary[Rack.SKUMSFId].LoadBalancerSpec.QuantityPerRack;
                        MissingAssetCheck(LoadBalancerList, Rack, "Load Balancer", quantityPerRack);
                        if (LoadBalancerList.Any() && !LoadBalancerList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                        {
                            DeviceCountValidation(LoadBalancerList, Rack, "Load Balancer", quantityPerRack);
                            DskuValidation(LoadBalancerList, Rack, SkuDictionary[Rack.SKUMSFId].LoadBalancerSpec, exoFlag);
                        }
                    }
                    if (SkuDictionary[Rack.SKUMSFId].SwitchSpec != null)
                    {
                        quantityPerRack = SkuDictionary[Rack.SKUMSFId].SwitchSpec.QuantityPerRack;
                        MissingAssetCheck(SwitchList, Rack, "Switch", quantityPerRack, " DeviceType might be missing in MSAsset");
                        if (SwitchList.Any() && !SwitchList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                        {
                            DeviceCountValidation(SwitchList, Rack, "Switch", quantityPerRack, " DeviceType might be missing in MSAsset");
                            DskuValidation(SwitchList, Rack, SkuDictionary[Rack.SKUMSFId].SwitchSpec, exoFlag);
                        }
                    }
                    if (SkuDictionary[Rack.SKUMSFId].IloSpec != null)
                    {
                        quantityPerRack = SkuDictionary[Rack.SKUMSFId].IloSpec.QuantityPerRack;
                        MissingAssetCheck(IloList, Rack, "ILO Switch", quantityPerRack);
                        if (IloList.Any() && !IloList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                        {
                            DeviceCountValidation(IloList, Rack, "ILO Switch", quantityPerRack);
                            DskuValidation(IloList, Rack, SkuDictionary[Rack.SKUMSFId].IloSpec, exoFlag);
                        }
                    }
                    if (SkuDictionary[Rack.SKUMSFId].DigiSpec != null)
                    {
                        quantityPerRack = SkuDictionary[Rack.SKUMSFId].DigiSpec.QuantityPerRack;
                        MissingAssetCheck(DigiList, Rack, "DIGI Switch", quantityPerRack);
                        if (DigiList.Any() && !DigiList.First().PropertyGroup.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                        {
                            DeviceCountValidation(DigiList, Rack, "DIGI Switch", quantityPerRack);
                            DskuValidation(DigiList, Rack, SkuDictionary[Rack.SKUMSFId].DigiSpec, exoFlag);
                        }
                    }
                }
            }
            else
            {
                SecondaryNetworkDeviceCheck(Rack);
            }
        }

        private void ValidatePhysicalRackLayout(PhysicalAssetListValue Rack, List<PhysicalAssetListValue> ServerList, List<PhysicalAssetListValue> ChassisList, List<PhysicalAssetListValue> NetworkDeviceList, List<PhysicalAssetListValue> PowerUpsList, bool exoFlag = false)
        {
            List<PhysicalAssetListValue> ServerOnlyList = null;
            List<PhysicalAssetListValue> UpsList = null;
            List<PhysicalAssetListValue> ChassisManagerList = null;
            List<PhysicalAssetListValue> LoadBalancerList = null;
            List<PhysicalAssetListValue> SwitchList = null;
            List<PhysicalAssetListValue> IloList = null;
            List<PhysicalAssetListValue> DigiList = null;

            if (ServerList != null && ServerList.Any())
            {
                (ServerOnlyList, UpsList, ChassisManagerList) = RackLayoutServerListBuild(ServerList);
            }

            // Gen 9 Servers
            if (UpsList != null && !UpsList.Any() && ChassisManagerList != null && !ChassisManagerList.Any())
            {
                ServerOnlyList = ServerList;
            }

            // Gen 9 Servers with UPS and Chassis Manager
            if (PowerUpsList != null && PowerUpsList.Any())
            {
                UpsList = PowerUpsList;
            }

            if (NetworkDeviceList != null && NetworkDeviceList.Any())
            {
                (LoadBalancerList, SwitchList, IloList, DigiList) = RackLayoutNetworkListBuild(NetworkDeviceList, exoFlag);
            }

            if (!string.IsNullOrEmpty(Rack.SKUMSFId) && SkuDictionary.ContainsKey(Rack.SKUMSFId) && SkuDictionary[Rack.SKUMSFId] != null)
            {
                if (SkuDictionary[Rack.SKUMSFId].ServerLayout != null && SkuDictionary[Rack.SKUMSFId].ServerLayout.Any())
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].ServerLayout;
                    //WCS Validation
                    if (ServerOnlyList.Count < ServerList.Count && (PowerUpsList == null || !PowerUpsList.Any()))
                    {
                        SideAndSlotValidation(ServerOnlyList, Rack, assetLayout, exoFlag);
                        BinValidation(ServerOnlyList, Rack, assetLayout, exoFlag);
                    }
                    //Gen9 Validation
                    if (ServerOnlyList.Count == ServerList.Count || (PowerUpsList != null && PowerUpsList.Any()))
                    {
                        SideAndSlotValidation(ServerOnlyList, Rack, assetLayout, exoFlag);
                    }
                }
                if (SkuDictionary[Rack.SKUMSFId].UpsLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].UpsLayout;
                    SideAndSlotValidation(UpsList, Rack, assetLayout);
                }
                if (SkuDictionary[Rack.SKUMSFId].ChassisManagerLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].ChassisManagerLayout;
                    SideAndSlotValidation(ChassisManagerList, Rack, assetLayout);
                }
                if (SkuDictionary[Rack.SKUMSFId].LoadBalancerLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].LoadBalancerLayout;
                    SideAndSlotValidation(LoadBalancerList, Rack, assetLayout);
                }
                if (SkuDictionary[Rack.SKUMSFId].SwitchLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].SwitchLayout;
                    SideAndSlotValidation(SwitchList, Rack, assetLayout);
                }
                if (SkuDictionary[Rack.SKUMSFId].IloLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].IloLayout;
                    SideAndSlotValidation(IloList, Rack, assetLayout);
                }
                if (SkuDictionary[Rack.SKUMSFId].DigiLayout != null)
                {
                    var assetLayout = SkuDictionary[Rack.SKUMSFId].DigiLayout;
                    SideAndSlotValidation(DigiList, Rack, assetLayout);
                }
            }
        }

        private void ValidateDeviceState(List<PhysicalAssetListValue> assets, bool exoFlag = false, string PG = null)
        {
            if (exoFlag)
            {
                // HashSet used to check duplicate server names
                HashSet<String> ServerNameCheck = new HashSet<String>();
                // HashSet used to check duplicate Asset tags
                HashSet<String> AssetTagCheck = new HashSet<String>();
                // HashSet used to check duplicate Serial numbers
                HashSet<String> SerialNumberCheck = new HashSet<String>();

                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset != null && asset.Location != null && asset.Location.Trim().IndexOf("Fail", StringComparison.OrdinalIgnoreCase) < 0 && asset.Location.Trim().IndexOf("DPLY", StringComparison.OrdinalIgnoreCase) < 0 && asset.Location.Trim().IndexOf("STRG", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation != null && asset.Colocation.Trim().IndexOf("Fail", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation.Trim().IndexOf("DPLY", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation.Trim().IndexOf("STRG", StringComparison.OrdinalIgnoreCase) < 0 && asset.DeviceState != null && asset.DeviceState.IndexOf("Installed", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "MSAsset Device State Validation", asset.Name, asset.ItemType, asset.DeviceState, "Installed", Fail, "Incorrect Device State", null, null));
                        }

                        // Validate Server Property Dimension
                        // Exception case for PropertyGroup= USSec EXO
                        if (asset != null && asset.ItemType != null && asset.ItemType.Trim().IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0 && asset.PropertyDimension != null && asset.PropertyDimension.Trim().IndexOf("Exchange", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            if ((PG.Trim().IndexOf("USSec EXO", StringComparison.OrdinalIgnoreCase) < 0) && (PG.Trim().IndexOf("USNat EXO", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "421 - Property dimension incorrect for Server", asset.Name, asset.ItemType, asset.PropertyDimension, "Exchange", Fail, "Server Property Dimension mismatch", null, null));
                            }
                        }

                        // Validate Server Security Classification
                        if (asset != null && asset.ItemType != null && asset.ItemType.Trim().IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0 && asset.SecurityClassification != null && asset.SecurityClassification.Trim().IndexOf("HBI", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            if ((PG.Trim().IndexOf("USSec EXO", StringComparison.OrdinalIgnoreCase) < 0) && (PG.Trim().IndexOf("USNat EXO", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "422 - Server security classification incorrect", asset.Name, asset.ItemType, asset.SecurityClassification, "HBI", Fail, "Server Security Classification mismatch", null, null));
                            }
                        }

                        // Check Dup Server Names
                        if (asset.Name != null && !ServerNameCheck.Add(asset.Name))
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "409 - Duplicate server name for PID/MDMID", asset.Name, asset.ItemType, asset.Name, "No Duplicate Server Names", Fail, "Duplicate Server Name: " + asset.Name, null, null));
                        }

                        // Check Dup Asset tags
                        if (asset.AssetTag != null && !AssetTagCheck.Add(asset.AssetTag))
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "425 - Duplicate asset tags in MSAsset", asset.Name, asset.ItemType, asset.AssetTag, "No Duplicate AssetTags", Fail, "Duplicate Asset Tag: " + asset.AssetTag, null, null));
                        }

                        // Check Dup Serial numbers
                        if (asset.SerialNumber != null && !SerialNumberCheck.Add(asset.SerialNumber))
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "426 - Duplicate serial numbers in MSAsset", asset.Name, asset.ItemType, asset.SerialNumber, "No Duplicate Serial Numbers", Fail, "Duplicate Serial Numbers: " + asset.SerialNumber, null, null));
                        }
                    }
                }
            }
            else
            {
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset != null && asset.Location != null && asset.Location.Trim().IndexOf("Fail", StringComparison.OrdinalIgnoreCase) < 0 && asset.Location.Trim().IndexOf("DPLY", StringComparison.OrdinalIgnoreCase) < 0 && asset.Location.Trim().IndexOf("STRG", StringComparison.OrdinalIgnoreCase) < 0 && asset.Location.Trim().IndexOf("DCOM", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation != null && asset.Colocation.Trim().IndexOf("Fail", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation.Trim().IndexOf("DPLY", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation.Trim().IndexOf("STRG", StringComparison.OrdinalIgnoreCase) < 0 && asset.Colocation.Trim().IndexOf("DCOM", StringComparison.OrdinalIgnoreCase) < 0 && asset.DeviceState != null && asset.DeviceState.IndexOf("Installed", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "MSAsset Device State Validation", asset.Name, asset.ItemType,
                                        asset.DeviceState, "Installed", Fail, "Incorrect Device State", null, null));
                        }
                    }
                }
            }
        }

        private void DeviceCountValidation(List<PhysicalAssetListValue> assets, PhysicalAssetListValue rack, string assetType, int quantityPerRack, string errorAdjustment = "")
        {
            if (assets != null && !string.IsNullOrEmpty(rack.SKUMSFId) && !string.IsNullOrEmpty(rack.Name) && SkuDictionary.ContainsKey(rack.SKUMSFId) && SkuDictionary[rack.SKUMSFId] != null)
            {
                if (!(quantityPerRack == assets.Count))
                {
                    AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDeviceCountValidation, null, assetType, assets.Count.ToString(), quantityPerRack.ToString(), Fail, DeviceCountIncorrectMessage + errorAdjustment, rack.Name, rack.SKUMSFId));
                }
            }
        }

        private void MissingAssetCheck(List<PhysicalAssetListValue> assets, PhysicalAssetListValue rack, string assetType, int quantityPerRack, string errorAdjustment = "")
        {
            if (assets == null && !string.IsNullOrEmpty(rack.SKUMSFId) && !string.IsNullOrEmpty(rack.Name) && SkuDictionary.ContainsKey(rack.SKUMSFId) && SkuDictionary[rack.SKUMSFId] != null && quantityPerRack > 0)
            {
                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDeviceCountValidation, null, assetType, NotFound, quantityPerRack.ToString(), Fail, DeviceCountIncorrectMessage + errorAdjustment, rack.Name, rack.SKUMSFId));
            }
        }

        private void SecondaryNetworkDeviceCheck(PhysicalAssetListValue rack)
        {
            int LBCount = 0;
            int TORCount = 0;
            int ILOCount = 0;
            int DIGICount = 0;
            int TotalNetDeviceCount = 0;

            if (!string.IsNullOrEmpty(rack.SKUMSFId) && !string.IsNullOrEmpty(rack.Name) && SkuDictionary.ContainsKey(rack.SKUMSFId) && SkuDictionary[rack.SKUMSFId] != null)
            {
                if (SkuDictionary[rack.SKUMSFId].LoadBalancerSpec != null)
                {
                    LBCount = SkuDictionary[rack.SKUMSFId].LoadBalancerSpec.QuantityPerRack;
                }

                if (SkuDictionary[rack.SKUMSFId].SwitchSpec != null)
                {
                    TORCount = SkuDictionary[rack.SKUMSFId].SwitchSpec.QuantityPerRack;
                }

                if (SkuDictionary[rack.SKUMSFId].IloSpec != null)
                {
                    ILOCount = SkuDictionary[rack.SKUMSFId].IloSpec.QuantityPerRack;
                }

                if (SkuDictionary[rack.SKUMSFId].DigiSpec != null)
                {
                    DIGICount = SkuDictionary[rack.SKUMSFId].DigiSpec.QuantityPerRack;
                }

                TotalNetDeviceCount = LBCount + TORCount + ILOCount + DIGICount;

                if (TotalNetDeviceCount > 0)
                {
                    AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDeviceCountValidation, null, "Network Devices", NotFound, TotalNetDeviceCount.ToString(), Fail, DeviceCountIncorrectMessage, rack.Name, rack.SKUMSFId));
                }
            }
        }

        private void SideAndSlotValidation(List<PhysicalAssetListValue> assetList, PhysicalAssetListValue rack, List<PhysicalRackLayout> assetLayout, bool exoServerFlag = false)
        {
            if (assetList != null && !string.IsNullOrEmpty(rack.Name))
            {
                foreach (var asset in assetList)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.ItemType) && !string.IsNullOrEmpty(asset.Side) && asset.SlotNumber != null)
                    {
                        bool validation = false;
                        bool slotCheck = false;
                        foreach (var sku in assetLayout)
                        {
                            if (sku != null && !string.IsNullOrEmpty(sku.Slot))
                            {
                                if (exoServerFlag)
                                {
                                    var slot = asset.Side + asset.SlotNumber.Value.ToString("D2");
                                    string slotNum = sku.Slot;
                                    slotCheck = slotNum.IndexOf(slot) >= 0;
                                    if (slotCheck)
                                    {
                                        validation = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    string side = sku.Slot.Substring(0, 2);
                                    string slotNum = sku.Slot.Substring(2, sku.Slot.Length - 2);
                                    if (side.IndexOf(asset.Side, StringComparison.OrdinalIgnoreCase) >= 0 && slotNum.IndexOf(asset.SlotNumber.Value.ToString()) >= 0)
                                    {
                                        validation = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (!validation)
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, PhysicalRackLayoutValidation, asset.Name, asset.ItemType, "Side: " + asset.Side + " Slot: " + asset.SlotNumber.Value, string.Join(".", GetSlots(assetLayout)), Fail, LayoutValidationFailureMessage, rack.Name, rack.SKUMSFId));
                        }
                    }
                }
            }
        }

        private void DskuValidation(List<PhysicalAssetListValue> assetList, PhysicalAssetListValue rack, TechSpec assetSpec, bool exoFlag = false)
        {
            if (!exoFlag)
                return;
            foreach (var asset in assetList)
            {
                if (asset != null && !string.IsNullOrEmpty(asset.DiscreteSKUMSFId) && !string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.ItemType))
                {
                    if (!(ExtractMSF(assetSpec.DskuName).Equals(ExtractMSF(asset.DiscreteSKUMSFId.Trim()), StringComparison.OrdinalIgnoreCase)))
                    {
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDskuValidation, asset.Name, asset.ItemType, asset.DiscreteSKUMSFId, assetSpec.DskuName, Fail, DskuMismatchMessage, rack.Name, rack.DiscreteSKUMSFId));
                    }
                }
            }
        }
        
        private void ServerDskuValidation(List<PhysicalAssetListValue> assetList, PhysicalAssetListValue rack, TechSpec assetSpec, bool exoFlag = false)
        {
            if (!exoFlag)
                return;
            foreach (var asset in assetList)
            {
                if (asset != null && !string.IsNullOrEmpty(asset.DiscreteSKUMSFId) && !string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.ItemType))
                {
                    if (!(ExtractMSF(assetSpec.DskuName).IndexOf(ExtractMSF(asset.DiscreteSKUMSFId.Trim()), StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, TechSpecDskuValidation, asset.Name, asset.ItemType, asset.DiscreteSKUMSFId, assetSpec.DskuName, Fail, DskuMismatchMessage, rack.Name, rack.SKUMSFId));
                    }
                }
            }
        }

        private void RackSecurityClassificationQc(string ProjectId, string MdmId, string WorkOrderName, string PG, List<PhysicalAssetListValue> racks, AllEgsOutputModel AllEgsOutputModel)
        {
            if (WorkOrderName.IndexOf("Operational", StringComparison.OrdinalIgnoreCase) < 0 && WorkOrderName.IndexOf("Pre-RTEG", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (racks != null && racks.Any())
            {
                var rackDetails = new MsAssetDataHandler().GetPhysicalAssetDetailByNames(racks.Where(r => !string.IsNullOrEmpty(r.Name)).Select(r => r.Name).ToList());
                if (rackDetails != null && rackDetails.Any())
                {
                    foreach (var rackDetail in rackDetails)
                    {
                        if (!rackDetail.IsSecure)
                        {
                            if ((PG.Trim().IndexOf("USSec EXO", StringComparison.OrdinalIgnoreCase) < 0) && (PG.Trim().IndexOf("USNat EXO", StringComparison.OrdinalIgnoreCase) < 0))
                            {
                                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Rack security classification QC", rackDetail.Name, rackDetail.ItemType, "False", "True", Fail, "Rack security classification is incorrect", null, null));
                            }
                        }
                    }
                }
            }
        }

        private void ValidateNetworkProjectCategory(List<PhysicalAssetListValue> assets)
        {
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    if (asset != null && asset.PropertyDimension != null && !asset.PropertyDimension.Trim().Equals("Network", StringComparison.OrdinalIgnoreCase))
                    {
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Property dimension incorrect for Netdevice", asset.Name, asset.ItemType, asset.PropertyDimension, "Network", Fail, "Incorrect Property dimension", null, null));
                    }
                    if (asset != null && asset.PropertyDimension == null)
                    {
                        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "Property dimension incorrect for Netdevice", asset.Name, asset.ItemType, asset.PropertyDimension, "Network", Fail, "Incorrect Property dimension", null, null));
                    }
                }
            }
        }

        private void BinValidation(List<PhysicalAssetListValue> assetList, PhysicalAssetListValue rack, List<PhysicalRackLayout> assetLayout, bool exoFlag = false)
        {
            if (!exoFlag)
                return;
            if (assetList != null && !string.IsNullOrEmpty(rack.Name))
            {
                foreach (var asset in assetList)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.ItemType) && !string.IsNullOrEmpty(asset.Side) && asset.BinNumber != null)
                    {
                        bool validation = false;
                        bool binCheck = false;
                        var bin = asset.BinNumber.HasValue ? asset.BinNumber.Value.ToString("D2") : string.Empty;
                        var binReport = bin + "00";
                        var total = asset.Side + asset.SlotNumber.Value.ToString("D2") + bin + "00";
                        foreach (var sku in assetLayout)
                        {
                            if (sku != null && !string.IsNullOrEmpty(sku.Slot))
                            {
                                var skuBin = sku.BinNumber.ToString("D2");
                                var skuTotal = sku.Slot + skuBin + "00";
                                binCheck = skuTotal.Equals(total, StringComparison.OrdinalIgnoreCase);
                                if (binCheck)
                                {
                                    validation = true;
                                    break;
                                }
                            }
                        }
                        if (!validation)
                        {
                            AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, PhysicalRackLayoutValidation, asset.Name, asset.ItemType, "Bin Number: " + binReport, string.Join(", ", GetBins(assetLayout, total)), Fail, LayoutValidationFailureMessage, rack.Name, rack.SKUMSFId));
                        }
                    }
                }
            }
        }

        private (List<PhysicalAssetListValue> ServerList, List<PhysicalAssetListValue> ChassisList, List<PhysicalAssetListValue> NetworkDeviceList, List<PhysicalAssetListValue> PowerUpsList) RackListExtraction(PhysicalAssetListValue rack)
        {
            try
            {
                var serverList = MsAssetPhysicalRackList.Where(asset => !string.IsNullOrEmpty(asset.Rack) && asset.Rack.Trim().Equals(rack.Name.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)).ToList();
                var chassisList = MsAssetPhysicalRackList.Where(asset => !string.IsNullOrEmpty(asset.Rack) && asset.Rack.Trim().Equals(rack.Name.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Chassis", StringComparison.OrdinalIgnoreCase)).ToList();
                var NetworkDeviceList = MsAssetPhysicalRackList.Where(asset => !string.IsNullOrEmpty(asset.Rack) && asset.Rack.Trim().Equals(rack.Name.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("NetworkDevice", StringComparison.OrdinalIgnoreCase)).ToList();
                var PowerUpsList = MsAssetPhysicalRackList.Where(asset => !string.IsNullOrEmpty(asset.Rack) && asset.Rack.Trim().Equals(rack.Name.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("PowerStrip", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(asset.Model) && asset.Model.Trim().IndexOf("Eaton", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                return (serverList, chassisList, NetworkDeviceList, PowerUpsList);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        private (List<PhysicalAssetListValue> ServerOnlyList, List<PhysicalAssetListValue> UpsList, List<PhysicalAssetListValue> ChassisManagerList) RackLayoutServerListBuild(List<PhysicalAssetListValue> serverList)
        {
            var ServerOnlyList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && (server.Model.Trim().IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0 || server.Model.Trim().IndexOf("G9", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var UpsList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && server.Model.Trim().IndexOf("Eaton", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var ChassisManagerList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && server.Model.Trim().IndexOf("WCS Chassis Manager", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            return (ServerOnlyList, UpsList, ChassisManagerList);
        }

        private (List<PhysicalAssetListValue> LoadBalancerList, List<PhysicalAssetListValue> SwitchList, List<PhysicalAssetListValue> IloList, List<PhysicalAssetListValue> DigiList) RackLayoutNetworkListBuild(List<PhysicalAssetListValue> networkDeviceList, bool exoFlag = false)
        {
            List<PhysicalAssetListValue> SwitchList;

            if (exoFlag)
            {
                SwitchList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(netdevice.Name) && (netdevice.Name.Trim().IndexOf("T0", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("hl", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }
            else
            {
                SwitchList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().Equals("Switch", StringComparison.OrdinalIgnoreCase) && ((!string.IsNullOrEmpty(netdevice.Name) && (netdevice.Name.Trim().IndexOf("T0", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("hl", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("xcg", StringComparison.OrdinalIgnoreCase) >= 0)))).ToList();
            }

            var LoadBalancerList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(netdevice.Name) && (netdevice.Name.Trim().IndexOf("HLB", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("xcg", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var IloList = networkDeviceList.Where(netdevice => (!string.IsNullOrEmpty(netdevice.Name) && netdevice.Name.Trim().IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0) || (!string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var DigiList = networkDeviceList.Where(netdevice => (!string.IsNullOrEmpty(netdevice.Name) && netdevice.Name.Trim().IndexOf("DIGI", StringComparison.OrdinalIgnoreCase) >= 0) || (!string.IsNullOrEmpty(netdevice.Manufacturer) && netdevice.Manufacturer.Trim().IndexOf("DIGI", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            return (LoadBalancerList, SwitchList, IloList, DigiList);
        }

        private (List<PhysicalAssetListValue> ServerOnlyList, List<PhysicalAssetListValue> UpsList, List<PhysicalAssetListValue> ChassisManagerList) DeviceCountServerListBuild(List<PhysicalAssetListValue> serverList, bool exoFlag = false)
        {
            List<PhysicalAssetListValue> ServerOnlyList = null;
            if (exoFlag)
            {
                ServerOnlyList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && (server.Model.Trim().IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0
                    || server.Model.Trim().IndexOf("G9", StringComparison.OrdinalIgnoreCase) >= 0)
                    || server.ItemType.Trim().IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            else
            {
                ServerOnlyList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && (server.Model.Trim().IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0
                    || server.Model.Trim().IndexOf("G9", StringComparison.OrdinalIgnoreCase) >= 0
                    || server.Model.Trim().IndexOf("ZT Systems FPGA Compute", StringComparison.OrdinalIgnoreCase) >= 0
                    || server.Model.Trim().IndexOf("ZT Web FPGA", StringComparison.OrdinalIgnoreCase) >= 0)
                    || server.ItemType.Trim().IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            var UpsList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && server.Model.Trim().IndexOf("Eaton", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var ChassisManagerList = serverList.Where(server => !string.IsNullOrEmpty(server.Model) && server.Model.Trim().IndexOf("WCS Chassis Manager", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            return (ServerOnlyList, UpsList, ChassisManagerList);
        }

        private (List<PhysicalAssetListValue> LoadBalancerList, List<PhysicalAssetListValue> SwitchList, List<PhysicalAssetListValue> IloList, List<PhysicalAssetListValue> DigiList) DeviceCountNetworkListBuild(List<PhysicalAssetListValue> networkDeviceList, bool exoFlag = false)
        {
            List<PhysicalAssetListValue> LoadBalancerList;
            
            if (exoFlag)
            {
                LoadBalancerList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            else
            {
                LoadBalancerList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && ((netdevice.NetworkDeviceType.Trim().IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(netdevice.Name) && (netdevice.Name.Trim().IndexOf("HLB", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("xcg", StringComparison.OrdinalIgnoreCase) >= 0)) || (netdevice.NetworkDeviceType.Trim().IndexOf("Load Balancer") >= 0))).ToList();
            }

            var SwitchList = networkDeviceList.Where(netdevice => !string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().Equals("Switch", StringComparison.OrdinalIgnoreCase) && ((!string.IsNullOrEmpty(netdevice.Name) && (netdevice.Name.Trim().IndexOf("T0", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("hl", StringComparison.OrdinalIgnoreCase) >= 0 || netdevice.Name.Trim().IndexOf("xcg", StringComparison.OrdinalIgnoreCase) >= 0)))).ToList();
            var IloList = networkDeviceList.Where(netdevice => (!string.IsNullOrEmpty(netdevice.Name) && netdevice.Name.Trim().IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0) || (!string.IsNullOrEmpty(netdevice.NetworkDeviceType) && netdevice.NetworkDeviceType.Trim().IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            var DigiList = networkDeviceList.Where(netdevice => (!string.IsNullOrEmpty(netdevice.Name) && netdevice.Name.Trim().IndexOf("DIGI", StringComparison.OrdinalIgnoreCase) >= 0) || (!string.IsNullOrEmpty(netdevice.Manufacturer) && netdevice.Manufacturer.Trim().IndexOf("DIGI", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            return (LoadBalancerList, SwitchList, IloList, DigiList);
        }

        private List<string> GetSlots(List<PhysicalRackLayout> deviceLayout)
        {
            var slots = new List<string>();
            if (deviceLayout == null)
            {
                return slots;
            }

            foreach (var device in deviceLayout)
            {
                slots.Add(device.Slot);
            }
            return slots;
        }

        private HashSet<string> GetBins(List<PhysicalRackLayout> deviceLayout, string specificLayout)
        {
            var bins = new HashSet<string>();
            if (deviceLayout == null)
            {
                return bins;
            }

            foreach (var device in deviceLayout)
            {
                var binOnly = device.BinNumber.ToString("D2") + "00";
                var newBin = device.Slot + device.BinNumber.ToString("D2") + "00";
                var binCompare = specificLayout.IndexOf(device.Slot, StringComparison.OrdinalIgnoreCase);
                if (binCompare >= 0)
                    bins.Add(binOnly);
            }
            return bins;
        }

        private string ExtractMSF(string sku)
        {
            if (string.IsNullOrEmpty(sku))
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            var skuList = sku.Split(';');

            foreach (var eachSku in skuList)
            {
                var parts = eachSku.Split('-');

                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Equals("MSF", StringComparison.OrdinalIgnoreCase) && i < (parts.Length - 1))
                    {
                        sb.Append(parts[i] + "-" + parts[i + 1] + ';');
                    }
                }
            }

            return sb.ToString();
        }
    }
}
