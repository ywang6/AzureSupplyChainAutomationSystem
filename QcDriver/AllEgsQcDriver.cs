using AssetQc;
using AssetQc.Model;
using GdcoTicket;
using QcDriver.Model;
using Reporting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utilities;
using Utilities.Model;

namespace QcDriver
{
    public class AllEgsQcDriver
    {
        private string ExecutionMode;
        private CisProjectModel cisProjectModel = null;
        private static string currentExeName = System.AppDomain.CurrentDomain.FriendlyName;
        private bool emailTrigger = false;
        public static StringBuilder EmailBody = new StringBuilder();

        public AllEgsQcDriver()
        {
            EmailBody.AppendLine("<style> table {    font-family: arial, sans-serif;    border-collapse: collapse;    width: 100%;}td, th {    border: 1px solid #dddddd;    text-align: left;    padding: 8px;}tr:nth-child(even) {    background-color: #dddddd;} h3 {    font-family: arial, sans-serif; color:#2F4F4F;}</style>");
            EmailBody.AppendLine("<p><h3>Following projects have tickets in OA but not CIS:</h3></p>");
            EmailBody.AppendLine("<table>");
            EmailBody.AppendLine("<tr><td>MDMID</td><td>ErrorTitle</td><td>Description</td></tr>");
        }

        public AllEgsResult GetMsAssetSkuValidationMdmIdsResult(string MdmId = null, string CurrentUser = null, bool Email = false, string EmailList = null)
        {
            var allEgsResult = new AllEgsResult();
            var allEgsOutput = new List<AllEgsOutputModel>();
            var error = new StringBuilder();
            var emailSubject = "All EGs MSAsset/SKU Validation Report for MdmIds from Cis - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var errorEmailSubject = "Error: All EGs MSAsset/SKU Validation Report for MdmIds from Cis - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            ExecutionMode = (MdmId == null) ? "S" : "U";

            if (currentExeName != null)
            {
                emailTrigger = currentExeName.IndexOf("AllEgsMsAssetSkuValidationReporting", StringComparison.OrdinalIgnoreCase) >= 0 ? true : false;
            }

            var cisAccess = new CisAccess();
            Dictionary<string, CisProject> cisProjectDictionary = new Dictionary<string, CisProject>();

            // Fetch the incoming MdmIds
            if (string.IsNullOrEmpty(MdmId))
            {
                try
                {
                    cisProjectModel = cisAccess.GetCisProjectsFromCis(true);
                }
                catch (Exception ex)
                {
                    error.AppendLine(ex.ToString());
                    allEgsResult.Error = error;
                    if (Email)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(CurrentUser, Constants.automationTeam, errorEmailSubject, "Error in fetching Projects from Cis. " + error.ToString());
                    }
                    return allEgsResult;
                }

                var incomingCisProjects = cisProjectModel.CisProjects.Where(c => c.UpdateAssetWorkOrderStatus.IndexOf("Finished", StringComparison.OrdinalIgnoreCase) >= 0);
                if (cisProjectModel == null || !incomingCisProjects.Any())
                {
                    error.AppendLine("No Exchange Project Found in Cis coming to OA queue.");
                    allEgsResult.Error = error;
                    if (Email)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(CurrentUser, Constants.automationTeam, emailSubject, error.ToString());
                    }
                    return allEgsResult;
                }

                foreach (var cisProj in incomingCisProjects)
                {
                    cisProjectDictionary[cisProj.MdmId] = cisProj;
                }
            }
            else
            {
                cisProjectDictionary[MdmId] = new CisProject
                {
                    MdmId = MdmId,
                    CurrentWorkOrder = string.Empty,
                    UpdateAssetWorkOrderStatus = string.Empty
                };
            }

            AllEgsQc allEgsQc = new AllEgsQc();

