using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BulkMD5
{
    class MD5Hash
    {
        public static void ComputeMD5()
        {
            using (var connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["SSFDB"].ConnectionString))
            {
                try
                {
                    connection.Open();

                    Stopwatch watch = new Stopwatch();
                    watch.Start();

                    string query = @"select top 10000 i.id, i.imageurl from images i
                                    join Feeds f on f.ID = i.FeedID
                                    where f.IsActive = 1
                                    order by i.id desc";

                    List<ImageDetails> imgs = connection.Query<ImageDetails>(query).ToList();
                    List<string> hashes = new List<string>();

                    watch.Stop();
                    Debug.Print("Retrieved {0} img urls in {1} ms : ", imgs.Count, watch.ElapsedMilliseconds);

                    watch.Reset();
                    watch.Start();

                    Debug.Print("Calculating hashes");
                    int i = 0;
                    Parallel.ForEach(imgs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (img) =>
                    {
                        try
                        {

                            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(img.ImageUrl);
                            //request.Timeout = 2000;

                            using (var sr = request.GetResponse().GetResponseStream())
                            {
                                //var md5 = MD5.Create();
                                //string hash = BitConverter.ToString(md5.ComputeHash(sr)).Replace("-", string.Empty);
                                //img.hash = hash;

                                i++;
                                //Debug.Print("{0} : {1} ", hash, i);

                                //if (i % 50000 == 0)
                                //{
                                //    Debug.Print("{0} images processed", i);
                                //}
                            }
                        }
                        catch (Exception ex)
                        {
                            //Debug.Print(i++ + ": " + ex.Message + " : "+ img.ImageUrl);        
                            img.hash = "00000000000000000000000000000000";
                            i++;
                        }
                    });

                    watch.Stop();
                    Debug.Print("Computed hash for {0} img urls in {1} ms : ", imgs.Count, watch.ElapsedMilliseconds);

                    watch.Reset();
                    watch.Start();

                    Debug.Print("Inserting records");

                    InsertHashes(imgs);
                    watch.Stop();

                    Debug.Print("Total time taken to insert {0} : ", watch.ElapsedMilliseconds);
                }
                catch (Exception e)
                {
                    Debug.Print(e.StackTrace);
                }
            }

        }
        public static void InsertHashes(List<ImageDetails> imgs)
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