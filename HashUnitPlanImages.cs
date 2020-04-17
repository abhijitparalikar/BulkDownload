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
using Serilog;
using BulkMD5.Models;


namespace BulkMD5
{
    class HashUnitPlanImages
    {
        private static string ssfDBName = System.Configuration.ConfigurationManager.AppSettings["ssfDBName"];

        ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();
        [ThreadStatic] List<BuzzBuzz_Developments_UnitPlans> imgs = new List<BuzzBuzz_Developments_UnitPlans>();
        [ThreadStatic] int rejects = 0;

        int DOWNLOAD_BATCH_SIZE = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("DOWNLOAD_BATCH_SIZE"));
        public const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.122 Safari/537.36";
        private readonly string INVALID_TYPE_ERROR = "Not an image";
        private readonly string EMPTY_MD5_HASH = "d41d8cd98f00b204e9800998ecf8427e";
        private readonly string UNITPLAN_IMG_S3_PATH = System.Configuration.ConfigurationManager.AppSettings.Get("S3Path") + "/" + System.Configuration.ConfigurationManager.AppSettings.Get("PlanUnitImagesBucket");
        private readonly string DownloadDirectory = System.Configuration.ConfigurationManager.AppSettings.Get("DownloadDirectory");

        public void GetImgs()
        {

            try
            {
                using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["BBHDB"].ConnectionString))
                {
                    connection.Open();

                    //string query = string.Format(@"select top 100000 d.DevelopmentID, up.PlanID, up.UnitID, up.WikiHistoryID, up.PlanName, up.PlanImage from BuzzBuzz_Developments_UnitPlans up with(nolock)
                    //                join BuzzBuzz_Developments_Units u with(nolock) on u.UnitID = up.UnitID
                    //                join BuzzBuzz_Developments_Collections c with(nolock) on c.CollectionID = u.CollectionID
                    //                join BuzzBuzz_Developments d with(nolock) on d.DevelopmentID = c.DevelopmentID
                    //                join [{0}].dbo.mappings m on m.objectid = d.developmentid
                    //                where d.isActive = 1
                    //                and ((d.IsHomeForRent = 1 and d.LeasingStatusID != 3) or (d.IsHomeForSale = 1 and d.SellingStatusID != 3))                                    
                    //                and up.PlanID not in
                    //                (
                    //                 select planid from Hashes_UnitsPlans_Images
                    //                )", ssfDBName);

                    string query = @"select top 100000 d.DevelopmentID, up.PlanID, up.UnitID, up.WikiHistoryID, up.PlanName, up.PlanImage from BuzzBuzz_Developments_UnitPlans up with(nolock)
                                    join BuzzBuzz_Developments_Units u with(nolock) on u.UnitID = up.UnitID
                                    join BuzzBuzz_Developments_Collections c with(nolock) on c.CollectionID = u.CollectionID
                                    join BuzzBuzz_Developments d with(nolock) on d.DevelopmentID = c.DevelopmentID
                                    where d.isActive = 1
                                    and ((d.IsHomeForRent = 1 and d.LeasingStatusID != 3) or (d.IsHomeForSale = 1 and d.SellingStatusID != 3))                                    
                                    and up.PlanID not in
                                    (
	                                    select planid from Hashes_UnitsPlans_Images
                                    )";

                    imgs = connection.Query<BuzzBuzz_Developments_UnitPlans>(query, commandTimeout: 240).ToList();
                    Log.Information("Processing {0} images", imgs.Count);
                }

            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
                Debug.Print(e.Message);
                //Debug.Print(e.StackTrace);
            }

            Log.Information("Downloading images");

            List<BuzzBuzz_Developments_UnitPlans> imgSubSet = new List<BuzzBuzz_Developments_UnitPlans>();
            for (int i = 0; i < imgs.Count; i++)
            {
                imgSubSet = imgs.Skip(i * DOWNLOAD_BATCH_SIZE).Take(DOWNLOAD_BATCH_SIZE).ToList();
                DownloadFiles(imgSubSet);

                try
                {
                    Task.WaitAll(tasks.ToArray());
                    tasks = new ConcurrentBag<Task>();
                    //InsertHashes(imgSubSet);
                }
                catch (Exception e)
                {
                    Log.Error("Task "+ i +" errored out: "+  e.Message);
                    Log.Error(e.InnerException.Message);
                    
                    Log.Error(e.StackTrace);
                    

                    Debug.Print(e.Message);
                    //Debug.Print(e.StackTrace);

                    if (e.InnerException.Message.Contains("(500) Internal Server Error"))
                    {
                        Debug.Print("");
                    }
                }


            }

            Log.Information("Downloading complete.");

            CalculateAndInsertMD5();
            Log.Information("Total images rejected {0}", rejects);
            //Debug.Print("Total images rejected {0}", rejects);
        }

        private void DownloadFiles(List<BuzzBuzz_Developments_UnitPlans> imgs)
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
                    img.Error = e.Message;
                    img.MD5Hash = EMPTY_MD5_HASH;
                    Debug.Print(e.Message);
                    Debug.Print(e.StackTrace);
                }

            });

        }

        private Task DownloadFile(BuzzBuzz_Developments_UnitPlans img)
        {

            //using (WebClient wc = new WebClient())
            using (DwnldWebClient wc = new DwnldWebClient())
            {

                string fullImagePath = string.Concat(UNITPLAN_IMG_S3_PATH + "/" + img.PlanImage);
                Uri uri = new Uri(fullImagePath);

                img.FormattedName =  img.PlanImage.Length < 150 ? string.Format("{0}_{1}", img.PlanID, img.PlanImage) 
                    : string.Format("{0}_{1:yyyy_MM_dd_hh_mm_ss_fff}", img.PlanID.ToString(), DateTime.Now);
               
                string downloadToDirectory = string.Concat(DownloadDirectory + "/" + img.FormattedName);

                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = 2000;
                request.Method = "HEAD";
                request.UserAgent = userAgent;

                try
                {

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        //if (response.StatusCode == HttpStatusCode.OK && response.ContentType.Contains("image") || img.ImageFile.EndsWith(".ashx"))
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            /* To set up each task with cancellation token
                             * so that errored out tasks can be cancelled
                             */
                            /*
                            CancellationTokenSource tokenSource = new CancellationTokenSource();
                            CancellationToken token = tokenSource.Token;

                            return Task.Factory.StartNew(() => wc.DownloadFileTaskAsync(uri, downloadToDirectory), token);
                            */
                            return wc.DownloadFileTaskAsync(uri, downloadToDirectory);

                        }
                        else
                        {
                            img.Error = INVALID_TYPE_ERROR;
                            img.MD5Hash = INVALID_TYPE_ERROR;
                            rejects++;
                            //img.hash = EMPTY_MD5_HASH;
                            return null;
                        }
                    }
                }
                catch (System.Net.WebException ex)
                {
                    img.Error = ex.Message;
                    img.MD5Hash = EMPTY_MD5_HASH;
                    Debug.Print(ex.Message);
                    Debug.Print(ex.StackTrace);
                    rejects++;
                    //return null;
                }
                catch (Exception ex)
                {
                    img.Error = ex.Message;
                    img.MD5Hash = EMPTY_MD5_HASH;
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
            Log.Information("Calculating MD5 hash for images.");

            Stopwatch watch = new Stopwatch();
            watch.Start();

            Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
            {
                string pathOnDisk = DownloadDirectory + Path.DirectorySeparatorChar + img.FormattedName;

                if (!string.IsNullOrWhiteSpace(pathOnDisk) && (img.MD5Hash != EMPTY_MD5_HASH && img.MD5Hash != INVALID_TYPE_ERROR))
                {
                    FileInfo fileInfo = new FileInfo(pathOnDisk);
                    using (var fStream = fileInfo.OpenRead())
                    {
                        if (fileInfo.Length > 0)
                        {
                            var md5 = MD5.Create();
                            img.MD5Hash = BitConverter.ToString(md5.ComputeHash(fStream)).Replace("-", string.Empty);
                        }
                    }
                }

            });

            Log.Information("Inserting hashes in DB.");

            InsertHashes(imgs);

            watch.Stop();

            Log.Information("Hashes inserted in {0} mins", ((float)watch.ElapsedMilliseconds / (float)60000));
        }

        public void InsertHashes(IEnumerable<BuzzBuzz_Developments_UnitPlans> imgs)
        {
            try
            {

                DataTable table = new DataTable();
                table.Columns.Add("ID", typeof(int));
                table.Columns.Add("DevelopmentID", typeof(Guid));
                table.Columns.Add("UnitID", typeof(Guid));
                table.Columns.Add("PlanID", typeof(Guid));
                table.Columns.Add("ImageURL", typeof(string));
                table.Columns.Add("MD5Hash", typeof(string));
                table.Columns.Add("Error", typeof(string));

                foreach (var img in imgs)
                {
                    DataRow row = table.NewRow();
                    row["DevelopmentID"] = img.DevelopmentID;
                    row["UnitID"] = img.UnitID;
                    row["PlanID"] = img.PlanID;
                    row["ImageURL"] = img.PlanImage;
                    row["MD5Hash"] = img.MD5Hash;
                    row["Error"] = img.Error;

                    table.Rows.Add(row);

                }

                using (var sqlBulk = new SqlBulkCopy(System.Configuration.ConfigurationManager.ConnectionStrings["BBHDB"].ConnectionString))
                {

                    sqlBulk.DestinationTableName = "Hashes_UnitsPlans_Images";
                    sqlBulk.WriteToServer(table);
                }

                table.Dispose();
            }
            catch (Exception e)
            {
                Debug.Print(e.StackTrace);
            }
        }
    }
}