            // Iterate over each Cis project
            foreach (var mdmId in cisProjectDictionary.Keys)
            {
                var cisProjectDetails = cisProjectDictionary[mdmId];
                if (cisProjectDetails != null)
                {
                    // Perform the QC
                    var allEgsOpModel = allEgsQc.PerformAllEgsSkuMsAssetValidation(mdmId, cisProjectDetails.CurrentWorkOrder, cisProjectDetails.PropertyGroupName, "M", null, null, cisProjectDetails.DatacenterCode);
                    if (allEgsOpModel != null)
                    {
                        allEgsOutput.Add(allEgsOpModel);
                        error.AppendLine(allEgsOpModel.Error.ToString());
                    }
                }
            }

            // Save the exchangeOutput to QcExecutionRawResult table and to the shared path
            List<string> resultFiles = null;
            try
            {
                resultFiles = SaveResultToFile(allEgsOutput, "M");
            }
            catch (Exception ex)
            {
                error.AppendLine("Exception in saving results to the csv file. " + ex.ToString());
            }

            try
            {
                var gdcoTickets = CreateGdcoTickets(allEgsOutput, "M");
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
                allEgsResult.Result = allEgsOutput;
                allEgsResult.Report = GetExoReport(allEgsOutput, "M");
            }
            catch (Exception ex)
            {
                error.AppendLine("Error in inserting the execution result. " + ex.ToString());
            }

            // Filter out duplicate errors
            HashSet<String> set = new HashSet<String>();
            string[] delim = { Environment.NewLine, "\n" };
            string[] lines = error.ToString().Split(delim, StringSplitOptions.None);
            foreach (string line in lines) { set.Add(line); }
            error = new StringBuilder();
            foreach (string line in set) { error.AppendLine(line); }

            allEgsResult.Error = error;

            if (Email)
            {
                var body = new StringBuilder();
                var dbSummaryReport = new StringBuilder();
                string dbDetailReport = null;
                body.AppendLine("<style> table {    font-family: arial, sans-serif;    border-collapse: collapse;    width: 100%;}td, th {    border: 1px solid #dddddd;    text-align: left;    padding: 8px;}tr:nth-child(even) {    background-color: #dddddd;} h3 {    font-family: arial, sans-serif; color:#2F4F4F;}</style>");
                body.AppendLine("<p><h3>Summary Report:</h3></p>");
                body.AppendLine("<table>");
                body.AppendLine("<tr><td>PropertyGroup</td><td>MdmId</td><td>Current WO</td><td>Failed Tests</td><td>Ticket(s)/Status</td></tr>");
                foreach (var report in allEgsResult.Report)
                {
                    var tickets = new StringBuilder(string.Empty);
                    if (report.gdcoTickets != null && report.gdcoTickets.Any())
                    {
                        foreach (var gdcoTicket in report.gdcoTickets)
                        {
                            if (gdcoTicket.Fields.ContainsKey("GDCOTicketing.Custom.UpdateInfo") && gdcoTicket.Fields.ContainsKey("System.State") && gdcoTicket.Fields.ContainsKey("GDCOTicketing.Custom.SLARemaining"))
                            {
                                var ticketUrl = gdcoTicket.Fields["GDCOTicketing.Custom.UpdateInfo"].ToString();
                                var firstIndex = ticketUrl.IndexOf("<a");
                                var lastIndex = ticketUrl.IndexOf(">", firstIndex);
                                var assignedTo = "None";
                                if (gdcoTicket.Fields.ContainsKey("System.AssignedTo"))
                                {
                                    assignedTo = gdcoTicket.Fields["System.AssignedTo"].ToString();
                                }
                                var state = (string)gdcoTicket.Fields["System.State"];
                                if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    tickets.Append(ticketUrl.Substring(firstIndex, lastIndex - firstIndex + 1) + gdcoTicket.Id + "</a>" + "(" + state + ") ");
                                }
                                else
                                {
                                    tickets.Append(ticketUrl.Substring(firstIndex, lastIndex - firstIndex + 1) + gdcoTicket.Id + "</a>" + "(" + state + "|RemainingSLA: " + Math.Round(TimeSpan.FromMilliseconds(Convert.ToDouble(gdcoTicket.Fields["GDCOTicketing.Custom.SLARemaining"])).TotalHours, 1) + "H|AssignedTo:" + assignedTo + ") ");
                                }
                            }
                        }
                    }
                    body.AppendLine("<tr><td>" + report.PropertyGroupName + "</td><td>" + report.MdmId + "</td><td>" + report.WorkOrder + "</td><td>" + report.FailedTests + "</td><td>" + tickets.ToString() + "</td></tr>");
                    dbSummaryReport.Append(report.PropertyGroupName + "," + report.MdmId + "," + report.WorkOrder + "," + report.FailedTests + "," + tickets.ToString() + ";");
                }
                if (dbSummaryReport.Length > 0)
                {
                    dbSummaryReport.Remove(dbSummaryReport.Length - 1, 1);
                }
                body.AppendLine("</table>");
                if (resultFiles != null && resultFiles.Any())
                {
                    body.AppendLine("<p><h3>Detail Report: </h3>" + resultFiles.First() + "</p>");
                    dbDetailReport = resultFiles.First();
                }

