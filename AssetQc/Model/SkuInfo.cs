using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetQc.Model
{
    public class SkuInfo
    {
        public List<string> ApprovedDcLocations { get; set; }
        public TechSpec ServerSpec { get; set; }
        public TechSpec ChassisSpec { get; set; }
        public TechSpec ChassisManagerSpec { get; set; }
        public TechSpec RackSpec { get; set; }
        public TechSpec UpsSpec { get; set; }
        public TechSpec SwitchSpec { get; set; }
        public TechSpec LoadBalancerSpec { get; set; }
        public TechSpec IloSpec { get; set; }
        public TechSpec DigiSpec { get; set; }
        public List<PhysicalRackLayout> ServerLayout { get; set; }
        public List<PhysicalRackLayout> UpsLayout { get; set; }
        public List<PhysicalRackLayout> LoadBalancerLayout { get; set; }
        public List<PhysicalRackLayout> ChassisLayout { get; set; }
        public List<PhysicalRackLayout> ChassisManagerLayout { get; set; }
        public List<PhysicalRackLayout> RackLayout { get; set; }
        public List<PhysicalRackLayout> SwitchLayout { get; set; }
        public List<PhysicalRackLayout> IloLayout { get; set; }
        public List<PhysicalRackLayout> DigiLayout { get; set; }
        public string Exception { get; set; }
    }
}
