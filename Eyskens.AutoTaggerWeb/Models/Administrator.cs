using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyskens.AutoTaggerWeb.Models
{
    public class Administrator
    {
        public int id
        {
            get;
            set;
        }
        [Display(Name = "Login or Email")]
        [DataType(DataType.Text)]     
        public string LoginName
        {
            get;
            set;
        }
    }
}
