using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkMD5.Models
{
    class BuzzBuzz_Developments_UserImage
    {
        public Guid DevelopmentID { get; set; }
        public Guid ImageID { get; set; }
        public string ImageName { get; set; }
        public string ImageFile { get; set; }
        public string Owner { get; set; }
        public DateTime DateAdded { get; set; }
        public int CatgeoryID { get; set; }
        public bool isApproved { get; set; }
        public bool isUpForDeletion { get; set; }
        public Guid WikiHistoryID { get; set; }
        public Guid WikiImageID { get; set; }
        public Guid EditedBy { get; set; }
        public string MD5Hash { get; set; }
        public string Error { get; set; }
        public string FormattedName { get; set; }
    }
}
