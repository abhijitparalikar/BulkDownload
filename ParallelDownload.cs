using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using System.Collections.Generic;

namespace BulkMD5
{
    class ParallelDownload
    {
        ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();
        [ThreadStatic] List<ImageDetails> imgs = new List<ImageDetails>();
        [ThreadStatic] int rejects = 0;

        int DOWNLOAD_BATCH_SIZE = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("DOWNLOAD_BATCH_SIZE"));
        public void GetImgs()
        {

            try
            {
                using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
                {
                    connection.Open();

                    string query = @"select top 100000 i.id, i.imageurl from images i
                                    join Feeds f on f.ID = i.FeedID
                                    where f.IsActive = 1
                                    order by i.id desc";

                    imgs = connection.Query<ImageDetails>(query).ToList();
                }

            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                Debug.Print(e.StackTrace);
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
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                    Debug.Print(e.StackTrace);
                }

                //InsertHashes(imgSubSet);
            }

            //Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
            //{
            //    var task = DownloadFile(img);
            //    if (task != null)
            //    {
            //        tasks.Add(task);
            //    }

            //});

            //Task.WaitAll(tasks.ToArray());

            Debug.Print("Total images rejected {0}", rejects);
        }

        private void DownloadFiles(List<ImageDetails> imgs)
        {

           
                Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
                {

                    try
                    {
                        var task = DownloadFile(img);
                        
                        if (task != null)
                        {
                            tasks.Add(task);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Print(e.Message);
                        Debug.Print(e.StackTrace);
                    }

                });
                
        }

        //private void GetEligibleWorkingURLs(List<ImageDetails> imgs)
        //{

        //    try
        //    {
        //        Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 1 }, (img) =>
        //        {
        //            try
        //            {
        //                var request = (HttpWebRequest)WebRequest.Create(img.ImageUrl);
        //                request.Timeout = 500;
        //                request.Method = "HEAD";

        //                using (var response = (HttpWebResponse)request.GetResponse())
        //                {
        //                    if (response.StatusCode == HttpStatusCode.OK && response.ContentType.Contains("image"))
        //                    {
        //                        img.isValid = true;

        //                    }
        //                }
        //            }
        //            catch (WebException we)
        //            {
        //                Debug.Print(we.StackTrace);
        //            }
        //        });
        //    }
        //    catch (Exception eeeee)
        //    {
        //        Debug.Print(eeeee.StackTrace);
        //        throw;
        //    }


        //}

        private Task DownloadFile(ImageDetails img)
        {

            //using (WebClient wc = new WebClient())
            using (DwnldWebClient wc = new DwnldWebClient())
            {
                
                Uri uri = new Uri(img.ImageUrl);

                //string formatedName = string.Format("{0}_{1:yyyy_MM_dd_hh_mm_ss_fff}", img.ID, DateTime.Now);
                string formatedName = string.Format("{0}", img.ID);

                string downloadToDirectory = @"C:\Users\Abhi\Documents\hashes\" + formatedName;

                img.pathOnDisk = downloadToDirectory;

                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 500;
                request.Method = "HEAD";

                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK && response.ContentType.Contains("image"))
                        {
                            
                            return wc.DownloadFileTaskAsync(uri, downloadToDirectory);

                        }
                        else
                        {
                            rejects++;
                            return null;
                        }
                    }
                }
                catch (System.Net.WebException ex)
                {
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    rejects++;
                    //return null;
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    rejects++;
                    //return null;
                }
                return null;
                //return wc.DownloadFileTaskAsync(uri, downloadToDirectory);
            }

        }

        public void InsertHashes(List<ImageDetails> imgs)
        {
            try
            {

                DataTable table = new DataTable();
                table.Columns.Add("ID", typeof(int));
                table.Columns.Add("Image_ID", typeof(int));
                table.Columns.Add("Hash", typeof(string));

                foreach (var img in imgs)
                {
                    DataRow row = table.NewRow();
                    row["Image_ID"] = img.ID;
                    row["Hash"] = img.hash;

                    table.Rows.Add(row);

                }

                using (var sqlBulk = new SqlBulkCopy(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
                {

                    sqlBulk.DestinationTableName = "Images_Hashes";
                    sqlBulk.WriteToServer(table);
                }


            }
            catch (Exception e)
            {
                Debug.Print(e.StackTrace);
            }
        }
    }
}
