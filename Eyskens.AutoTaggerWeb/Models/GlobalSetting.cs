using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Eyskens.AutoTaggerWeb.Models
{    
    public class GlobalSetting
    {
        public int id
        {
            get;
            set;
        }
        public string key
        {
            get;
            set;
        }
        [Required(ErrorMessage = "You must specify a value")]            
        public string value
        {
            get;
            set;
        }
    }
}
