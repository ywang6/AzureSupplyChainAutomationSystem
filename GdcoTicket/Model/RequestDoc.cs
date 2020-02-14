using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GdcoTicket.Model
{
    public class RequestDoc
    {
        public bool Count = false;
        public List<string> Facets = new List<string>();
        public string Filter = "";
        public List<string> Orderby = new List<string>();
        public List<string> SearchFields = new List<string>();
        public string Search = "*";
        public List<string> Select = new List<string>();
        public string SearchMode = "all";
        public int Skip = 0;
        public int Top = 400;
    }
}
