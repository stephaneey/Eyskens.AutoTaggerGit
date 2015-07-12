using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace Eyskens.AutoTaggerWeb.Models
{
    public class EmptyWord
    {
        public int id
        {
            get;
            set;
        }

        [Required(ErrorMessage = "You must specify a value")]
        [Display(Name = "Word")]
        [DataType(DataType.Text)]        
        public string word
        {
            get;
            set;
        }
        [Required(ErrorMessage = "You must specify a value that is eng or fra")]
        [Display(Name = "Language")]
        [DataType(DataType.Text)]    
        public string lang
        {
            get;
            set;
        }
    }
}
