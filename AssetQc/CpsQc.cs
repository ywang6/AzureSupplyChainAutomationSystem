using AssetQc.Model;
using MsAsset;
using MsAsset.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Linq;
using System.Text;
using Utilities;

namespace AssetQc
{
    public class CpsQc
    {
        private const string Pass = "Passed";
        private const string Fail = "Failed";
        private const string FailIgnore = "Failed-Ignored";
        private List<string> ServerNameCheckList = new List<string>(new string[] { "DPS", "CDDS", "IS" });

        public void CpsValidation(string ProjectId, string MdmId, string WorkOrderName, List<PhysicalAssetListValue> MsAssetPhysicalAssetList, AllEgsOutputModel AllEgsOutputModel)
        {
            ServerNameCheck(ProjectId, MdmId, WorkOrderName, MsAssetPhysicalAssetList, AllEgsOutputModel);
            VlanCheck(ProjectId, MdmId, WorkOrderName, MsAssetPhysicalAssetList, AllEgsOutputModel);
            PingTest(ProjectId, MdmId, WorkOrderName, MsAssetPhysicalAssetList, AllEgsOutputModel);
            PropertyCheck(ProjectId, MdmId, WorkOrderName, MsAssetPhysicalAssetList, AllEgsOutputModel);
        }

        // validate if server names match
        public void ServerNameCheck(string ProjectId, string MdmId, string WorkOrderName, List<PhysicalAssetListValue> MsAssetPhysicalAssetList, AllEgsOutputModel AllEgsOutputModel)
        {
            var ServerList = MsAssetPhysicalAssetList.Where(asset => !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)).ToList();
            var DcCode = AllEgsOutputModel.DataCenterCode;

