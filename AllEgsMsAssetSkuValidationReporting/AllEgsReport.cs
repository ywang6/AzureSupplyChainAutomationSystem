using QcDriver;
using Reporting;
using System;
using System.Threading;
using Utilities;

namespace AllEgsMsAssetSkuValidationReporting
{
    public class AllEgsReport
    {
        public static void Main(string[] args)
        {
            var retryCount = 0;
            var retry = false;
            string from = Constants.serviceAccountAlias;
            do
            {
                retry = false;
                try
                {
                    new AllEgsQcDriver().GetDSMsAssetSkuValidationResult(null, Constants.serviceAccountAlias, true, Constants.automationTeam);
                    new ReportingHandler().SendAllEgsReport(Constants.serviceAccountAlias, "oadpm,jpathuri,v-vapul,nandab,azizm,lasmi,majdib", "daily");

                    new ExchangeQcDriver().GetDSMsAssetSkuValidationResult(null, Constants.serviceAccountAlias, true, Constants.automationTeam);
                    new ReportingHandler().SendReport(Constants.serviceAccountAlias, "oadpm", "daily");
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