                // Save the Report to database
                if (MdmId == null)
                {
                    try
                    {
                        new ReportingHandler().SaveReport(dbSummaryReport.ToString(), dbDetailReport, "cis", "allegs_msassetsku");
                    }
                    catch (Exception ex)
                    {
                        allEgsResult.Error.AppendLine(ex.ToString());
                    }
                }

                if (allEgsResult.Error != null && allEgsResult.Error.ToString().Trim().Length > 0)
                {
                    string[] errorLines = allEgsResult.Error.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
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
            return allEgsResult;
        }

        public AllEgsResult GetDSMsAssetSkuValidationResult(string FulfillmentID = null, string CurrentUser = null, bool Email = false, string EmailList = null)
        {
            var allEgsResult = new AllEgsResult();
            var allEgsOutput = new List<AllEgsOutputModel>();
            var error = new StringBuilder();
            var emailSubject = "All EGs MSAsset/SKU Validation Report for FulfillmentsIDs from DS - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var errorEmailSubject = "Error: All EGs MSAsset/SKU Validation Report for FulfillmentsIDs from DS - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            ExecutionMode = (FulfillmentID == null) ? "S" : "U";

            if (currentExeName != null)
            {
                emailTrigger = currentExeName.IndexOf("AllEgsMsAssetSkuValidationReporting", StringComparison.OrdinalIgnoreCase) >= 0 ? true : false;
            }

            var dsDataSetAccess = new DSDataSetAccess();
            Dictionary<string, List<DSDataSet>> dsDataSet = null;

            try
            {
                dsDataSet = new DSDataSetAccess().GetInProgressAllEGsDeploymentsToRTEG(FulfillmentID);
                if (dsDataSet == null || !dsDataSet.Any())
                {
                    throw new Exception("dsDataSet returned null or empty for the Fullfillment ID: " + FulfillmentID + ". Please run this FID through debugging in GetInProgressAllEGsDeploymentsToRTEG to understand the root issue.");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            AllEgsQc allEgsQc = new AllEgsQc();

            // Get FulfillmentId to DemandId mapping
            GDCOTicketStoreHandler GDCOTicketStoreHandler = new GDCOTicketStoreHandler();
            Dictionary<string, string> FidToDemandIdMapping = GDCOTicketStoreHandler.GetFidToDemandIdMapping(dsDataSet.Keys.ToList());

            // Get CPS FidList, it contains all of the fids which are CPS
            Dictionary<string, string> CpsFidTitleDic = GDCOTicketStoreHandler.GetCpsFidTitleDic(FidToDemandIdMapping);

            // Iterate over each DS project
            foreach (var fID in dsDataSet.Keys)
            {
                var dsTasks = dsDataSet[fID];
                if (dsTasks != null && dsTasks.Any())
                {
                    var dcCode = dsTasks.Last().DeploymentDCCode;
                    var GroupType = dsTasks.Last().GroupType;
                    // Perform the QC
                    var allEgsOpModel = allEgsQc.PerformAllEgsSkuMsAssetValidation(fID, dsTasks.Last().GDCOFaultDescription, dsTasks.Last().DeploymentPGName, "D", null, null, dcCode, GroupType, CpsFidTitleDic, dsTasks.Last().MDMIdList, dsTasks.Last().PurchaseOrderNumber);
                    allEgsOpModel.GroupType = GroupType;
                    if (allEgsOpModel != null)
                    {
                        allEgsOutput.Add(allEgsOpModel);
                        error.AppendLine(allEgsOpModel.Error.ToString());
                    }
                }
            }

            // Save the exchangeOutput to QcExecutionRawResult table and to the shared path
            List<string> resultFiles = null;
            try
            {
                resultFiles = SaveResultToFile(allEgsOutput, "D");
            }
            catch (Exception ex)
            {
                error.AppendLine("Exception in saving results to the csv file. " + ex.ToString());
            }

            try
            {
                var gdcoTickets = CreateGdcoTickets(allEgsOutput, "D");
                
                // Create blocking ticket for CPS fid
                DSDataSetAccess dsa = new DSDataSetAccess();
                GdcoTicketHandler gdcoTicketHandler = new GdcoTicketHandler();

                foreach (var fID in CpsFidTitleDic.Keys.ToList())
                {
                    var dsTasks = dsDataSet[fID];
                    var title = CpsFidTitleDic[fID];
                    if (dsTasks != null && dsTasks.Any())
                    {
                        var dcCode = dsTasks.Last().DeploymentDCCode;
                        var currentWorkOrder = dsTasks.Last().GDCOFaultDescription;
                        var errorDescription = "Please complete manual QC: " + title + "< EOM >";
                        var errorCode = "CPS manual block ticket";
                        var errorTitle = "OperationalAcceptance - Engineering tool dependency";

                        if (dsa.IsInOA(dsTasks))
                        {
                            var tickets = new List<GdcoTicket.Model.GdcoTableTicket>();
                            try
                            {
                                tickets = new GdcoTicketTableHandler().GetTickets(fID, "allegs_msassetsku");
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
                                    // Loop through each ticket to see if manual CPS blocking ticket exist under active OA task ticket
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
                                    var CpsBlockingTicket = new GdcoTicketHandler().CreateTicket(fID, currentWorkOrder,
                                            errorCode, errorTitle, errorDescription, dcCode, "oadpm", "3", "", "D", "allegs_msassetsku");
                                    gdcoTickets.Add(CpsBlockingTicket);

                                    // Assign blocking tickets' parents
                                    DSDataSet oaTask = null;

                                    if (dsDataSet.ContainsKey(fID))
                                    {
                                        oaTask = dsDataSet[fID].Where(d => ((d.GDCOFaultCode.Equals("124110"))
                                        && (d.TicketState.Equals("Created", StringComparison.OrdinalIgnoreCase) || d.TicketState.Equals("InProgress", StringComparison.OrdinalIgnoreCase)))).FirstOrDefault();
                                    }
                                    if (oaTask != null && oaTask.TicketId != null)
                                    {
                                        new GdcoTicketHandler().AssignParent(Convert.ToInt64(oaTask.TicketId), CpsBlockingTicket.Id);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, "CPS Blocking ticket failure", ex.ToString());
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
                allEgsResult.Result = allEgsOutput;
                allEgsResult.Report = GetExoReport(allEgsOutput, "D");
            }
            catch (Exception ex)
            {
                error.AppendLine("Error in inserting the execution result. " + ex.ToString());
            }

            // Filter out duplicate errors
            HashSet<String> set = new HashSet<String>();
            string[] delim = { Environment.NewLine, "\n" };
            string[] lines = error.ToString().Split(delim, StringSplitOptions.None);
            foreach (string line in lines) { set.Add(line); }
            error = new StringBuilder();
            foreach (string line in set) { error.AppendLine(line); }

            allEgsResult.Error = error;

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
                body.AppendLine("<tr><td>PropertyGroup</td><td>MdmId</td><td>Current WO</td><td>Failed Tests</td><td>Ticket(s)/Status</td></tr>");
                foreach (var report in allEgsResult.Report)
                {
                    var tickets = new StringBuilder(string.Empty);
                    if (report.gdcoTickets != null && report.gdcoTickets.Any())
                    {
                        foreach (var gdcoTicket in report.gdcoTickets)
                        {
                            if (gdcoTicket.Fields.ContainsKey("GDCOTicketing.Custom.UpdateInfo") && gdcoTicket.Fields.ContainsKey("System.State") && gdcoTicket.Fields.ContainsKey("GDCOTicketing.Custom.SLARemaining"))
                            {
                                var ticketUrl = gdcoTicket.Fields["GDCOTicketing.Custom.UpdateInfo"].ToString();
                                var firstIndex = ticketUrl.IndexOf("<a");
                                var lastIndex = ticketUrl.IndexOf(">", firstIndex);
                                var assignedTo = "None";
                                if (gdcoTicket.Fields.ContainsKey("System.AssignedTo"))
                                {
                                    assignedTo = gdcoTicket.Fields["System.AssignedTo"].ToString();
                                }
                                var state = (string)gdcoTicket.Fields["System.State"];
                                if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    tickets.Append(ticketUrl.Substring(firstIndex, lastIndex - firstIndex + 1) + gdcoTicket.Id + "</a>" + "(" + state + ") ");
                                }
                                else
                                {
                                    tickets.Append(ticketUrl.Substring(firstIndex, lastIndex - firstIndex + 1) + gdcoTicket.Id + "</a>" + "(" + state + "|RemainingSLA: " + Math.Round(TimeSpan.FromMilliseconds(Convert.ToDouble(gdcoTicket.Fields["GDCOTicketing.Custom.SLARemaining"])).TotalHours, 1) + "H|AssignedTo:" + assignedTo + ") ");
                                }
                            }
                        }
                    }
                    body.AppendLine("<tr><td>" + report.PropertyGroupName + "</td><td>" + report.MdmId + "</td><td>" + report.WorkOrder + "</td><td>" + report.FailedTests + "</td><td>" + tickets.ToString() + "</td></tr>");
                    dbSummaryReport.Append(report.PropertyGroupName + "," + report.MdmId + "," + report.WorkOrder + "," + report.FailedTests + "," + tickets.ToString() + ";");
                }
                if (dbSummaryReport.Length > 0)
                {
                    dbSummaryReport.Remove(dbSummaryReport.Length - 1, 1);
                }
                body.AppendLine("</table>");
                if (resultFiles != null && resultFiles.Any())
                {
                    body.AppendLine("<p><h3>Detail Report: </h3>" + resultFiles.First() + "</p>");
                    dbDetailReport = resultFiles.First();
                }

                // Save the Report to database
                try
                {
                    new ReportingHandler().SaveReport(dbSummaryReport.ToString(), dbDetailReport, "ds", "allegs_msassetsku");
                }
                catch (Exception ex)
                {
                    allEgsResult.Error.AppendLine(ex.ToString());
                }

                if (allEgsResult.Error != null && allEgsResult.Error.ToString().Trim().Length > 0)
                {
                    string[] errorLines = allEgsResult.Error.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
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

                // Send out email to OA when no pid/mdmid found in MSAsset and no PO found for these Fids
                if (emailTrigger && error.ToString().IndexOf("MSAsset returned no output", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Filter the error just contains MsAsset association issues.
                    string[] sep = { Environment.NewLine, "\n" }; // "\n" added in case you manually appended a newline
                    string[] errList = error.ToString().Split(sep, StringSplitOptions.None);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("<p><h3>Below All EGs fids don't have both MsAsset and PO Associations, please find investigate. </h3></p>");

                    foreach (string line in errList)
                    {
                        if (line.IndexOf("MSAsset returned no output", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sb.AppendLine(line);
                        }
                    }

                    string subject = "Below All EGs Fids don't have MsAsset and PO Associations";
                    string to = Constants.msAssetSupportAccount + ";" + Constants.OADPMAccount;
                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, to, subject, sb.ToString());
                }
            }
            return allEgsResult;
        }

        private List<GdcoTicket.Model.GdcoTicket> CreateGdcoTickets(List<AllEgsOutputModel> results, string mode)
        {
            var gdcoTickets = new List<GdcoTicket.Model.GdcoTicket>();
            HashSet<String> failedDescriptions = new HashSet<String>();
            KustoAccess ka = new KustoAccess();

            if (results == null || !results.Any())
            {
                return gdcoTickets;
            }

            // Check for failed tests, create tickets
            foreach(var result in results)
            {
                var errorTypeToAllEgsResults = new Dictionary<string, List<AllEngineeringGroupsResult>>();
                var allEgsResults = result.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed") && !string.IsNullOrEmpty(eo.QcName));
                var mdmId = string.IsNullOrEmpty(result.ProjectId) ? result.MdmId : result.ProjectId;
                // compare the current ticket with CIS Kusto feed, if different, cut the ticket
                var kustoErrMsg = "";

                if (allEgsResults != null && allEgsResults.Any())
                {
                    kustoErrMsg = ka.GetErrorEsgByMdmid(mdmId);
                }

                foreach (var allEgResult in allEgsResults)
                {
                    if(!errorTypeToAllEgsResults.ContainsKey(allEgResult.QcName))
                    {
                        errorTypeToAllEgsResults[allEgResult.QcName] = new List<AllEngineeringGroupsResult> { allEgResult };
                    }
                    else
                    {
                        var exchangeResultList = errorTypeToAllEgsResults[allEgResult.QcName];
                        exchangeResultList.Add(allEgResult);
                    }
                }

                if(!errorTypeToAllEgsResults.Any())
                {
                    continue;
                }

                foreach(var key in errorTypeToAllEgsResults.Keys)
                {
                    var allEgsErrorList = errorTypeToAllEgsResults[key];
                    var projectId = string.IsNullOrEmpty(result.ProjectId) ? result.MdmId : result.ProjectId;
                    var currentWorkOrder = allEgsErrorList.First().WorkOrderName.Trim();
                    var errorCode = key;
                    string errorTitle = null;
                    var errorDescription = GetAllEgsResultError(allEgsErrorList);
                    var dcCode = result.DataCenterCode.Trim();
                    var requestOwner = "oadpm";
                    string severity = null;
                    string parentTicketId = null;

                    var gdcoTicket = new GdcoTicket.Model.GdcoTicket();
                    try
                    {
                        if (String.IsNullOrEmpty(kustoErrMsg))
                        {
                            // send email if there is something diff between us and their Kusto feed
                            EmailBody.AppendLine("<tr><td>" + projectId + "</td><td>" + errorTitle + "</td><td>" + errorDescription + "</td></tr>");
                        }


                        if (result.GroupType.IndexOf("PreRack", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            continue;
                        }

                        // Leave cutting ticket part here and will move into above if later
                        if (ErrorCodeMapping.LiveErrorChanges.Contains(errorCode))
                        {
                            var internalErrorCode = mode.Equals("P") ? ErrorCodeMapping.InternalGfsdErrorToInternalFc[key] : ErrorCodeMapping.InternalCisErrorToInternalFc[key];
                            errorTitle = ErrorCodeMapping.InternalFcToGdcoTicketTitle.Keys.Contains(internalErrorCode) ? ErrorCodeMapping.InternalFcToGdcoTicketTitle[internalErrorCode] : "";
                            errorDescription = GetAllEgsNewResultError(allEgsErrorList, internalErrorCode);
                            gdcoTicket = new GdcoTicketHandler().CreateErrorTicket(projectId, currentWorkOrder,
                                    internalErrorCode, errorTitle, errorDescription, dcCode, requestOwner, severity, parentTicketId, mode, "allegs_msassetsku");
                        }
                        else
                        {
                            gdcoTicket = new GdcoTicketHandler().CreateTicket(projectId, currentWorkOrder,
                                errorCode, errorTitle, errorDescription, dcCode, requestOwner, severity, parentTicketId, mode, "allegs_msassetsku");
                        }
                    }
                    catch (Exception ex)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Exception in CreateTicket(), Ticket Creation failed for ", "ProjectId: " + projectId + " Exception in CreateTicket(): " + ex);
                        continue;
                    }
                    errorDescription = errorDescription.Length > 1000 ? errorDescription.Substring(0, 1000) : errorDescription;
                    failedDescriptions.Add(errorDescription);
                    gdcoTickets.Add(gdcoTicket);
                    try
                    {
                        if (mode.Equals("P"))
                        {
                            new GdcoTicketHandler().AssignParent(projectId, gdcoTicket.Id);
                        }
                        else if (mode.Equals("M"))
                        {
                            var cisProject = cisProjectModel.CisProjects.Where(c => c.MdmId.Equals(projectId)).FirstOrDefault();
                            if (cisProject != null && cisProject.UpdateAssetDataTicketId != null)
                            {
                                new GdcoTicketHandler().AssignParent(Convert.ToInt64(cisProject.UpdateAssetDataTicketId), gdcoTicket.Id);
                            }
                        }
                        else if (mode.Equals("D"))
                        {
                            var dsDataSet = new DSDataSetAccess().GetInProgressAllEGsDeploymentsToRTEG();
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
                    catch (Exception ex)
                    {
                        SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "Exception in CreateGdcoTickets()", "ProjectId: " + projectId + " Exception in CreateGdcoTickets(): " + ex);
                        continue;
                    }
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
                var failedAllEgsResults = result.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed") && !string.IsNullOrEmpty(eo.QcName));
                var pid = string.IsNullOrEmpty(result.ProjectId) ? result.MdmId : result.ProjectId;

                if (!failedAllEgsResults.Any())
                {
                    pids.Add(pid);
                }
                else
                {
                    failedPids.Add(pid);
                }
            }
            if(pids.Any())
            {
                foreach(var pid in pids)
                {
                    gdcoTickets.AddRange(new GdcoTicketHandler().UpdateTicket(pid, "allegs_msassetsku"));
                }
            }
            if(failedPids.Any())
            {
                foreach (var pid in failedPids)
                {
                    gdcoTickets.AddRange(new GdcoTicketHandler().UpdateFailedTicket(pid, "allegs_msassetsku", failedDescriptions));
                }
            }
            return gdcoTickets;
        }

        private string GetAllEgsNewResultError(List<AllEngineeringGroupsResult> allEgsErrors, string internalFaultCode)
        {
            var errorDescription = new StringBuilder();
            if (ErrorCodeMapping.InternalFcToErrorBlurb.Keys.Contains(internalFaultCode))
            {
                errorDescription.AppendLine(ErrorCodeMapping.InternalFcToErrorBlurb[internalFaultCode]);
                errorDescription.AppendLine("");
            }

            if (allEgsErrors != null)
            {
                if (ErrorCodeMapping.RackLevelOnlyErrors.Contains(internalFaultCode))
                {
                    foreach (var exchangeError in allEgsErrors)
                    {
                        errorDescription.AppendLine("RackName: " + exchangeError.RackName);
                    }
                }
                else if (ErrorCodeMapping.IdMissingErrors.Contains(internalFaultCode))
                {
                    foreach (var exchangeError in allEgsErrors)
                    {
                        errorDescription.AppendLine("Missing ID: " + exchangeError.MdmId + exchangeError.ProjectId);
                    }
                }
                else if (ErrorCodeMapping.BlurbOnlyErrors.Contains(internalFaultCode))
                {
                    return errorDescription.ToString();
                }
                else
                {
                    foreach (var exchangeError in allEgsErrors)
                    {
                        errorDescription.AppendLine("Error: " + exchangeError.Comments + " RackName: " + exchangeError.RackName + " DeviceName: " + exchangeError.DeviceName + " DeviceType: " + exchangeError.DeviceType + " MSAssetValue: " + exchangeError.MsAssetValue + " ExpectedValue: " + exchangeError.SkuDocValue + " | ");
                    }
                }
            }
            return errorDescription.ToString();
        }

        private string GetAllEgsResultError(List<AllEngineeringGroupsResult> allEgsErrors)
        {
            var errorDescription = new StringBuilder();
            if (allEgsErrors != null)
            {
                foreach (var exchangeError in allEgsErrors)
                {
                    errorDescription.AppendLine("Error: " + exchangeError.Comments + " RackName: " + exchangeError.RackName + " DeviceName: " + exchangeError.DeviceName + " DeviceType: " + exchangeError.DeviceType + " MSAssetValue: " + exchangeError.MsAssetValue + " ExpectedValue: " + exchangeError.SkuDocValue + " | ");
                }
            }
            return errorDescription.ToString();
        }

        private List<AllEgsOutputReport> GetExoReport(List<AllEgsOutputModel> AllEgsOutput, string Mode)
        {
            var allEgsOutputReportList = new List<AllEgsOutputReport>();
            if(AllEgsOutput != null)
            {
                // Fetch the GDCO tickets
                var projectIds = new List<string>();
                foreach(var qcResult in AllEgsOutput)
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
                var gdcoTicketsDictionary = new GdcoTicketHandler().GetTickets(projectIds, "allegs_msassetsku");

                foreach(var qcResult in AllEgsOutput)
                {
                    var allEgsOutputReport = new AllEgsOutputReport();
                    allEgsOutputReport.PropertyGroupName = qcResult.PropertyGroupName;
                    if (qcResult != null && qcResult.AllEgsOutput != null)
                    {
                        if(Mode.Equals("P"))
                        {
                            allEgsOutputReport.ProjectId = qcResult.ProjectId;
                        }
                        else if(Mode.Equals("M") || Mode.Equals("D"))
                        {
                            allEgsOutputReport.MdmId = qcResult.MdmId;
                        }
                        allEgsOutputReport.TotalTests = qcResult.AllEgsOutput.Count;
                        allEgsOutputReport.PassedTests = qcResult.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Passed")).ToList().Count;
                        allEgsOutputReport.FailedTests = qcResult.AllEgsOutput.Where(eo => eo.TestStatus.Equals("Failed")).ToList().Count;
                        allEgsOutputReport.WorkOrder = qcResult.WorkOrderName;
                        if((!string.IsNullOrEmpty(qcResult.ProjectId) && gdcoTicketsDictionary.ContainsKey(qcResult.ProjectId)) || (!string.IsNullOrEmpty(qcResult.MdmId) && gdcoTicketsDictionary.ContainsKey(qcResult.MdmId)))
                        {
                            var pid = string.IsNullOrEmpty(qcResult.ProjectId) ? qcResult.MdmId : qcResult.ProjectId;
                            allEgsOutputReport.gdcoTickets = gdcoTicketsDictionary[pid];
                        }
                    }
                    allEgsOutputReportList.Add(allEgsOutputReport);
                }
            }
            return allEgsOutputReportList;
        }

        private List<string> SaveResultToFile(List<AllEgsOutputModel> AllEgsOutput, string Mode = "P")
        {
            var basePath = "\\\\mcio-oa-pc5\\Reporting\\";
            var format = ".csv";
            var fileName = "AllEGs_SkuMsAsset_" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss-fff");
            var reportFilePath = File.Create(basePath + "Report_" + fileName + format);
            reportFilePath.Close();
            var reports = new List<string> { reportFilePath.Name };

            try
            {
                var report = new StringBuilder();

                // Headers
                if(Mode.Equals("P"))
                {
                    report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                                "PropertyGroup",
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
                    report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                                "PropertyGroup",
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

                foreach(var allEgs in AllEgsOutput)
                {
                    foreach(var result in allEgs.AllEgsOutput)
                    {
                        if(Mode.Equals("P"))
                        {
                            report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                            allEgs.PropertyGroupName,
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
                            report.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                            allEgs.PropertyGroupName,
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
