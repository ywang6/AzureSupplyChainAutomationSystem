using AssetQc;
using AssetQc.Model;
using GdcoTicket;
using QcDriver.Model;
using Reporting;
using SkuDataHandler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utilities;
using Utilities.Model;
using GdcoTicket.Model;
using Newtonsoft.Json;

namespace QcDriver
{
    public class ExchangeQcDriver
    {
        private const string PropertyGroup = "Exchange";
        private string ExecutionMode;
        private static string currentExeName = System.AppDomain.CurrentDomain.FriendlyName;
        private bool emailTrigger = false;
        public static StringBuilder EmailBody = new StringBuilder();

        public ExchangeQcDriver()
        {
            EmailBody.AppendLine("<style> table {    font-family: arial, sans-serif;    border-collapse: collapse;    width: 100%;}td, th {    border: 1px solid #dddddd;    text-align: left;    padding: 8px;}tr:nth-child(even) {    background-color: #dddddd;} h3 {    font-family: arial, sans-serif; color:#2F4F4F;}</style>");
            EmailBody.AppendLine("<p><h3>Following projects have tickets in OA but not CIS:</h3></p>");
            EmailBody.AppendLine("<table>");
            EmailBody.AppendLine("<tr><td>MDMID</td><td>ErrorTitle</td><td>Description</td></tr>");
        }

