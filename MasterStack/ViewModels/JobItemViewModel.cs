using System;
using System.Collections.Generic;

namespace MasterStack.ViewModels
{
    public class JobItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CompanyName { get; set; }
        public string Location { get; set; }
        public string JobType { get; set; } 
        public DateTime PostedDate { get; set; }
        public List<string> Skills { get; set; } = new List<string>();
    }
}