            foreach (var server in ServerList)
            {
                if ((server.Name.IndexOf(DcCode, StringComparison.OrdinalIgnoreCase) < 0) ||
                    (!ServerNameRulesCheck(ServerNameCheckList, server.Name)))
                {
                    AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "404 - Naming convention incorrect", server.Name, server.ItemType,
                        server.Name, "", Fail, "Naming convention incorrect", null, null));
                }
            }
        }

        // validate if vlan all match, deprecated due to new API change
        public void VlanCheck(string ProjectId, string MdmId, string WorkOrderName, List<PhysicalAssetListValue> MsAssetPhysicalAssetList, AllEgsOutputModel AllEgsOutputModel)
        {
            //var ServerList = MsAssetPhysicalAssetList.Where(asset => !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)).ToList();
            //var DcCode = AllEgsOutputModel.DataCenterCode;
            //List<string> VlanNameList = new List<string>();

            //MsAssetDataHandler msd = new MsAssetDataHandler();

            //foreach (var server in ServerList)
            //{
            //    var ServerAssociations = msd.GetAssociationByAssetId(server.Id.ToString());

            //    if (ServerAssociations != null && ServerAssociations.AssociationValues != null && ServerAssociations.AssociationValues.Any())
            //    {
            //        foreach (var ServerAssociation in ServerAssociations.AssociationValues)
            //        {
            //            if (ServerAssociation.TargetType != null && ServerAssociation.TargetType.Equals("InternetAddress"))
            //            {
                            
            //            }
            //            else
            //            {
            //                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, null, WorkOrderName, "424 - IP association missing in MSAsset", "", "IP association",
            //                        null, null, Fail, "IP Association missing", null, null));
            //            }
            //        }
            //    }
            //    else
            //    {
            //        AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, null, WorkOrderName, "424 - IP association missing in MSAsset", "", "IP association",
            //            null, null, Fail, "IP or Vlan Association missing", null, null));
            //    }
            //}
        }

        // Ping test
        public void PingTest(string ProjectId, string MdmId, string WorkOrderName, List<PhysicalAssetListValue> MsAssetPhysicalAssetList, AllEgsOutputModel AllEgsOutputModel)
        {
            List<string> IpList = new List<string>();
            List<string> ILOMissingServerList = new List<string>();
            var ServerList = MsAssetPhysicalAssetList.Where(asset => !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)).ToList();
            
            // loop through all servers to get associated IP
            if (ServerList != null && ServerList.Any())
            {
                int total = ServerList.Count;
                int take = 100;
                int skip = 0;
                var handler = new HttpClientHandler { UseDefaultCredentials = true };
                var client = new HttpClient(handler);

                while (skip < total)
                {
                    var currentAssetList = ServerList.Skip(skip).Take(take).ToList();
                    int a = 0;
                    StringBuilder FilterContent = new StringBuilder();
                    var assetAssociationUrl = Constants.MsAssetBaseUri + "ConfigItemAssociation()?$filter=(";
                    List<string> tempAssetNameList = new List<string>();

                    for (; a < currentAssetList.Count - 1; a++)
                    {
                        string content = String.Format("SourceConfigItemId eq {0}", currentAssetList[a].Id);
                        FilterContent.Append(content);
                        FilterContent.Append(" or ");
                        tempAssetNameList.Add(currentAssetList[a].Name);
                    }
                    FilterContent.Append(String.Format("SourceConfigItemId eq {0}", currentAssetList[a].Id));
                    FilterContent.Append(")" + "&$format=json");
                    assetAssociationUrl += FilterContent.ToString();

                    var reponse = client.GetStringAsync(assetAssociationUrl).Result;
                    var odata = JsonConvert.DeserializeObject<AssociationOdata>(reponse);

                    // Get IPs
                    if (odata != null && odata.AssociationValues != null && odata.AssociationValues.Any())
                    {
                        for (int i = 0; i < odata.AssociationValues.Count; i++)
                        {
                            if (odata.AssociationValues[i].TargetType.Equals("InternetAddress") && odata.AssociationValues[i].TargetConfigItemName != null)
                            {
                                IpList.Add(odata.AssociationValues[i].TargetConfigItemName);
                            }
                            else
                            {
                                ILOMissingServerList.AddRange(tempAssetNameList);
                            }
                        }
                    }

                    skip = skip + take;
                }
            }
            // cut the ticket if any server doesn't have IP associations
            StringBuilder errorDescription = new StringBuilder("HardwareAssetUpload - ILO information missing");
            errorDescription.AppendLine("Following servers are missing ILO IP information in the AssociationValues tab - ");
            errorDescription.AppendLine("Please update the Internet Address for each server with actual ILO IP any concerns please reach out to oadpm@gmail.com");
            errorDescription.AppendLine();
            foreach (string server in ILOMissingServerList)
            {
                errorDescription.AppendLine(server);
            }

            if (ILOMissingServerList.Any())
            {
                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, null, WorkOrderName, "424 - IP association missing in MSAsset",
                                null, null, "Empty", "Not Empty", Fail, errorDescription.ToString(), null, null));
            }

            // Generate csv file for these IPs
            if (!IpList.Any()) return;

            GenerateCSV(IpList, ProjectId);
        }

        // Property Group and Property Dimension check
        public void PropertyCheck(string ProjectId, string MdmId, string WorkOrderName, List<PhysicalAssetListValue> MsAssetPhysicalAssetList, AllEgsOutputModel AllEgsOutputModel)
        {
            var ServerList = MsAssetPhysicalAssetList.Where(asset => !string.IsNullOrEmpty(asset.ItemType) && asset.ItemType.Trim().Equals("Server", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var asset in ServerList)
            {
                if (!ServerNameRulesCheck(ServerNameCheckList, asset.Name))
                {
                    AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "421 - Property dimension incorrect for Server", asset.Name, asset.ItemType,
                        asset.PropertyDimension, "IS/CDDS/DPS", Fail, "MsAsset Property Dimension not correct", null, null));
                }

                if (asset.PropertyGroup.IndexOf("Core Platform Services", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(ProjectId, MdmId, WorkOrderName, "404 - Naming convention incorrect", asset.Name, asset.ItemType,
                        asset.PropertyGroup, "Core Platform Services", Fail, "MsAsst Property Group not correct", null, null));
                }
            }
        }

        // Check if current server name meets the rule which defined in the list
        public bool ServerNameRulesCheck(List<string> ServerNameCheckList, string ServerName)
        {
            foreach (var rule in ServerNameCheckList)
            {
                if (ServerName.IndexOf(rule, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public string GenerateCSV(List<string> ILOIPsList, string pid)
        {

            if (!ILOIPsList.Any())
            {
                return Constants.NoILOIP;
            }

            string filePath = "\\" + Constants.ILOCSVFolder + "\\ILOList-" + pid + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + ".csv";
            var csv = new StringBuilder();

            foreach (string ip in ILOIPsList)
            {
                var newline = string.Format("{0}", ip);
                csv.AppendLine(newline);
            }
            try
            {
                System.IO.File.WriteAllText(filePath, csv.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("SPOILOIPCollector: Exception in write ILO IPs to CSV file, Exception: " + ex);
            }

            return filePath;
        }

    }
}
