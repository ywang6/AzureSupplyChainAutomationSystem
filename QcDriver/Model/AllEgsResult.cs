using AssetQc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QcDriver.Model
{
    public class AllEgsResult
    {
        public List<AllEgsOutputModel> Result { get; set; }
        public List<AllEgsOutputReport> Report { get; set; }
        public StringBuilder Error { get; set; }
    }
}
