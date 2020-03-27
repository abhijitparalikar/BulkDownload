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
using BulkMD5.Models;
using Serilog;

namespace BulkMD5
{
    class ParallelDownload
    {
        ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();
        [ThreadStatic] List<ImageDetails> imgs = new List<ImageDetails>();
        [ThreadStatic] int rejects = 0;

        int DOWNLOAD_BATCH_SIZE = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("DOWNLOAD_BATCH_SIZE"));
        public const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.122 Safari/537.36";
        private readonly string INVALID_TYPE_ERROR = "Not an image";
        private readonly string EMPTY_MD5_HASH = "d41d8cd98f00b204e9800998ecf8427e";
        public void GetImgs()
        {

            try
            {
                using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
                {                    
                    connection.Open();

                    /* Images for Maronda Homes excluded except for top 10,000.
                     * Images take too long to download. To be processed separately.
                     */
                    string query = @"select top 100000 i.id, i.imageurl from images i with(nolock)
                                    join Feeds f with(nolock) on f.ID = i.FeedID
                                    join Mappings m on m.FeedObjectID = i.ProviderParentID
                                    where f.IsActive = 1
                                    and f.ID != '3839506C-D933-4856-9EF7-4E46998A0F3D'                                    
                                    and m.Status in (1,2)
                                    and i.id not in(
                                        select image_ID from Images_Hashes with(nolock)
                                    )
                                    order by i.id desc";
                    
                    imgs = connection.Query<ImageDetails>(query, commandTimeout: 240).ToList();
                    Log.Information("Processing {0} images", imgs.Count);
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
                    //InsertHashes(imgSubSet);
                }
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                    Debug.Print(e.StackTrace);
                }

                
            }

            CalculateAndInsertMD5();
            Log.Information("Total images rejected {0}", rejects);
            //Debug.Print("Total images rejected {0}", rejects);
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
                        img.error = e.Message;

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
                request.Timeout = 2000;
                request.Method = "HEAD";
                request.UserAgent = userAgent;

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
                            img.error = INVALID_TYPE_ERROR;
                            rejects++;
                            img.hash = EMPTY_MD5_HASH;
                            return null;
                        }
                    }
                }
                catch (System.Net.WebException ex)
                {
                    img.error = ex.Message;
                    img.hash = EMPTY_MD5_HASH;
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    rejects++;
                    //return null;
                }
                catch (Exception ex)
                {
                    img.error = ex.Message;
                    img.hash = EMPTY_MD5_HASH;
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    rejects++;
                    //return null;
                }
                return null;
                //return wc.DownloadFileTaskAsync(uri, downloadToDirectory);
            }

        }

        private void CalculateAndInsertMD5()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
            {
                if (!string.IsNullOrWhiteSpace(img.pathOnDisk) && img.hash != EMPTY_MD5_HASH)
                {
                    FileInfo fileInfo = new FileInfo(img.pathOnDisk);
                    using (var fStream = fileInfo.OpenRead())
                    {
                        if (fileInfo.Length > 0)
                        {
                            var md5 = MD5.Create();
                            img.hash = BitConverter.ToString(md5.ComputeHash(fStream)).Replace("-", string.Empty);
                        }
                    }
                }
                
            });
            InsertHashes(imgs);
            /*
            var dir = Directory.CreateDirectory(@"C:\Users\Abhi\Documents\hashes\");
            var files = dir.GetFiles();
            List<ImageDetails> iHash = new List<ImageDetails>();

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (file) =>
            {

                using (var fStream = file.OpenRead())
                {
                    ImageDetails details = new ImageDetails();
                    details.ID = Int32.Parse(file.Name);
                    if (file.Length > 0)
                    {
                        var md5 = MD5.Create();
                        details.hash = BitConverter.ToString(md5.ComputeHash(fStream)).Replace("-", string.Empty);
                    }

                    iHash.Add(details);
                }
            });


            //iHash.Union(imgs.Where(x => !iHash.Select(y => y.ID).Contains(x.ID)));
            var exclusions = imgs.Where(x => !iHash.Any(y => y.ID == x.ID));
            var allImgs =  iHash.Union(exclusions);

            InsertHashes(allImgs);
            */
            watch.Stop();

            Log.Information("Hashes processed in {0} mins", ((float)watch.ElapsedMilliseconds / (float)60000));
            //Debug.Print("Hashes processed in {0} mins", ((float)watch.ElapsedMilliseconds / (float)60000));
        }

        public void InsertHashes(IEnumerable<ImageDetails> imgs)
        {
            try
            {

                DataTable table = new DataTable();
                table.Columns.Add("ID", typeof(int));
                table.Columns.Add("Image_ID", typeof(int));
                table.Columns.Add("Hash", typeof(string));
                table.Columns.Add("Error", typeof(string));

                foreach (var img in imgs)
                {
                    DataRow row = table.NewRow();
                    row["Image_ID"] = img.ID;
                    row["Hash"] = img.hash;
                    row["Error"] = img.error;

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
