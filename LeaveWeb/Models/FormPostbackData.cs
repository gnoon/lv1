using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Leave.Models
{
    public class FormPostbackData : CommonPostbackData
    {
        public string PersonNo { get; set; }
        public int TypeSubID { get; set; }
        public string BeginDate { get; set; }
        public string BeginTime { get; set; }
        public string UntilDate { get; set; }
        public string UntilTime { get; set; }
        public string Reason { get; set; }
        public string TypeCase { get; set; }
    }
}