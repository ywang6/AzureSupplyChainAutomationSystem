using AssetQc.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using Utilities;
using Utilities.Model;

namespace AssetQc
{
    public class FopeDiscreteQc
    {
        public void FopeDiscreteCheck(string Pid, string PropertyGroup, string WorkOrderName, AllEgsOutputModel AllEgsOutputModel)
        {
            // Verify if VlanID is correct using Kusto data
            VlanIdCheck(Pid, WorkOrderName, AllEgsOutputModel);

        }

        public void VlanIdCheck(string Pid, string WorkOrderName, AllEgsOutputModel AllEgsOutputModel)
        {
            // Get TOR information from OA DB
            List<FopeVlanInfo> VlanList = GetVlanInfoFromCache(Pid);
            if (!VlanList.Any())
            {
                return;
            }

            KustoAccess kusto = new KustoAccess();
            StringBuilder errorDescription = new StringBuilder();

            try
            {
                foreach (FopeVlanInfo vlan in VlanList)
                {
                    var KustoVlanId = kusto.GetVlanInfoByTorName(vlan.TorName);
                    Thread.Sleep(500);
                    if (KustoVlanId.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        errorDescription.AppendLine(String.Format("FOPE Discrete VlanId check failed: TorName {0} not found in Kusto", vlan.TorName));
                        continue;
                    }

                    // Compare Kusto Vlan ID with SDL Vlan ID 
                    if (KustoVlanId.IndexOf(vlan.SDLVlanId) < 0)
                    {
                        errorDescription.AppendLine(String.Format("FOPE Discrete Vlan ID mismatch, expected value: {0}, actual value: {1}, TOR Name: {2}", vlan.SDLVlanId, "VlanId Not found in Kusto", vlan.TorName));
                    }
                }
            }
            catch(Exception ex)
            {
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Fope Vlan Id check failed with exception", "Fope vlan Id check exception: " + ex);
                return;
            }

            if (errorDescription.Length != 0)
            {
                AllEgsOutputModel.AllEgsOutput.Add(Common.CreateResultObject(Pid, null, WorkOrderName, "423 - Vlan association missing in MSAsset",
                            null, null, "Empty", "Not Empty", "Failed", errorDescription.ToString(), null, null));
            }
        }

        // Get SDL Vlan information from OA DB
        public List<FopeVlanInfo> GetVlanInfoFromCache(string Pid)
        {
            List<FopeVlanInfo> FopeVlanList = new List<FopeVlanInfo>();
            var selectQuery = "SELECT * FROM [mcio_oa_db].[FopeQcTable] WHERE ProjectId='" + Pid + "'";

            using (var connection = new SqlConnection(ConnectionHandler.ConnectionString))
            {
                try
                {
                    var command = new SqlCommand(selectQuery, connection)
                    {
                        CommandType = CommandType.Text
                    };

                    connection.Open();

                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var FopeVlanInfoModel = new DataTable();
                    objSqlDataAdapter.Fill(FopeVlanInfoModel);

                    if (FopeVlanInfoModel.Rows.Count > 0)
                    {
                        for (int i = 0; i < FopeVlanInfoModel.Rows.Count; i++)
                        {
                            FopeVlanInfo fopeVlanObj = new FopeVlanInfo();
                            var drRow = FopeVlanInfoModel.Rows[i];
                            var ProjectId = drRow.IsNull("ProjectId") ? null : drRow["ProjectId"].ToString();
                            var TorName = drRow.IsNull("TorName") ? null : drRow["TorName"].ToString();
                            //var IloTorPort = drRow.IsNull("IloTorPort") ? null : drRow["IloTorPort"].ToString();
                            var SDLVlanID = drRow.IsNull("SDLVlanID") ? null : drRow["SDLVlanID"].ToString();

                            fopeVlanObj.ProjectId = ProjectId;
                            fopeVlanObj.TorName = TorName;
                            //fopeVlanObj.IloTorPort = IloTorPort;
                            fopeVlanObj.SDLVlanId = SDLVlanID;
                            FopeVlanList.Add(fopeVlanObj);
                        }
                    }
                    else
                    {
                        // Fope pids not in OA DB yet, will send out email notification to input vlan information
                        string body = "FOPE Discrete Pid: " + Pid + " coming to our queue, please input Vlan information in FOPE Info Upload Page.";
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.OADPMAccount, "Please input FOPE Discrete Vlan information for " + Pid, body);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception in GetVlanInfoFromCache method", ex);
                }
                finally
                {
                    connection.Close();
                }
                return FopeVlanList;
            }
        }
    }
}
