using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkMD5.Models
{
    class BuzzBuzz_Developments_UnitPlans
    {
        public Guid DevelopmentID { get; set; }
        public Guid UnitID { get; set; }
        public Guid PlanID { get; set; }
        public Guid WikiPlanID { get; set; }
        public string PlanName { get; set; }
        public string PlanImage { get; set; }        
        public DateTime AddedDate { get; set; }
        public bool isApproved { get; set; }
        public bool isUpForDeletion { get; set; }
        public Guid LastUpdatedBy { get; set; }

        public string MD5Hash { get; set; }
        public string Error { get; set; }
        public string FormattedName { get; set; }
    }
}
