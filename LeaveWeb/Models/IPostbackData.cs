using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Leave.Models
{
    public interface IPostbackData
    {
        string FocusOnElementID { get; set; }
        string AlertMessage { get; set; }
        bool AlertEnabled { get; set; }
    }

    public class CommonPostbackData : IPostbackData
    {
        public string FocusOnElementID { get; set; }
        public string AlertMessage { get; set; }
        public bool AlertEnabled { get; set; }
    }
}