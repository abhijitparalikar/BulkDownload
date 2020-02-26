using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkMD5
{
    public class ImageDetails
    {
        public int ID { get; set; }
        public string ImageUrl { get; set; }
        public string pathOnDisk { get; set; }
        public string hash { get; set; }
        public string error { get; set; }
    }
}
