using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class GdcoTicket
    {
        public long Id { get; set; }
        public int Rev { get; set; }
        public Dictionary<string, object> Fields { get; set; }
        public List<Relation> Relations { get; set; }
        public string Error { get; set; }
        public object KeyLookup(string keyName)
        {
            object result;
            if (this.Fields == null)
            {
                return null;
            }
            if (!this.Fields.TryGetValue(keyName, out result))
            {
                return null;
            }
            return result;
        }
    }
}
