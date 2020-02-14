using AssetQc.Model;
using SkuRef;
using SkuRef.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilities;

namespace AssetQc
{
    public class FetchSku
    {
        public SkuInfo FetchSkuInfo(string rackSkuName)
        {
            SkuInfo skuInfo = null;
            SkuObject skuObject = null;
            try
            {
                var skuRefHandler = new SkuRefHandler();
                skuObject = skuRefHandler.GetSkuDetails(rackSkuName);
                if (skuObject == null || skuObject.Results == null || !skuObject.Results.Any() || !string.IsNullOrEmpty(skuObject.Error))
                {
                    return skuInfo;
                }
            }
            catch (Exception ex)
            {
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccountAlias, Constants.automationTeam, "FetchSkuInfo exception", ex.ToString());
                return skuInfo;
            }

            // Initialize the skuInfo object
            skuInfo = new SkuInfo();
            foreach (var skuInfoResult in skuObject.Results)
            {
                if (skuInfoResult != null && skuInfoResult.Name != null && skuInfoResult.Name.Equals(rackSkuName, StringComparison.OrdinalIgnoreCase) && skuInfoResult.Items != null)
                {
                    // Fill Servers
                    var serverItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("Server", StringComparison.OrdinalIgnoreCase) && IsServer(sItem.SingleScanDetails));
                    if (serverItems != null && serverItems.Any())
                    {
                        skuInfo.ServerSpec = new TechSpec();
                        skuInfo.ServerLayout = new List<PhysicalRackLayout>();
                        foreach (var serverItem in serverItems)
                        {
                            if (serverItem != null)
                            {
                                skuInfo.ServerSpec.QuantityPerRack += serverItem.Quantity;
                                skuInfo.ServerSpec.DskuName = string.IsNullOrEmpty(skuInfo.ServerSpec.DskuName) ? 
                                                        (serverItem.DiscreteSkuName):
                                                        (skuInfo.ServerSpec.DskuName + ";" + serverItem.DiscreteSkuName);
                                skuInfo.ServerSpec.MsfNumber = serverItem.MsfPartNumberAx;
                                skuInfo.ServerSpec.Model = serverItem.ModelName;
                                if (serverItem.SingleScanDetails != null && serverItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = serverItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.ServerLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill Chassis
                    var chassisItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("Chassis", StringComparison.OrdinalIgnoreCase) && IsChassis(sItem.SingleScanDetails));
                    if (chassisItems != null && chassisItems.Any())
                    {
                        skuInfo.ChassisSpec = new TechSpec();
                        skuInfo.ChassisLayout = new List<PhysicalRackLayout>();
                        foreach (var chassisItem in chassisItems)
                        {
                            if (chassisItem != null)
                            {
                                skuInfo.ChassisSpec.QuantityPerRack += chassisItem.Quantity;
                                skuInfo.ChassisSpec.DskuName = string.IsNullOrEmpty(skuInfo.ChassisSpec.DskuName) ?
                                                        (chassisItem.DiscreteSkuName) :
                                                        (skuInfo.ChassisSpec.DskuName + ";" + chassisItem.DiscreteSkuName);
                                skuInfo.ChassisSpec.MsfNumber = chassisItem.MsfPartNumberAx;
                                skuInfo.ChassisSpec.Model = chassisItem.ModelName;
                                if (chassisItem.SingleScanDetails != null && chassisItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = chassisItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.ChassisLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill Chassis Manager
                    var chassisManagerItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("Server", StringComparison.OrdinalIgnoreCase) && IsChassisManager(sItem.SingleScanDetails));
                    if (chassisManagerItems != null && chassisManagerItems.Any())
                    {
                        skuInfo.ChassisManagerSpec = new TechSpec();
                        skuInfo.ChassisManagerLayout = new List<PhysicalRackLayout>();
                        foreach (var chassisManagerItem in chassisManagerItems)
                        {
                            if (chassisManagerItem != null)
                            {
                                skuInfo.ChassisManagerSpec.QuantityPerRack += chassisManagerItem.Quantity;
                                skuInfo.ChassisManagerSpec.DskuName = string.IsNullOrEmpty(skuInfo.ChassisManagerSpec.DskuName) ?
                                                        (chassisManagerItem.DiscreteSkuName) :
                                                        (skuInfo.ChassisManagerSpec.DskuName + ";" + chassisManagerItem.DiscreteSkuName);
                                skuInfo.ChassisManagerSpec.MsfNumber = chassisManagerItem.MsfPartNumberAx;
                                skuInfo.ChassisManagerSpec.Model = chassisManagerItem.ModelName;
                                if (chassisManagerItem.SingleScanDetails != null && chassisManagerItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = chassisManagerItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.ChassisManagerLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill Rack
                    var rackItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("Rack", StringComparison.OrdinalIgnoreCase) && IsRack(sItem.SingleScanDetails));
                    if (rackItems != null && rackItems.Any())
                    {
                        skuInfo.RackSpec = new TechSpec();
                        skuInfo.RackLayout = new List<PhysicalRackLayout>();
                        foreach (var rackItem in rackItems)
                        {
                            if (rackItem != null)
                            {
                                skuInfo.RackSpec.QuantityPerRack += rackItem.Quantity;
                                skuInfo.RackSpec.DskuName = string.IsNullOrEmpty(skuInfo.RackSpec.DskuName) ?
                                                        (rackItem.DiscreteSkuName) :
                                                        (skuInfo.RackSpec.DskuName + ";" + rackItem.DiscreteSkuName);
                                skuInfo.RackSpec.MsfNumber = rackItem.MsfPartNumberAx;
                                skuInfo.RackSpec.Model = rackItem.ModelName;
                                if (rackItem.SingleScanDetails != null && rackItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = rackItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.RackLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill UPS
                    var upsItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) &&
                                    (sItem.AssetType.Equals("Server", StringComparison.OrdinalIgnoreCase) || sItem.AssetType.Equals("PowerStrip", StringComparison.OrdinalIgnoreCase)) && IsUps(sItem.SingleScanDetails));
                    if (upsItems != null && upsItems.Any())
                    {
                        skuInfo.UpsSpec = new TechSpec();
                        skuInfo.UpsLayout = new List<PhysicalRackLayout>();
                        foreach (var upsItem in upsItems)
                        {
                            if (upsItem != null)
                            {
                                skuInfo.UpsSpec.QuantityPerRack += upsItem.Quantity;
                                skuInfo.UpsSpec.DskuName = string.IsNullOrEmpty(skuInfo.UpsSpec.DskuName) ?
                                                        (upsItem.DiscreteSkuName) :
                                                        (skuInfo.UpsSpec.DskuName + ";" + upsItem.DiscreteSkuName);
                                skuInfo.UpsSpec.MsfNumber = upsItem.MsfPartNumberAx;
                                skuInfo.UpsSpec.Model = upsItem.ModelName;
                                if (upsItem.SingleScanDetails != null && upsItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = upsItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.UpsLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill ToR Switch
                    var switchItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("NetDevice", StringComparison.OrdinalIgnoreCase) && IsSwitch(sItem.SingleScanDetails));
                    if (switchItems != null && switchItems.Any())
                    {
                        skuInfo.SwitchSpec = new TechSpec();
                        skuInfo.SwitchLayout = new List<PhysicalRackLayout>();
                        foreach (var switchItem in switchItems)
                        {
                            if (switchItem != null)
                            {
                                skuInfo.SwitchSpec.QuantityPerRack += switchItem.Quantity;
                                skuInfo.SwitchSpec.DskuName = string.IsNullOrEmpty(skuInfo.SwitchSpec.DskuName) ?
                                                        (switchItem.DiscreteSkuName) :
                                                        (skuInfo.SwitchSpec.DskuName + ";" + switchItem.DiscreteSkuName);
                                skuInfo.SwitchSpec.MsfNumber = switchItem.MsfPartNumberAx;
                                skuInfo.SwitchSpec.Model = switchItem.ModelName;
                                if (switchItem.SingleScanDetails != null && switchItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = switchItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.SwitchLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill Load Balancer
                    var loadBalancerItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("NetDevice", StringComparison.OrdinalIgnoreCase) &&
                                                                        !string.IsNullOrEmpty(sItem.DiscreteSkuNetDeviceType) && sItem.DiscreteSkuNetDeviceType.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (loadBalancerItems != null && loadBalancerItems.Any())
                    {
                        skuInfo.LoadBalancerSpec = new TechSpec();
                        skuInfo.LoadBalancerLayout = new List<PhysicalRackLayout>();
                        foreach (var loadBalancerItem in loadBalancerItems)
                        {
                            if (loadBalancerItem != null)
                            {
                                skuInfo.LoadBalancerSpec.QuantityPerRack += loadBalancerItem.Quantity;
                                skuInfo.LoadBalancerSpec.DskuName = string.IsNullOrEmpty(skuInfo.LoadBalancerSpec.DskuName) ?
                                                        (loadBalancerItem.DiscreteSkuName) :
                                                        (skuInfo.LoadBalancerSpec.DskuName + ";" + loadBalancerItem.DiscreteSkuName);
                                skuInfo.LoadBalancerSpec.MsfNumber = loadBalancerItem.MsfPartNumberAx;
                                skuInfo.LoadBalancerSpec.Model = loadBalancerItem.ModelName;
                                if (loadBalancerItem.SingleScanDetails != null && loadBalancerItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = loadBalancerItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.LoadBalancerLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Fill ILO and DIGI
                    var iloItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("NetDevice", StringComparison.OrdinalIgnoreCase) &&
                                                                        !string.IsNullOrEmpty(sItem.DiscreteSkuNetDeviceType) && sItem.DiscreteSkuNetDeviceType.IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                                        IsIloSwitch(sItem.SingleScanDetails));
                    var digiItems = skuInfoResult.Items.Where(sItem => !string.IsNullOrEmpty(sItem.AssetType) && sItem.AssetType.Equals("NetDevice", StringComparison.OrdinalIgnoreCase) &&
                                                                        !string.IsNullOrEmpty(sItem.Manufacturer) && sItem.Manufacturer.IndexOf("Digi", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                                        IsDigiSwitch(sItem.SingleScanDetails));
                    iloItems = iloItems.Union(digiItems);
                    if (iloItems != null && iloItems.Any())
                    {
                        skuInfo.IloSpec = new TechSpec();
                        skuInfo.IloLayout = new List<PhysicalRackLayout>();
                        foreach (var iloItem in iloItems)
                        {
                            if (iloItem != null)
                            {
                                skuInfo.IloSpec.QuantityPerRack += iloItem.Quantity;
                                skuInfo.IloSpec.DskuName = string.IsNullOrEmpty(skuInfo.IloSpec.DskuName) ?
                                                        (iloItem.DiscreteSkuName) :
                                                        (skuInfo.IloSpec.DskuName + ";" + iloItem.DiscreteSkuName);
                                skuInfo.IloSpec.MsfNumber = iloItem.MsfPartNumberAx;
                                skuInfo.IloSpec.Model = iloItem.ModelName;
                                if (iloItem.SingleScanDetails != null && iloItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = iloItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.IloLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (digiItems != null && digiItems.Any())
                    {
                        skuInfo.DigiSpec = new TechSpec();
                        skuInfo.DigiLayout = new List<PhysicalRackLayout>();
                        foreach (var digiItem in digiItems)
                        {
                            if (digiItem != null)
                            {
                                skuInfo.DigiSpec.QuantityPerRack += digiItem.Quantity;
                                skuInfo.DigiSpec.DskuName = string.IsNullOrEmpty(skuInfo.DigiSpec.DskuName) ?
                                                        (digiItem.DiscreteSkuName) :
                                                        (skuInfo.DigiSpec.DskuName + ";" + digiItem.DiscreteSkuName);
                                skuInfo.DigiSpec.MsfNumber = digiItem.MsfPartNumberAx;
                                skuInfo.DigiSpec.Model = digiItem.ModelName;
                                if (digiItem.SingleScanDetails != null && digiItem.SingleScanDetails.Any())
                                {
                                    var singleScanDetails = digiItem.SingleScanDetails;
                                    foreach (var ssd in singleScanDetails)
                                    {
                                        if (ssd != null)
                                        {
                                            skuInfo.DigiLayout.Add(new PhysicalRackLayout
                                            {
                                                Slot = ssd.SlotNum,
                                                BinNumber = converToInt(ssd.BinNum)
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return skuInfo;
        }

        private bool IsServer(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && (ssd.LayoutId.IndexOf("UPS", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsChassis(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && (ssd.LayoutId.IndexOf("CHASSIS", StringComparison.OrdinalIgnoreCase) >= 0 || ssd.LayoutId.IndexOf("RACKMANAGER", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsChassisManager(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && ssd.LayoutId.IndexOf("TS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsRack(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && ssd.LayoutId.IndexOf("RACKFRAME", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsUps(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && ssd.LayoutId.IndexOf("UPS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsSwitch(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.Role) && ssd.Role.IndexOf("ToR", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsIloSwitch(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && (ssd.LayoutId.IndexOf("NS2", StringComparison.OrdinalIgnoreCase) >= 0 || ssd.LayoutId.IndexOf("ILO", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsDigiSwitch(List<SingleScanDetail> singleScanDetail)
        {
            if (singleScanDetail != null)
            {
                foreach (var ssd in singleScanDetail)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.LayoutId) && ssd.LayoutId.IndexOf("TS1", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int converToInt(string num)
        {
            if(string.IsNullOrEmpty(num))
            {
                return 0;
            }

            int parsedNum = 0;
            int.TryParse(num.Substring(0, 2), out parsedNum);
            return parsedNum;
        }
    }
}
