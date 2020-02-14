using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using WorkOrder;
using WorkOrder.Model;

namespace DataSync
{
    public class CisDataSync
    {
        public void SyncCisDataSet()
        {
            List<CisProjectInfo> cisDataSet = null;
            var errorSubject = "Error in syncing Cis Data";

            try
            {
                cisDataSet = new CisHandler().GetCisProjects();
            }
            catch(Exception ex)
            {
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.opmServiceAccount, Constants.automationTeam, errorSubject, ex.ToString());
            }

            if (cisDataSet == null || !cisDataSet.Any())
            {
                return;
            }

            var take = 100;
            var skip = 0;

            while (skip < cisDataSet.Count)
            {
                var currentCisDataSet = cisDataSet.Skip(skip).Take(take);
                using (var connection = new SqlConnection(ConnectionHandler.ConnectionString))
                {
                    try
                    {
                        var command = new SqlCommand("mcio_oa_db.prc_UpdateCisDataSet", connection)
                        {
                            CommandType = CommandType.StoredProcedure
                        };

                        var cisDataTable = new DataTable("mcio_oa_db.CisDataSetTableType");

                        cisDataTable.Columns.Add("JobId", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("DisplayName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("Workflow", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("JobCreatedDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("JobStartDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("JobEndDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("JobStateName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("ClusterName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("DatacenterCode", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("MDMID", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("PropertyGroupName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskId", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskDisplayName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("Component", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("OrderId", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskCreatedDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskStartDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskEndDate", typeof(DateTime)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TaskStateName", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("AssignedTo", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TicketId", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("GDCOFaultCode", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("GDCOFaultDescription", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("TemplateType", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("DeliveryNumber", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("GDCODCCode", typeof(string)).AllowDBNull = true;
                        cisDataTable.Columns.Add("ResourceType", typeof(string)).AllowDBNull = true;

                        foreach (var cisData in currentCisDataSet)
                        {
                            var jobId = cisData.JobId;
                            var displayName = cisData.DisplayName;
                            var workflow = cisData.Workflow;
                            var jobCreatedDate = cisData.CreatedDate;
                            var jobStartDate = cisData.StartDate;
                            var jobEndDate = cisData.FinishDate;
                            var jobStateName = cisData.StateName;
                            var clusterName = cisData.ClusterName;
                            var datacenterCode = cisData.DatacenterCode;
                            var MDMID = cisData.MdmId;
                            var propertyGroupName = cisData.PropertyGroupName;
                            var resourceType = cisData.ResourceType;

                            if (cisData.Tasks != null && cisData.Tasks.Any())
                            {
                                foreach (var cisTask in cisData.Tasks)
                                {
                                    var taskId = cisTask.TaskId;
                                    var taskName = cisTask.TaskName;
                                    var taskDisplayName = cisTask.DisplayName;
                                    var component = cisTask.Component;
                                    var orderId = cisTask.OrderId;
                                    var taskCreatedDate = cisTask.CreatedDate;
                                    var taskStartDate = cisTask.StartDate;
                                    var taskEndDate = cisTask.FinishDate;
                                    var taskStateName = cisTask.StateName;
                                    var assignedTo = cisTask.AssignedTo;
                                    string ticketId = null;
                                    string GDCOFaultCode = null;
                                    string GDCOFaultDescription = null;
                                    string TemplateType = null;
                                    string DeliveryNumber = null;
                                    string GDCODCCode = null;
                                    if (cisTask.GDCOTicket != null)
                                    {
                                        ticketId = cisTask.GDCOTicket.TicketId;
                                        GDCOFaultCode = cisTask.GDCOTicket.GDCOFaultCode;
                                        GDCOFaultDescription = cisTask.GDCOTicket.GDCOFaultDescription;
                                        TemplateType = cisTask.GDCOTicket.TemplateType;
                                        DeliveryNumber = cisTask.GDCOTicket.DeliveryNumber;
                                        GDCODCCode = cisTask.GDCOTicket.DatacenterCode;
                                    }

                                    cisDataTable.Rows.Add(
                                        jobId,
                                        displayName,
                                        workflow,
                                        jobCreatedDate,
                                        jobStartDate,
                                        jobEndDate,
                                        jobStateName,
                                        clusterName,
                                        datacenterCode,
                                        MDMID,
                                        propertyGroupName,
                                        taskId,
                                        taskName,
                                        taskDisplayName,
                                        component,
                                        orderId,
                                        taskCreatedDate,
                                        taskStartDate,
                                        taskEndDate,
                                        taskStateName,
                                        assignedTo,
                                        ticketId,
                                        GDCOFaultCode,
                                        GDCOFaultDescription,
                                        TemplateType,
                                        DeliveryNumber,
                                        GDCODCCode,
                                        resourceType
                                        );
                                }
                            }
                        }

                        command.Parameters.Add(new SqlParameter
                        {
                            ParameterName = "@CisDataSetTvp",
                            SqlDbType = SqlDbType.Structured,
                            Value = cisDataTable
                        });

                        connection.Open();
                        command.ExecuteScalar();
                        connection.Close();
                    }
                    catch(Exception ex)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, errorSubject, $"Take = {take} Skip = {skip} {ex.ToString()}");
                    }
                }
                skip = skip + take;
            }
        }
    }
}
