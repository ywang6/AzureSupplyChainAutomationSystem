using System;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Utilities;
using GdcoTicket;
using GdcoTicket.Model;
using DeploymentService;
using DeploymentService.Model;

namespace DataSync
{
    public class DsDataSync
    {
        public void SyncDsDataSet()
        {
            List<DeploymentServiceStore> dsDataSet = new List<DeploymentServiceStore>();
            var errorSubject = "Error in syncing Deployment Service Data";
            var take = 100;
            var skip = 0;

            try
            {
                DSAccess ds = new DSAccess();
                GdcoTicketHandler gt = new GdcoTicketHandler();
                List<string> FulfillmentIdList = new List<string>();
                dsDataSet = ds.GetDeploymentServiceStoreList();

                foreach (DeploymentServiceStore dss in dsDataSet)
                {
                    FulfillmentIdList.Add(dss.fulfillmentId);
                }

                if (FulfillmentIdList.Any())
                {
                    Dictionary<string, List<TicketingObject>> ticketingDic = new Dictionary<string, List<TicketingObject>>();
                    // Add paging here and pass in 200 FulfillmentIds per time
                    int total = FulfillmentIdList.Count;
                    take = 200;
                    skip = 0;

                    while (skip < total)
                    {
                        var CurrentFulfillmentIdList = FulfillmentIdList.Skip(skip).Take(take).ToList();
                        Dictionary<string, List<TicketingObject>> tempDic = gt.GetTicketsByFulfillmentId(CurrentFulfillmentIdList);
                        foreach (var row in tempDic)
                        {
                            if (!ticketingDic.ContainsKey(row.Key))
                            {
                                ticketingDic.Add(row.Key, row.Value);
                            }
                        }
                        skip = skip + take;
                    }
                    
                    // Put ticketingObjectList to related DeploymentServiceStore by fullfillmentId
                    foreach (var dss in dsDataSet)
                    {
                        if (ticketingDic.ContainsKey(dss.fulfillmentId))
                        {
                            dss.TicketList = ticketingDic[dss.fulfillmentId];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, errorSubject, ex.ToString());
            }

            if (dsDataSet == null || !dsDataSet.Any())
            {
                return;
            }

            // Before we write DS data into database, we filter out all of the DSData which has null ticketList
            List<DeploymentServiceStore> dsFinalList = new List<DeploymentServiceStore>();
            Dictionary<string, DeploymentServiceStore> FidDeploymentDic = new Dictionary<string, DeploymentServiceStore>();

            foreach (var dsData in dsDataSet)
            {
                if (dsData.TicketList != null && dsData.TicketList.Any())
                {
                    dsFinalList.Add(dsData);
                    if (!FidDeploymentDic.ContainsKey(dsData.fulfillmentId))
                    {
                        FidDeploymentDic.Add(dsData.fulfillmentId, dsData);
                    }
                }
            }

            // Get FulfillmentId to DemandId mapping
            Dictionary<string, string> CpsFidTitleDic = new Dictionary<string, string>();
            if (FidDeploymentDic.Any())
            {
                GDCOTicketStoreHandler GDCOTicketStoreHandler = new GDCOTicketStoreHandler();
                Dictionary<string, string> FidToDemandIdMapping = GDCOTicketStoreHandler.GetFidToDemandIdMapping(FidDeploymentDic.Keys.ToList());

                // Get CPS FidList, it contains all of the fids which are CPS
                CpsFidTitleDic = GDCOTicketStoreHandler.GetCpsFidTitleDic(FidToDemandIdMapping);
                bool sendEmail = false;

                if (CpsFidTitleDic != null && CpsFidTitleDic.Any())
                {
                    StringBuilder body = new StringBuilder();
                    body.AppendLine("<style> table {    font-family: arial, sans-serif;    border-collapse: collapse;    width: 100%;}td, th {    border: 1px solid #dddddd;    text-align: left;    padding: 8px;}tr:nth-child(even) {    background-color: #dddddd;} h3 {    font-family: arial, sans-serif; color:#2F4F4F;}</style>");
                    body.AppendLine("<p><h3>Please QC following CPS Fids: </h3></p>");
                    body.AppendLine("<table>");
                    body.AppendLine("<tr><td>FulfillmentId</td><td>Title</td><td>TicketCreatedDate</td><td>EngineerGroup</td><td>PropertyGroup/td></tr>");

                    foreach (var fid in CpsFidTitleDic.Keys)
                    {
                        var DsData = FidDeploymentDic[fid];
                        var title = CpsFidTitleDic[fid];
                        var engineerGroup = DsData.EngineeringGroup;
                        var propertyGroup = DsData.PGName;
                        string ticketCreationDate = "NA";
                        foreach (var ticket in DsData.TicketList)
                        {
                            // check if current fid has OA task, if yes, we should QC it
                            if (ticket.GDCOFaultCode.Equals("124110"))
                            {
                                ticketCreationDate = ticket.CreatedDate.ToString();
                                sendEmail = true;
                                body.AppendLine("<tr><td>" + fid + "</td><td>" + title + "</td><td>" + ticketCreationDate + "</td><td>" + engineerGroup + "</td><td>" + propertyGroup + "</td></tr>");
                            }
                        }
                    }
                    body.AppendLine("</table>");

                    TimeSpan start = new TimeSpan(8, 0, 0); //8 o'clock
                    TimeSpan end = new TimeSpan(10, 0, 0); //10 o'clock
                    TimeSpan now = DateTime.Now.TimeOfDay;

                    if (sendEmail && (now >= start) && (now <= end))
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.OADPMAccount, "CPS Fid List", body.ToString());
                    }
                }
            }

            take = 100;
            skip = 0;

            while (skip < dsFinalList.Count)
            {
                var currentdsDataSet = dsFinalList.Skip(skip).Take(take);
                using (var connection = new SqlConnection(ConnectionHandler.ConnectionString))
                {
                    try
                    {
                        var command = new SqlCommand("mcio_oa_db.prc_UpdateDsDataSet", connection)
                        {
                            CommandType = CommandType.StoredProcedure
                        };

                        var dsDataTable = new DataTable("mcio_oa_db.DsDataSetTableType");

                        dsDataTable.Columns.Add("FulfillmentId", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("MDMIdList", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeploymentId", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("ResourceTypeList", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("GroupType", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("EngineeringGroup", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeploymentDCCode", typeof(string)).AllowDBNull = true;                 
                        dsDataTable.Columns.Add("DeploymentPGName", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("PDimmension", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeploymentClusterName", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeploymentStatus", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeploymentCreatedDate", typeof(DateTime)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketId", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketState", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("CurrentSeverity", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("GDCOFaultCode", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("GDCOFaultDescription", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketTitle", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("ClusterName", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketDCCode", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketPGName", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("DeliveryNumber", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TemplateType", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketCreatedDate", typeof(DateTime)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketAssignedDate", typeof(DateTime)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketResolvedDate", typeof(DateTime)).AllowDBNull = true;
                        dsDataTable.Columns.Add("TicketDueDate", typeof(DateTime)).AllowDBNull = true;
                        dsDataTable.Columns.Add("WasSLABreached", typeof(string)).AllowDBNull = true;
                        dsDataTable.Columns.Add("PurchaseOrderNumber", typeof(string)).AllowDBNull = true;

                        foreach (var dsData in currentdsDataSet)
                        {
                            var FulfillmentId = dsData.fulfillmentId != null ? dsData.fulfillmentId : "NA";
                            var MDMIdList = dsData.MDMIDList != null ? dsData.MDMIDList : "NA";
                            var DeploymentId = dsData.deploymentId != null ? dsData.deploymentId : "NA";
                            var resourceTypeList = dsData.ResourceTypeList != null ? dsData.ResourceTypeList : "NA";
                            var groupType = dsData.GroupType != null ? dsData.GroupType : "NA";

                            var engineeringGroup = "NA";
                            var DeploymentPGName = "NA";
                            if (dsData!= null && dsData.fulfillmentId != null && CpsFidTitleDic != null && CpsFidTitleDic.ContainsKey(dsData.fulfillmentId))
                            {
                                var title = CpsFidTitleDic[dsData.fulfillmentId];
                                if (title.IndexOf("CDDS", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    engineeringGroup = "CDDS";
                                    DeploymentPGName = "CDDS";

                                    if (title.IndexOf("FI", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        engineeringGroup = "CDDS FI";
                                        DeploymentPGName = "CDDS FI";
                                    }
                                }
                                else if (title.IndexOf("IS ", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    engineeringGroup = "IS";
                                    DeploymentPGName = "IS";

                                    if (title.IndexOf("FI", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        engineeringGroup = "IS FI";
                                        DeploymentPGName = "IS FI";
                                    }
                                }
                                else if (title.IndexOf("DPS", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    engineeringGroup = "DPS";
                                    DeploymentPGName = "DPS";
                                }
                                else if (title.IndexOf("ADNS", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    engineeringGroup = "ADNS";
                                    DeploymentPGName = "ADNS";
                                }
                            }
                            else
                            {
                                engineeringGroup = dsData.EngineeringGroup;
                                DeploymentPGName = dsData.PGName;
                            }

                            var DeploymentDCCode = dsData.dcCode != null ? dsData.dcCode : "NA";
                            var PDimmension = dsData.PDimmension;
                            var DeploymentClusterName = dsData.ClusterName != null ? dsData.ClusterName : "NA";
                            var DeploymentStatus = dsData.Status != null ? dsData.Status : "NA";
                            var DeploymentCreatedDate = dsData.CreatedDate;
                            var PurchaseOrderNumber = dsData.PurchaseOrderNumber != null ? dsData.PurchaseOrderNumber : "NA";

                            if (dsData.TicketList != null && dsData.TicketList.Any())
                            {
                                foreach (var dsTicket in dsData.TicketList)
                                {
                                    var TicketId = dsTicket.TicketId;
                                    var TicketState = dsTicket.StateName;
                                    var CurrentSeverity = dsTicket.CurrentSeverity;
                                    var GDCOFaultCode = dsTicket.GDCOFaultCode;
                                    var GDCOFaultDescription = dsTicket.GDCOFaultDescription;
                                    var TicketTitle = dsTicket.TicketTitle;
                                    var TicketClusterName = dsTicket.ClusterName;
                                    var TicketDatacenterCode = dsTicket.DatacenterCode;
                                    var TicketPropertyGroupName = dsTicket.PropertyGroupName;
                                    var DeliveryNumber = dsTicket.DeliveryNumber;
                                    var TemplateType = dsTicket.TemplateType;
                                    var TicketCreatedDate = dsTicket.CreatedDate;
                                    var TicketAssignedDate = dsTicket.AssignedDate;
                                    var TicketResolvedDate = dsTicket.ResolvedDate;
                                    var TicketDueDate = dsTicket.DueDate;
                                    var WasSLABreached = dsTicket.WasSLABreached;

                                    dsDataTable.Rows.Add(
                                        FulfillmentId,
                                        MDMIdList,
                                        DeploymentId,
                                        resourceTypeList,
                                        groupType,
                                        engineeringGroup,
                                        DeploymentDCCode,
                                        DeploymentPGName,
                                        PDimmension,
                                        DeploymentClusterName,
                                        DeploymentStatus,
                                        DeploymentCreatedDate,
                                        TicketId,
                                        TicketState,
                                        CurrentSeverity,
                                        GDCOFaultCode,
                                        GDCOFaultDescription,
                                        TicketTitle,
                                        TicketClusterName,
                                        TicketDatacenterCode,
                                        TicketPropertyGroupName,
                                        DeliveryNumber,
                                        TemplateType,
                                        TicketCreatedDate,
                                        TicketAssignedDate,
                                        TicketResolvedDate,
                                        TicketDueDate,
                                        WasSLABreached,
                                        PurchaseOrderNumber
                                        );
                                }
                            }
                        }

                        command.Parameters.Add(new SqlParameter
                        {
                            ParameterName = "@DsDataSetTvp",
                            SqlDbType = SqlDbType.Structured,
                            Value = dsDataTable
                        });

                        connection.Open();
                        command.ExecuteScalar();
                        connection.Close();
                    }
                    catch (Exception ex)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, errorSubject, $"Take = {take} Skip = {skip} {ex.ToString()}");
                    }
                }
                skip = skip + take;
            }
        }
    }
}
