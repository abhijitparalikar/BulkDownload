using Dapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BulkMD5
{
    class AsyncDownload
    {
        [ThreadStatic] List<Task> tasks = new List<Task>();
        [ThreadStatic] List<ImageDetails> imgs = new List<ImageDetails>();

        int DOWNLOAD_BATCH_SIZE = int.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("DOWNLOAD_BATCH_SIZE"));
        public void GetImgs()
        {
           
            try
            {
                using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
                {
                    connection.Open();

                    string query = @"select top 10000 i.id, i.imageurl from images i
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
                    Task.WaitAll(tasks.ToArray());
                    InsertHashes(imgSubSet);
                }

            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        private void DownloadFiles(List<ImageDetails> imgs)
        {

            try
            {

                GetEligibleWorkingURLs(imgs);

                //ServicePointManager.DefaultConnectionLimit = int.MaxValue;
                Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (img) =>
                {
                    if (img.isValid)
                    {
                        var task = DownloadFile(img);

                        if (task != null)
                        {
                            tasks.Add(task);
                        }
                    }
                    
                });
                //foreach (var img in imgs)
                //{
                //    var task = DownloadFile(img);
                //    tasks.Add(task);
                //}
                //Task.WaitAll(tasks.ToArray());
            }
            catch (Exception eex)
            {
                Debug.Print(eex.StackTrace);
            }

        }

        private void GetEligibleWorkingURLs(List<ImageDetails> imgs)
        {
           
            try
            {
                Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 1 }, (img) =>
                {
                    try
                    {
                        var request = (HttpWebRequest)WebRequest.Create(img.ImageUrl);
                        request.Timeout = 1000;
                        request.Method = "HEAD";

                        using (var response = (HttpWebResponse)request.GetResponse())
                        {
                            if (response.StatusCode == HttpStatusCode.OK && response.ContentType.Contains("image"))
                            {
                                img.isValid = true;
                                
                            }
                        }
                    }
                    catch (WebException we)
                    {
                        Debug.Print(we.StackTrace);
                    }
                });
            }
            catch (Exception eeeee)
            {
                Debug.Print(eeeee.StackTrace);
                throw;
            }

            
        }

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

                    wc.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                    {
                    //Debug.Print(e.ProgressPercentage + "% downloaded.");
                        
                    };


                    try
                    {
                        wc.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) =>
                                {
                                    WebClient _wc = (WebClient)sender;

                                    var md5 = MD5.Create();
                                    try
                                    {
                                        string hash = BitConverter.ToString(md5.ComputeHash(_wc.OpenRead(img.pathOnDisk))).Replace("-", string.Empty);
                                        img.hash = hash;
                                    }
                                    catch (Exception ex)
                                    {
                                        img.hash = "00000000000000000000000000000000";
                                    }


                                //Debug.Print("{0} was downloaded.", img.ImageUrl);
                                // TODO: Signal this "Task" is done

                            };
                    }
                    catch (Exception eex)
                    {

                        img.hash = "00000000000000000000000000000000";
                    }


                    wc.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
                    {
                        WebClient _wc = (WebClient)sender;

                        var md5 = MD5.Create();

                        try
                        {
                            string hash = BitConverter.ToString(md5.ComputeHash(_wc.OpenRead(uri))).Replace("-", string.Empty);
                            img.hash = hash;
                        }
                        catch (Exception ex)
                        {
                            img.hash = "00000000000000000000000000000000";
                        }
                    //Debug.Print("{0} was downloaded.", img.ImageUrl);
                    // TODO: Signal this "Task" is done

                };

                    return wc.DownloadFileTaskAsync(uri, downloadToDirectory);
                    //return wc.DownloadDataTaskAsync(img.ImageUrl);
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
