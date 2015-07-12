using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyskens.AutoTaggerWeb.Models
{
    public class DocumentInformation
    {
        public List<string> Tokens
        {
            get;
            set;
        }

        public string Locations
        {
            get;
            set;
        }
        public string Organizations
        {
            get;
            set;
        }
        public string Persons
        {
            get;
            set;
        }
    }
}
