using Dapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BulkMD5.Models;

namespace BulkMD5
{
    class AsyncDownload
    {
        [ThreadStatic] List<Task> tasks = new List<Task>();
        [ThreadStatic] List<ImageDetails> imgs = new List<ImageDetails>();


        int DOWNLOAD_BATCH_SIZE = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("DOWNLOAD_BATCH_SIZE"));
        public const string ImageDownLoadRequestUserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:24.0) Gecko/20100101 Firefox/24.0";
        public void GetImgs()
        {

            try
            {
                using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
                {
                    connection.Open();

                    string query = @"select top 25000 i.id, i.imageurl from images i
                                    join Feeds f on f.ID = i.FeedID
                                    where f.IsActive = 1
                                    order by i.id desc";

                    imgs = connection.Query<ImageDetails>(query).ToList();
                }

                List<ImageDetails> imgSubSet = new List<ImageDetails>();
                for (int i = 0; i < imgs.Count; i++)
                {
                    imgSubSet = imgs.Skip(i * DOWNLOAD_BATCH_SIZE).Take(DOWNLOAD_BATCH_SIZE).ToList();

                    DownloadFiles(imgSubSet);

                    try
                    {
                        Task.WaitAll(tasks.ToArray());
                    }
                    catch (Exception te)
                    {
                        Debug.Print(te.Message);
                        Debug.Print(te.StackTrace);
                    }

                    //InsertHashes(imgSubSet);
                }

            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                Debug.Print(e.StackTrace);
            }
        }

        private void DownloadFiles(List<ImageDetails> imgs)
        {

            try
            {
                Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
                {
                    var task = Task.Run(() => DownloadFile(img));

                    if (task != null)
                    {
                        tasks.Add(task);
                        //task.Wait();
                    }

                });

            }
            catch (Exception eex)
            {
                Debug.Print(eex.StackTrace);
            }

        }


        private async Task DownloadFile(ImageDetails img)
        {
            try
            {
                Uri uri = new Uri(img.ImageUrl);

                //string formatedName = string.Format("{0}_{1:yyyy_MM_dd_hh_mm_ss_fff}", img.ID, DateTime.Now);
                string formatedName = string.Format("{0}", img.ID);

                string downloadToDirectory = @"C:\Users\Abhi\Documents\hashes\" + formatedName;

                img.pathOnDisk = downloadToDirectory;

                HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
                httpWebRequest.MaximumAutomaticRedirections = 1;
                httpWebRequest.UserAgent = ImageDownLoadRequestUserAgent;
                httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                IAsyncResult ar = httpWebRequest.BeginGetResponse(GetAsyncResponse, new object[] { httpWebRequest, formatedName });

                
            }
            catch (WebException we)
            {
                Debug.Print(we.Message);
                Debug.Print(we.StackTrace);
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                Debug.Print(e.StackTrace);
            }

        }

        private void GetAsyncResponse(IAsyncResult result)
        {
            object[] p = (object[])result.AsyncState;
            HttpWebRequest request = (HttpWebRequest)p[0];
            string fileName = (string)p[1];
            
            var response = request.GetResponse();
            var httpResp = (HttpWebResponse)response;

            using (var respStream = httpResp.GetResponseStream())
            using (var fStream = File.Create(fileName))
            {
                respStream.CopyTo(fStream);
            }
        }
    }
}
