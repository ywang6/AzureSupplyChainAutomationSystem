using QcDriver;
using Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utilities;
using Utilities.Model;

namespace AllEgsScheduledTask
{
    public class AllEgsScheduledRunner
    {
        public static void Main(string[] args)
        {
            var retryCount = 0;
            var retry = false;
            string from = Constants.serviceAccountAlias;
            string to = "oadpm";

            var dsList = new List<string>();
            var exoDsList = new List<string>();
            var dsQueue = "";
            var exoDsQueue = "";

            var dsProjects = new Dictionary<string, List<DSDataSet>>();
            var exoDsProjects = new Dictionary<string, List<DSDataSet>>();
            try
            {
                dsProjects = new DSDataSetAccess().GetInProgressAllEGsDeploymentsToRTEG();
            }
            catch (Exception)
            {
            }
            try
            {
                exoDsProjects = new DSDataSetAccess().GetInProgressExchangeDeploymentsToRTEG();
            }
            catch (Exception)
            {
            }
                
            foreach (var dsProject in dsProjects.Keys)
            {
                var dsTasks = dsProjects[dsProject];
                var oaTask = dsTasks.Where(d => d.GDCOFaultCode != null && d.GDCOFaultCode.Equals("124110") && !d.TicketResolvedDate.HasValue).FirstOrDefault();
                if (oaTask != null)
                {
                    dsList.Add(dsProject);
                }
            }
            foreach (var dsProject in exoDsProjects.Keys)
            {
                var dsTasks = exoDsProjects[dsProject];
                var oaTask = dsTasks.Where(d => d.GDCOFaultCode != null && d.GDCOFaultCode.Equals("124110") && !d.TicketResolvedDate.HasValue).FirstOrDefault();
                if (oaTask != null)
                {
                    exoDsList.Add(dsProject);
                }
            }

            if (!dsList.Any() && !exoDsList.Any())
            {
                return;
            }

            do
            {
                retry = false;
                try
                {
                    if (dsList.Any())
                    {
                        dsQueue = String.Join(",", dsList);
                        new AllEgsQcDriver().GetDSMsAssetSkuValidationResult(dsQueue, Constants.serviceAccountAlias, true, Constants.automationTeam);
                    }
                    new ReportingHandler().SendAllEgsReport(Constants.serviceAccountAlias, to, "every3hours");

                    if (exoDsList.Any())
                    {
                        exoDsQueue = String.Join(",", exoDsList);
                        new ExchangeQcDriver().GetDSMsAssetSkuValidationResult(exoDsQueue, Constants.serviceAccountAlias, true, Constants.automationTeam);
                    }
                    new ReportingHandler().SendReport(Constants.serviceAccountAlias, to, "every3hours");
                }
                catch (Exception ex)
                {
                    retry = true;
                    retryCount++;
                    Thread.Sleep(20000);
                }
            } while (retry == true && retryCount < 5);
        }
    }
}
