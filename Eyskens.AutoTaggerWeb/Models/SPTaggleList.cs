using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Eyskens.AutoTaggerWeb.Models
{
    public class SPEnabledList
    {       
        public bool Disabled { get; set; }
    }
    public class SPTaggableList
    {
        public SPTaggableList()
        {
            Fields = new List<SPTaggableField>();
        }
        public string Title { get; set; }
        public List<SPTaggableField> Fields { get; set; }
        public string Id { get; set; }

        public string Disabled
        {
            get;
            set;
        }
        public bool Asynchronous
        {
            get;
            set;
        }
    }
    public class SPTaggableField
    {
        public SPTaggableField(){}
        public string Title { get; set; }
        public bool TaggingEnabled { get; set; }
        public string Id { get; set; }

    }
}