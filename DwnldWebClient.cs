using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace BulkMD5
{
    class DwnldWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            HttpWebRequest request = base.GetWebRequest(uri) as HttpWebRequest;
            request.Timeout = 500;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.MaximumAutomaticRedirections = 1;
            //request.AllowAutoRedirect = false;
            return request;
        }
    }
}