        public AllEgsResult GetDSMsAssetSkuValidationResult(string fulfillmentID = null, string CurrentUser = null, bool Email = false, string EmailList = null)
        {
            var exoResult = new AllEgsResult();
            var exchangeOutput = new List<AllEgsOutputModel>();
            var error = new StringBuilder();
            var emailSubject = "Exchange MSAsset/SKU Validation Report for FulfillmentIDs from DS - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var errorEmailSubject = "Error: Exchange MSAsset/SKU Validation Report for FulfillmentIDs from DS - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            ExecutionMode = (fulfillmentID == null) ? "S" : "U";

            if (currentExeName != null)
            {
                emailTrigger = currentExeName.IndexOf("AllEgsMsAssetSkuValidationReporting", StringComparison.OrdinalIgnoreCase) >= 0 ? true : false;
            }

            Dictionary<string, List<DSDataSet>> dsDataSet = null;
            
            try
            {
                dsDataSet = new DSDataSetAccess().GetInProgressExchangeDeploymentsToRTEG(fulfillmentID);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            AllEgsQc exoQc = new AllEgsQc();

            // Get FulfillmentId to DemandId mapping
            GDCOTicketStoreHandler GDCOTicketStoreHandler = new GDCOTicketStoreHandler();
            Dictionary<string, string> FidToDemandIdMapping = GDCOTicketStoreHandler.GetFidToDemandIdMapping(dsDataSet.Keys.ToList());

            // Get CPS FidList, it contains all of the fids which are CPS
            Dictionary<string, string> CpsFidTitleDic = GDCOTicketStoreHandler.GetCpsFidTitleDic(FidToDemandIdMapping);

            // Iterate over each DS project
            foreach (var fID in dsDataSet.Keys)
            {
                var dsTasks = dsDataSet[fID];
                var GroupType = dsTasks.Last().GroupType;
                if (dsTasks != null && dsTasks.Any())
                {
                    var dcCode = dsTasks.Last().DeploymentDCCode;
                    
                    var exoOpModel = exoQc.PerformAllEgsSkuMsAssetValidation(fID, dsTasks.Last().GDCOFaultDescription, dsTasks.Last().DeploymentPGName, "D", null, null, dcCode, GroupType, CpsFidTitleDic, dsTasks.Last().MDMIdList, null, true);
                    exoOpModel.GroupType = GroupType;
                    exchangeOutput.Add(exoOpModel);
                    error.AppendLine(exoOpModel.Error.ToString());
                }
            }

            // Save the exchangeOutput to QcExecutionRawResult table and to the shared path
            List<string> resultFiles = null;
            try
            {
                resultFiles = SaveResultToFile(exchangeOutput, "D");
            }
            catch (Exception ex)
            {
                error.AppendLine("Exception in saving results to the csv file. " + ex.ToString());
            }

            try
            {
                var gdcoTickets = CreateGdcoTickets(exchangeOutput, "D");

                // Create blocking ICM ticket for EXO fid
                DSDataSetAccess dsa = new DSDataSetAccess();
                GdcoTicketHandler gdcoTicketHandler = new GdcoTicketHandler();

                foreach (var fID in dsDataSet.Keys)
                {
                    var dsTasks = dsDataSet[fID];
                    if (dsTasks != null && dsTasks.Any() && dsTasks.Last().GroupType.IndexOf("PreRack", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        var dcCode = dsTasks.Last().DeploymentDCCode;
                        var clusterName = dsTasks.Last().ClusterName;
                        var currentWorkOrder = dsTasks.Last().GDCOFaultDescription;
                        var errorDescription = "Please complete manual QC for this cluster " + clusterName + "< EOM >";
                        var errorCode = "Exo manual block ticket";
                        var errorTitle = "NetworkProvisioning - General Investigation for Azure Networking - MOR/PRD build (Non PNaaS)";

                        if (dsa.IsInOA(dsTasks))
                        {
                            var tickets = new List<GdcoTicket.Model.GdcoTableTicket>();
                            try
                            {
                                tickets = new GdcoTicketTableHandler().GetTickets(fID, "msassetsku");
                            }
                            catch (Exception ex)
                            {
                                return null;
                            }

                            bool blockTicketExist = false;
                            List<DSDataSet> oaTasks = new List<DSDataSet>();

                            if (dsDataSet.ContainsKey(fID))
                            {
                                oaTasks = dsDataSet[fID].Where(d => (d.GDCOFaultCode.Equals("124110"))).ToList();
                            }
                            foreach (var oaTask in oaTasks)
                            {
                                if (oaTask.TicketState.Equals("Created", StringComparison.OrdinalIgnoreCase) || oaTask.TicketState.Equals("InProgress", StringComparison.OrdinalIgnoreCase)
                                    || oaTask.TicketState.Equals("Blocked", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Loop through each ticket to see if manual exo blocking ticket exist under active OA task ticket
                                    foreach (var ticket in tickets)
                                    {
                                        if (ticket.GdcoTicket.IndexOf(errorTitle, StringComparison.OrdinalIgnoreCase) >= 0
                                            && ticket.GdcoTicket.IndexOf(oaTask.TicketId, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            blockTicketExist = true;
                                            break;
                                        }
                                    }
                                }
                                if (blockTicketExist) break;
                            }

                            if (!blockTicketExist)
                            {
                                try
                                {
                                    var ExoBlockingTicket = new GdcoTicketHandler().CreateTicket(fID, currentWorkOrder,
                                            errorCode, errorTitle, errorDescription, dcCode, "oadpm", "3", "", "D", "msassetsku");
                                    gdcoTickets.Add(ExoBlockingTicket);

                                    // Assign blocking tickets' parents
                                    DSDataSet oaTask = null;

                                    if (dsDataSet.ContainsKey(fID))
                                    {
                                        oaTask = dsDataSet[fID].Where(d => ((d.GDCOFaultCode.Equals("124110")) 
                                        && (d.TicketState.Equals("Created", StringComparison.OrdinalIgnoreCase) || d.TicketState.Equals("InProgress", StringComparison.OrdinalIgnoreCase)))).FirstOrDefault();
                                    }
                                    if (oaTask != null && oaTask.TicketId != null)
                                    {
                                        new GdcoTicketHandler().AssignParent(Convert.ToInt64(oaTask.TicketId), ExoBlockingTicket.Id);
                                    }
                                }
                                catch(Exception ex)
                                {
                                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, "EXO Blocking ticket failure", ex.ToString());
                                }
                            }
                        }
                    }
                }

                foreach (var gdcoTicket in gdcoTickets)
                {
                    if (!string.IsNullOrEmpty(gdcoTicket.Error))
                    {
                        error.AppendLine("Error in GDCO Ticket Creation " + gdcoTicket.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                error.AppendLine("Exception in creating GDCO Tickets. " + ex.ToString());
            }

            // Insert the record and send email
            try
            {
                exoResult.Result = exchangeOutput;
                var insertedResultId = QcExecutionRawResultHandler.InsertQcExecutionRawResult(PropertyGroup, ExecutionMode, JsonConvert.SerializeObject(exchangeOutput), DateTime.Now, resultFiles, CurrentUser);
                exoResult.Report = GetExoReport(insertedResultId, "D");
            }
            catch (Exception ex)
            {
                error.AppendLine("Error in inserting the execution result. " + ex.ToString());

            }
            exoResult.Error = error;

            // send diff email
            if (EmailBody.Length > 440)
            {
                string to = "wnyu@gmail.com";
                string subject = "OA CIS tickets comparision";
                EmailBody.AppendLine("</table>");
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, to, subject, EmailBody.ToString());
            }

            if (Email)
            {
                var body = new StringBuilder();
                var dbSummaryReport = new StringBuilder();
                string dbDetailReport = null;
                body.AppendLine("<style> table {    font-family: arial, sans-serif;    border-collapse: collapse;    width: 100%;}td, th {    border: 1px solid #dddddd;    text-align: left;    padding: 8px;}tr:nth-child(even) {    background-color: #dddddd;} h3 {    font-family: arial, sans-serif; color:#2F4F4F;}</style>");
                body.AppendLine("<p><h3>Summary Report:</h3></p>");
                body.AppendLine("<table>");
                body.AppendLine("<tr><td>MdmId</td><td>Current WO</td><td>Failed Tests</td><td>Ticket(s)/Status</td></tr>");
                foreach (var report in exoResult.Report)
                {
                    var tickets = new StringBuilder(string.Empty);
                    if (report.gdcoTickets != null && report.gdcoTickets.Any())
                    {
                        foreach (var gdcoTicket in report.gdcoTickets)
                        {
                            if (gdcoTicket.Fields.ContainsKey("GDCOTicketing.Custom.UpdateInfo") && gdcoTicket.Fields.ContainsKey("System.State"))
                            {
                                var ticketUrl = gdcoTicket.Fields["GDCOTicketing.Custom.UpdateInfo"].ToString();
                                var firstIndex = ticketUrl.IndexOf("<a");
                                var lastIndex = ticketUrl.IndexOf(">", firstIndex);
                                tickets.Append(ticketUrl.Substring(firstIndex, lastIndex - firstIndex + 1) + gdcoTicket.Id + "</a>" + "(" + gdcoTicket.Fields["System.State"] + ") ");
                            }
                        }
                    }
                    body.AppendLine("<tr><td>" + report.MdmId + "</td><td>" + report.WorkOrder + "</td><td>" + report.FailedTests + "</td><td>" + tickets.ToString() + "</td></tr>");
                    dbSummaryReport.Append(report.MdmId + "," + report.WorkOrder + "," + report.FailedTests + "," + tickets.ToString() + ";");
                }
                if (dbSummaryReport.Length > 0)
                {
                    dbSummaryReport.Remove(dbSummaryReport.Length - 1, 1);
                }
                body.AppendLine("</table>");
                if (exoResult.Report != null && exoResult.Report.Any())
                {
                    body.AppendLine("<p><h3>Detail Report: </h3>" + exoResult.Report.First().DetailReport + "</p>");
                    dbDetailReport = exoResult.Report.First().DetailReport;
                }
                // Save the Report to database
                try
                {
                    new ReportingHandler().SaveReport(dbSummaryReport.ToString(), dbDetailReport, "ds", "msassetsku");
                }
                catch (Exception ex)
                {
                    exoResult.Error.AppendLine(ex.ToString());
                }

                if (exoResult.Error != null && exoResult.Error.ToString().Trim().Length > 0)
                {
                    string[] errorLines = exoResult.Error.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                    body.AppendLine("<p><h3>Errors: </h3><ol>");
                    foreach (var erroLine in errorLines)
                    {
                        if (erroLine != null && !string.IsNullOrEmpty(erroLine.Trim()))
                        {
                            body.AppendLine("<li>" + erroLine + "</li>");
                        }
                    }
                    body.AppendLine("</ol></p>");
                }
                SendEmail.SendExoSkuMsAssetReportEmail(CurrentUser, EmailList, emailSubject, body.ToString());
            }

            // Send out email to OA when no pid/mdmid found in MSAsset and no PO found for these Fids
            if (emailTrigger && error.ToString().IndexOf("MSAsset returned no output", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Filter the error just contains MsAsset association issues.
                string[] sep = { Environment.NewLine, "\n" }; // "\n" added in case you manually appended a newline
                string[] errList = error.ToString().Split(sep, StringSplitOptions.None);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<p><h3>Below EXO fids don't have both MsAsset and PO Associations, please find investigate. </h3></p>");

                foreach (string line in errList)
                {
                    if (line.IndexOf("MSAsset returned no output", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine(line);
                    }
                }

                string subject = "Below EXO Fids don't have MsAsset and PO Associations";
                string to = Constants.msAssetSupportAccount + ";" + Constants.OADPMAccount;
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, to, subject, sb.ToString());
            }

            return exoResult;
        }

        private List<GdcoTicket.Model.GdcoTicket> CreateGdcoTickets(List<AllEgsOutputModel> results, string mode)
        {
            var gdcoTickets = new List<GdcoTicket.Model.GdcoTicket>();
            HashSet<String> failedDescriptions = new HashSet<String>();

            if (results == null || !results.Any())
            {
                return gdcoTickets;
            }

            // Check for failed tests, create tickets
            foreach(var result in results)
            {
                var errorTypeToExchangeResults = new Dictionary<string, List<AllEngineeringGroupsResult>>();
                var exchangeResults = result.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed") && !string.IsNullOrEmpty(eo.QcName));
                var mdmId = string.IsNullOrEmpty(result.MdmId) ? result.ProjectId : result.MdmId;

                KustoAccess ka = new KustoAccess();
                
                // compare the current ticket with CIS Kusto feed, if different, cut the ticket
                var kustoErrMsg = "";

                if (exchangeResults != null && exchangeResults.Any())
                {
                    kustoErrMsg = ka.GetErrorEsgByMdmid(mdmId);
                }

                foreach (var exchangeResult in exchangeResults)
                {
                    if(!errorTypeToExchangeResults.ContainsKey(exchangeResult.QcName))
                    {
                        errorTypeToExchangeResults[exchangeResult.QcName] = new List<AllEngineeringGroupsResult> { exchangeResult };
                    }
                    else
                    {
                        var exchangeResultList = errorTypeToExchangeResults[exchangeResult.QcName];
                        exchangeResultList.Add(exchangeResult);
                    }
                }

                if(!errorTypeToExchangeResults.Any())
                {
                    continue;
                }

                foreach(var key in errorTypeToExchangeResults.Keys)
                {
                    var exchangeErrorList = errorTypeToExchangeResults[key];
                    var projectId = string.IsNullOrEmpty(result.ProjectId) ? result.MdmId : result.ProjectId;
                    var currentWorkOrder = exchangeErrorList.First().WorkOrderName;
                    var errorCode = key;
                    string errorTitle = null;
                    var errorDescription = GetExoResultError(exchangeErrorList);
                    var dcCode = result.DataCenterCode;
                    var requestOwner = "oadpm";
                    string severity = null;
                    string parentTicketId = null;

                    // KustoErrMsg is empty means we couldn't found ticket in Kusto feed, we should email
                    if (String.IsNullOrEmpty(kustoErrMsg))
                    {
                        // send email if there is something diff between us and their Kusto feed
                        EmailBody.AppendLine("<tr><td>" + projectId + "</td><td>" + errorTitle + "</td><td>" + errorDescription + "</td></tr>");
                    }

                    if (result.GroupType.IndexOf("PreRack", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    var gdcoTicket = new GdcoTicketHandler().CreateTicket(projectId, currentWorkOrder,
                        errorCode, errorTitle, errorDescription, dcCode, requestOwner, severity, parentTicketId, mode, "msassetsku");
                    gdcoTickets.Add(gdcoTicket);

                    try
                    {
                        if(mode.Equals("P"))
                        {
                            new GdcoTicketHandler().AssignParent(projectId, gdcoTicket.Id);
                        }
                        else if(mode.Equals("M"))
                        {
                            var cisProject = new CisAccess().GetCisProjectsFromCache().CisProjects.Where(c => c.MdmId.Equals(projectId)).FirstOrDefault();
                            if(cisProject != null && cisProject.UpdateAssetDataTicketId != null)
                            {
                                new GdcoTicketHandler().AssignParent(Convert.ToInt64(cisProject.UpdateAssetDataTicketId), gdcoTicket.Id);
                            }
                        }
                        else if (mode.Equals("D"))
                        {
                            var dsDataSet = new DSDataSetAccess().GetInProgressExchangeDeploymentsToRTEG();
                            DSDataSet updateAssetTask = null;
                            DSDataSet oaTask = null;
                            if (dsDataSet.ContainsKey(projectId))
                            {
                                updateAssetTask = dsDataSet[projectId].Where(d => (d.GDCOFaultCode.Equals("124107") || d.GDCOFaultCode.Equals("124246") || d.GDCOFaultCode.Equals("124054"))).FirstOrDefault();
                                oaTask = dsDataSet[projectId].Where(d => (d.GDCOFaultCode.Equals("124110"))).FirstOrDefault();
                            }
                            if (updateAssetTask != null && updateAssetTask.TicketState.Equals("Created") && updateAssetTask.TicketId != null)
                            {
                                new GdcoTicketHandler().AssignParent(Convert.ToInt64(updateAssetTask.TicketId), gdcoTicket.Id);
                            }
                            else if (oaTask != null && oaTask.TicketId != null)
                            {
                                new GdcoTicketHandler().AssignParent(Convert.ToInt64(oaTask.TicketId), gdcoTicket.Id);
                            }
                        }
                    }
                    catch(Exception)
                    {
                    }
                    errorDescription = errorDescription.Length > 1000 ? errorDescription.Substring(0, 1000) : errorDescription;
                    failedDescriptions.Add(errorDescription);
                }
            }

            // Check for passed cases that have failed earlier and ticket is not updated. Update the tickets.
            var pids = new List<string>();
            var failedPids = new List<string>();
            foreach (var result in results)
            {
                if (result.GroupType.IndexOf("PreRack", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                var failedExchangeResults = result.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed") && !string.IsNullOrEmpty(eo.QcName));
                var pid = string.IsNullOrEmpty(result.ProjectId) ? result.MdmId : result.ProjectId;
                if (!failedExchangeResults.Any())
                {
                    pids.Add(pid);
                }
                else
                {
                    failedPids.Add(pid);
                }
            }
            if (pids.Any())
            {
                foreach(var pid in pids)
                {
                    gdcoTickets.AddRange(new GdcoTicketHandler().UpdateTicket(pid, "msassetsku"));
                }
            }
            if(failedPids.Any())
            {
                foreach (var pid in failedPids)
                {
                    gdcoTickets.AddRange(new GdcoTicketHandler().UpdateFailedTicket(pid, "msassetsku", failedDescriptions));
                }
            }
            return gdcoTickets;
        }

        private string GetExoResultError(List<AllEngineeringGroupsResult> exchangeErrors)
        {
            var errorDescription = new StringBuilder();
            if (exchangeErrors != null)
            {
                foreach (var exchangeError in exchangeErrors)
                {
                    errorDescription.AppendLine("Error: " + exchangeError.Comments + Environment.NewLine + "RackName: " + exchangeError.RackName + Environment.NewLine + "DeviceName: " + exchangeError.DeviceName + Environment.NewLine + "DeviceType: " + exchangeError.DeviceType + Environment.NewLine + "MSAssetValue: " + exchangeError.MsAssetValue + Environment.NewLine + "ExpectedValue Range: " + exchangeError.SkuDocValue + Environment.NewLine);
                }
            }
            return errorDescription.ToString();
        }

        private List<AllEgsOutputReport> GetExoReport(int Id, string Mode = "P")
        {
            var exchangeOutputReportList = new List<AllEgsOutputReport>();
            var qcRawResult = QcExecutionRawResultHandler.GetQcExecutionRawResultById(Id);
            if(qcRawResult != null)
            {
                // Deserialize the Result
                var qcResults = JsonConvert.DeserializeObject<List<AllEgsOutputModel>>(qcRawResult.Result);

                // Fetch the GDCO tickets
                var projectIds = new List<string>();
                foreach(var qcResult in qcResults)
                {
                    if(!string.IsNullOrEmpty(qcResult.ProjectId))
                    {
                        projectIds.Add(qcResult.ProjectId);
                    }
                    else if(!string.IsNullOrEmpty(qcResult.MdmId))
                    {
                        projectIds.Add(qcResult.MdmId);
                    }
                }
                var gdcoTicketsDictionary = new GdcoTicketHandler().GetTickets(projectIds, "msassetsku");

                foreach(var qcResult in qcResults)
                {
                    var exchangeOutputReport = new AllEgsOutputReport();
                    exchangeOutputReport.ExecutionDate = qcRawResult.ExecutionDate;
                    exchangeOutputReport.DetailReport = qcRawResult.ReportFilePath;
                    if (qcResult != null && qcResult.AllEgsOutput != null)
                    {
                        if(Mode.Equals("P"))
                        {
                            exchangeOutputReport.ProjectId = qcResult.ProjectId;
                        }
                        else if(Mode.Equals("M") || Mode.Equals("D"))
                        {
                            exchangeOutputReport.MdmId = qcResult.MdmId;
                        }
                        exchangeOutputReport.TotalTests = qcResult.AllEgsOutput.Count;
                        exchangeOutputReport.PassedTests = qcResult.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Passed")).ToList().Count;
                        exchangeOutputReport.FailedTests = qcResult.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed")).ToList().Count;
                        exchangeOutputReport.WorkOrder = qcResult.WorkOrderName;
                        if((!string.IsNullOrEmpty(qcResult.ProjectId) && gdcoTicketsDictionary.ContainsKey(qcResult.ProjectId)) || (!string.IsNullOrEmpty(qcResult.MdmId) && gdcoTicketsDictionary.ContainsKey(qcResult.MdmId)))
                        {
                            var pid = string.IsNullOrEmpty(qcResult.ProjectId) ? qcResult.MdmId : qcResult.ProjectId;
                            exchangeOutputReport.gdcoTickets = gdcoTicketsDictionary[pid];
                        }
                    }
                    exchangeOutputReportList.Add(exchangeOutputReport);
                }
            }
            return exchangeOutputReportList;
        }

        private List<string> SaveResultToFile(List<AllEgsOutputModel> ExchangeOutput, string Mode = "P")
        {
            var basePath = "\\\\mcio-oa-pc5\\Reporting\\";
            var format = ".csv";
            var fileName = "SkuMsAsset_" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss-fff");
            var reportFilePath = File.Create(basePath + "Report_" + fileName + format);
            reportFilePath.Close();
            var reports = new List<string> { reportFilePath.Name };

            try
            {
                var report = new StringBuilder();
                
                if(Mode.Equals("P"))
                {
                    report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                "ProjectId",
                                "WorkOrderName",
                                "QcName",
                                "DeviceName",
                                "RackName",
                                "DeviceType",
                                "MsAssetValue",
                                "SkuDocValue",
                                "SkuDoc",
                                "TestStatus",
                                "Comments"));
                }
                else if(Mode.Equals("M") || Mode.Equals("D"))
                {
                    report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                "MdmId",
                                "WorkOrderName",
                                "QcName",
                                "DeviceName",
                                "RackName",
                                "DeviceType",
                                "MsAssetValue",
                                "SkuDocValue",
                                "SkuDoc",
                                "TestStatus",
                                "Comments"));
                }

                foreach(var exo in ExchangeOutput)
                {
                    foreach(var result in exo.AllEgsOutput)
                    {
                        if(Mode.Equals("P"))
                        {
                            report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                            result.ProjectId,
                            result.WorkOrderName,
                            result.QcName,
                            result.DeviceName,
                            result.RackName,
                            result.DeviceType,
                            result.MsAssetValue,
                            result.SkuDocValue,
                            result.SkuDoc,
                            result.TestStatus,
                            result.Comments));
                        }
                        else if(Mode.Equals("M") || Mode.Equals("D"))
                        {
                            report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                            result.MdmId,
                            result.WorkOrderName,
                            result.QcName,
                            result.DeviceName,
                            result.RackName,
                            result.DeviceType,
                            result.MsAssetValue,
                            result.SkuDocValue,
                            result.SkuDoc,
                            result.TestStatus,
                            result.Comments));
                        }
                    }
                }

                File.WriteAllText(reportFilePath.Name, report.ToString());

                reportFilePath.Close();
            }
            catch(Exception ex)
            {
                reportFilePath.Close();
                throw new Exception("Exception in SaveResultToFile method", ex);
            }

            return reports;
        }
    }
}
