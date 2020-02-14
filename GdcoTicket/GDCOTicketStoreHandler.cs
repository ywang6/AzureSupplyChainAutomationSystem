using GdcoTicket.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using Utilities.Model;

namespace GdcoTicket
{
    public class GDCOTicketStoreHandler
    {
        public List<string> GetRecentProjects()
        {
            List<string> projects = new List<string>();

            using (var connection = new SqlConnection(ConnectionHandler.DeraAzureDBConnectionString))
            {
                try
                {
                    var command = new SqlCommand(GetRecentProjectsQuery(), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var RecentProjectModel = new DataTable();
                    objSqlDataAdapter.Fill(RecentProjectModel);

                    if (RecentProjectModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in RecentProjectModel.Rows)
                        {
                            var fulfillmentId = row["FulfillmentID"].ToString();
                            var mdmid = row["MDMID"].ToString();

                            // Fulfillmentid is the king.
                            if (!String.IsNullOrEmpty(fulfillmentId) && !String.IsNullOrEmpty(mdmid)) projects.Add(fulfillmentId);
                            if (String.IsNullOrEmpty(fulfillmentId)) projects.Add(mdmid);
                            else if (String.IsNullOrEmpty(mdmid)) projects.Add(fulfillmentId);
                        }
                    }
                    return projects;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        // Get Dera Seed File objects from [reportview].[vwSeedFileVersion]
        public List<DeraSeedFile> GetDeraSeedFileObjects(List<string> projectIds)
        {
            if (projectIds == null || !projectIds.Any())
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.DeraAzureDBConnectionString))
            {
                try
                {
                    var command = new SqlCommand(GetSeedFileMetaData(projectIds), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var DeraSeedFileModel = new DataTable();
                    objSqlDataAdapter.Fill(DeraSeedFileModel);

                    List<DeraSeedFile> DeraSeedFileList = new List<DeraSeedFile>();
                    if (DeraSeedFileModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in DeraSeedFileModel.Rows)
                        {
                            DeraSeedFile df = new DeraSeedFile();
                            df.FulfillmentId = row["FulfillmentId"].ToString();
                            df.VersionSequence = row["VersionSequence"].ToString();
                            df.RawVersion = Int32.Parse(row["RawVersion"].ToString());
                            df.CreatedDate = row["CreatedDate"].ToString();
                            df.LastModifiedDate = row["LastModifiedDate"].ToString();
                            df.DeploymentId = row["DeploymentId"].ToString();
                            df.PhysicalDataCenterCode = row["PhysicalDataCenterCode"].ToString();
                            df.SeedType = row["SeedType"].ToString();
                            df.Status = row["Status"].ToString();
                            df.isSKUChanged = Int32.Parse(row["isSKUChanged"].ToString());
                            df.isTileChanged = Int32.Parse(row["isTileChanged"].ToString());
                            df.isColoChanged = Int32.Parse(row["isColoChanged"].ToString());
                            df.isClusterNameChanged = Int32.Parse(row["isClusterNameChanged"].ToString());
                            df.isClusterTemplateChanged = Int32.Parse(row["isClusterTemplateChanged"].ToString());
                            df.isSecondarySKUChanged = Int32.Parse(row["isSecondarySKUChanged"].ToString());
                            df.isPropertyChanged = Int32.Parse(row["isPropertyChanged"].ToString());
                            df.isMaxPublishedRevision = Int32.Parse(row["isMaxPublishedRevision"].ToString());
                            df.isIntentChanged = Int32.Parse(row["isIntentChanged"].ToString());
                            df.isHostNameChanged = Int32.Parse(row["isHostNameChanged"].ToString());
                            df.isAvailabilityZoneChanged = Int32.Parse(row["isAvailabilityZoneChanged"].ToString());
                            df.isDCChanged = Int32.Parse(row["isDCChanged"].ToString());
                            df.isRegionChanged = Int32.Parse(row["isRegionChanged"].ToString());
                            df.ismorchanged = Int32.Parse(row["ismorchanged"].ToString());
                            df.ist1skuchanged = Int32.Parse(row["ist1skuchanged"].ToString());
                            df.isFabricControllerNodeCountChanged = Int32.Parse(row["isFabricControllerNodeCountChanged"].ToString());
                            df.isUpLinkSpeedChanged = Int32.Parse(row["isUpLinkSpeedChanged"].ToString());
                            df.isC0SKUChanged = Int32.Parse(row["isC0SKUChanged"].ToString());

                            DeraSeedFileList.Add(df);
                        }
                    }
                    return DeraSeedFileList;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        public Dictionary<string, string> GetCpsFidTitleDic (Dictionary<string, string> FidToDemandIdMapping)
        {
            if (FidToDemandIdMapping == null)
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.DeraAzureDBConnectionString))
            {
                try
                {
                    Dictionary<string,string> DemandIdToTitleDic = new Dictionary<string, string>();
                    Dictionary<string, string> CpsFidTitleDic = new Dictionary<string, string>();

                    var command = new SqlCommand(GetMsPODTitleQuery(FidToDemandIdMapping.Values.ToList()), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var TicketStoreModel = new DataTable();
                    objSqlDataAdapter.Fill(TicketStoreModel);

                    if (TicketStoreModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in TicketStoreModel.Rows)
                        {
                            if (!row.IsNull("Id") && !row.IsNull("title"))
                            {
                                DemandIdToTitleDic.Add(row["Id"].ToString(), row["title"].ToString());
                            }
                        }
                    }

                    // Iterate over all Fids and check if they are CPS Fids
                    // We just QC whose title contains FI, which means First instance
                    foreach (var Fid in FidToDemandIdMapping.Keys)
                    {
                        var demandId = FidToDemandIdMapping[Fid];
                        if (DemandIdToTitleDic.ContainsKey(demandId) && !Constants.MigrationFidList.Contains(Fid))
                        {
                            CpsFidTitleDic.Add(Fid, DemandIdToTitleDic[demandId]);
                        }
                    }
                    return CpsFidTitleDic;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
            }
            return null;
        }

        public Dictionary<string, string> GetFidToDemandIdMapping(List<string> fidList)
        {
            if (fidList == null)
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.DeraAzureDBConnectionString))
            {
                try
                {
                    Dictionary<string, string> FidToDemandId = new Dictionary<string, string>();

                    var command = new SqlCommand(GetDemandIdQuery(fidList), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var TicketStoreModel = new DataTable();
                    objSqlDataAdapter.Fill(TicketStoreModel);

                    if (TicketStoreModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in TicketStoreModel.Rows)
                        {
                            if (!row.IsNull("fulfillmentId") && !row.IsNull("demandId"))
                            {
                                if (!FidToDemandId.ContainsKey(row["fulfillmentId"].ToString()))
                                {
                                    FidToDemandId.Add(row["fulfillmentId"].ToString(), row["demandId"].ToString());
                                }
                            }
                        }
                    }

                    return FidToDemandId;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        public List<string> GetPoNumbersByDeliveryNumber(string deliveryNumber)
        {
            if (deliveryNumber == null)
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.DeraAzureDBConnectionString))
            {
                try
                {
                    var command = new SqlCommand(GetPoNumberQuery(deliveryNumber), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var TicketStoreModel = new DataTable();
                    objSqlDataAdapter.Fill(TicketStoreModel);

                    List<string> PoNumberList = new List<string>();
                    if (TicketStoreModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in TicketStoreModel.Rows)
                        {
                            PoNumberList.Add(row["POID"].ToString());
                        }
                    }
                    return PoNumberList;
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        public Dictionary<string, TicketStoreV2> GetTicketStore(List<string> ticketIDs)
        {
            if (ticketIDs == null || !ticketIDs.Any())
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.GDCOAnalyticDBConnectionString))
            {
                try
                {
                    var command = new SqlCommand(GetTicketStoreQuery(string.Join(",", ticketIDs)), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var TicketStoreModel = new DataTable();
                    objSqlDataAdapter.Fill(TicketStoreModel);

                    var ticketStoreData = new List<TicketStoreV2>();

                    if (TicketStoreModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in TicketStoreModel.Rows)
                        {
                            ticketStoreData.Add(new TicketStoreV2()
                            {
                                TicketId = row["TicketId"].ToString(),
                                CurrentState = row.IsNull("CurrentState") ? null : row["CurrentState"].ToString().Trim(),
                                CurrentSeverity = row.IsNull("CurrentSeverity") ? null : row["CurrentSeverity"].ToString().Trim(),
                                GDCOFaultCode = row.IsNull("GDCOFaultCode") ? null : row["GDCOFaultCode"].ToString().Trim(),
                                GDCOFaultDescription = row.IsNull("GDCOFaultDescription") ? null : row["GDCOFaultDescription"].ToString().Trim(),
                                MDMId = row.IsNull("MDMId") ? null : row["MDMId"].ToString().Trim(),
                                workflowjobid = row.IsNull("workflowjobid") ? null : row["workflowjobid"].ToString().Trim(),
                                DeliveryNumber = row.IsNull("DeliveryNumber") ? null : row["DeliveryNumber"].ToString().Trim(),
                                DatacenterCode = row.IsNull("DatacenterCode") ? null : row["DatacenterCode"].ToString().Trim(),
                                TemplateType = row.IsNull("TemplateType") ? null : row["TemplateType"].ToString().Trim(),
                                CreatedDate = row.IsNull("CreatedDate") ? (DateTime?)null : Convert.ToDateTime(row["CreatedDate"]),
                                AssignedDate = row.IsNull("AssignedDate") ? (DateTime?)null : Convert.ToDateTime(row["AssignedDate"]),
                                ResolvedDate = row.IsNull("ResolvedDate") ? (DateTime?)null : Convert.ToDateTime(row["ResolvedDate"]),
                                DueDate = row.IsNull("DueDate") ? (DateTime?)null : Convert.ToDateTime(row["DueDate"]),
                                WasSLABreached = row.IsNull("WasSLABreached") ? null : row["WasSLABreached"].ToString().Trim()
                            });
                        }
                    }

                    if (ticketStoreData.Any())
                    {
                        var ticketData = new Dictionary<string, TicketStoreV2>();
                        foreach (var ticket in ticketStoreData)
                        {
                            ticketData[ticket.TicketId] = ticket;
                        }
                        return ticketData;
                    }
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        public Dictionary<string, TicketStoreV2> GetTicketStoreByDeliveryIDs(List<string> deliveryIDs)
        {
            if (deliveryIDs == null || !deliveryIDs.Any())
            {
                return null;
            }

            using (var connection = new SqlConnection(ConnectionHandler.GDCOAnalyticDBConnectionString))
            {
                try
                {
                    var deliveryIDsStr = string.Empty;

                    foreach (string deliveryID in deliveryIDs)
                    {
                        deliveryIDsStr = deliveryIDsStr + "'" + deliveryID + "'" + ",";
                    }
                    deliveryIDsStr = deliveryIDsStr.Substring(0, deliveryIDsStr.Length - 1);

                    var command = new SqlCommand(GetTicketStoreByDeliveryIDsQuery(deliveryIDsStr), connection)
                    {
                        CommandType = CommandType.Text
                    };
                    connection.Open();
                    var objSqlDataAdapter = new SqlDataAdapter(command);
                    var TicketStoreModel = new DataTable();
                    objSqlDataAdapter.Fill(TicketStoreModel);

                    var ticketStoreData = new List<TicketStoreV2>();

                    if (TicketStoreModel.Rows.Count > 0)
                    {
                        foreach (DataRow row in TicketStoreModel.Rows)
                        {
                            ticketStoreData.Add(new TicketStoreV2()
                            {
                                TicketId = row["TicketId"].ToString(),
                                CurrentState = row.IsNull("CurrentState") ? null : row["CurrentState"].ToString().Trim(),
                                CurrentSeverity = row.IsNull("CurrentSeverity") ? null : row["CurrentSeverity"].ToString().Trim(),
                                GDCOFaultCode = row.IsNull("GDCOFaultCode") ? null : row["GDCOFaultCode"].ToString().Trim(),
                                GDCOFaultDescription = row.IsNull("GDCOFaultDescription") ? null : row["GDCOFaultDescription"].ToString().Trim(),
                                MDMId = row.IsNull("MDMId") ? null : row["MDMId"].ToString().Trim(),
                                workflowjobid = row.IsNull("workflowjobid") ? null : row["workflowjobid"].ToString().Trim(),
                                DeliveryNumber = row.IsNull("DeliveryNumber") ? null : row["DeliveryNumber"].ToString().Trim(),
                                DatacenterCode = row.IsNull("DatacenterCode") ? null : row["DatacenterCode"].ToString().Trim(),
                                TemplateType = row.IsNull("TemplateType") ? null : row["TemplateType"].ToString().Trim(),
                                CreatedDate = row.IsNull("CreatedDate") ? (DateTime?)null : Convert.ToDateTime(row["CreatedDate"]),
                                AssignedDate = row.IsNull("AssignedDate") ? (DateTime?)null : Convert.ToDateTime(row["AssignedDate"]),
                                ResolvedDate = row.IsNull("ResolvedDate") ? (DateTime?)null : Convert.ToDateTime(row["ResolvedDate"]),
                                DueDate = row.IsNull("DueDate") ? (DateTime?)null : Convert.ToDateTime(row["DueDate"]),
                                WasSLABreached = row.IsNull("WasSLABreached") ? null : row["WasSLABreached"].ToString().Trim()
                            });
                        }
                    }

                    if (ticketStoreData.Any())
                    {
                        var ticketData = new Dictionary<string, TicketStoreV2>();
                        foreach (var ticket in ticketStoreData)
                        {
                            if (!string.IsNullOrEmpty(ticket.DeliveryNumber))
                            {
                                ticketData[ticket.DeliveryNumber] = ticket;
                            }
                        }
                        return ticketData;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    connection.Close();
                }
                return null;
            }
        }

        private string GetSeedFileMetaData(List<string> projectIds)
        {
            string fidCombinationString = "'";
            int i = 0;
            for (; i < projectIds.Count - 1; i++)
            {
                fidCombinationString += projectIds[i] + "','";
            }
            fidCombinationString += projectIds[i] + "'";

            return
                $@"SELECT [FulfillmentId]
              ,[VersionSequence]
              ,[RawVersion]
              ,[CreatedDate]
              ,[LastModifiedDate]
              ,[DeploymentId]
              ,[PhysicalDataCenterCode]
              ,[SeedType]
              ,[Status]
              ,[isreworkedFile]
              ,[isSKUChanged]
              ,[isTileChanged]
              ,[isColoChanged]
              ,[isClusterNameChanged]
              ,[isClusterTemplateChanged]
              ,[isSecondarySKUChanged]
              ,[isPropertyChanged]
              ,[isMaxPublishedRevision]
              ,[isIntentChanged]
              ,[isHostNameChanged]
              ,[isAvailabilityZoneChanged]
              ,[isDCChanged]
              ,[isRegionChanged]
              ,[ismorchanged]
              ,[ist1skuchanged]
              ,[isFabricControllerNodeCountChanged]
              ,[isUpLinkSpeedChanged]
              ,[isC0SKUChanged] 
            FROM [reportview].[vwSeedFileVersion] 
            where [isMaxPublishedRevision] = 1 and fulfillmentId in ({fidCombinationString})";
        }

        private string GetRecentProjectsQuery()
        {
            return $@"SELECT 
                       [MDMID]
                      , p.[FulfillmentID]
                    FROM [l3view].[vwProject] p, [reportview].[vwSeedFileVersion] s
                    where p.fulfillmentId = s.fulfillmentId and s.versionsequence > 2 and s.[isMaxPublishedRevision] = 1 and p.creationdate>= DATEADD(month,-1,GETDATE())";
        }

        private string GetTicketStoreQuery(string ticketIDs)
        {
            return $@"SELECT [TicketId]
                  ,[CurrentState]
                  ,[CurrentSeverity]
	              ,[Code] AS GDCOFaultCode
                  ,[FaultDescription] AS GDCOFaultDescription
                  ,[MDMId]
	              ,[workflowjobid]
	              ,CASE TemplateType WHEN 'GFSDeployment' then [WorkflowJobId] WHEN 'GFSDeploymentIncident' then [WorkflowJobId] ELSE [MDMId] END as DeliveryNumber
                  ,[DatacenterCode]
	              ,[TemplateType]
                  ,[CreatedDate]
                  ,[AssignedDate]
                  ,[ResolvedDate]
                  ,[DueDate]
                  ,[WasSLABreached]
              FROM [dbo].[TicketStore](nolock)
              WHERE TicketId in ({ticketIDs})";
        }

        private string GetPoNumberQuery (string deliveryNumber)
        {
            return $@"SELECT [POID]
              FROM [reportview].[vwSignaltoLiveTracking](nolock)
              WHERE deliverynumber={deliveryNumber}";
        }

        private string GetDemandIdQuery (List<string> FidList)
        {
            string fidCombinationString = "'";
            int i = 0;
            for (; i < FidList.Count - 1; i++)
            {
                fidCombinationString += FidList[i] + "','";
            }
            fidCombinationString += FidList[i] + "'";

            return $@"select fulfillmentId, demandId
                from [l3view].[vwProject]
                where fulfillmentId in ({fidCombinationString})";
        }

        private string GetMsPODTitleQuery (List<string> DemandIdList)
        {
            string DidCombinationString = "'";
            int i = 0;
            for (; i < DemandIdList.Count - 1; i++)
            {
                DidCombinationString += DemandIdList[i] + "','";
            }
            DidCombinationString += DemandIdList[i] + "'";

            return $@"select Id, title
                from [reportview].[vwMSPODDemand]
                where Id in ({DidCombinationString}) and (title like 'CDDS%' or title like 'CPS%' or title like 'IS %' or title like 'DPS%' or title like 'ADNS%')";
        }

        private string GetTicketStoreByDeliveryIDsQuery(string deliveryIDs)
        {
            return $@"SELECT [TicketId]
                ,[CurrentState]
                ,[CurrentSeverity]
	            ,[Code] AS GDCOFaultCode
                ,[FaultDescription] AS GDCOFaultDescription
                ,[MDMId]
	            ,[workflowjobid]
	            ,CASE TemplateType WHEN 'GFSDeployment' then [WorkflowJobId] WHEN 'GFSDeploymentIncident' then [WorkflowJobId] ELSE [MDMId] END as DeliveryNumber
                ,[DatacenterCode]
	            ,[TemplateType]
                ,[CreatedDate]
                ,[AssignedDate]
                ,[ResolvedDate]
                ,[DueDate]
                ,[WasSLABreached]
            FROM [dbo].[TicketStore](nolock) 
            WHERE (Code = 114386 and [WorkflowJobId] in ({deliveryIDs})) or 
	            (Code = 124110 and [MDMId] in ({deliveryIDs}))";
        }
    }
}
