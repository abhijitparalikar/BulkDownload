using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using xxHashSharp;

namespace BulkMD5
{
    class XXHashing
    {
        public static void ComputeXXHash()
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
                            MemoryStream memStream;

                            using (var sr = request.GetResponse().GetResponseStream())
                            {
                               memStream = new MemoryStream();

                                byte[] buffer = new byte[1024];
                                int byteCount;
                                do
                                {
                                    byteCount = sr.Read(buffer, 0, buffer.Length);
                                    memStream.Write(buffer, 0, byteCount);
                                } while (byteCount > 0);

                            }
                            memStream.Seek(0, SeekOrigin.Begin);

                            i++;
                            xxHash.CalculateHash(memStream.ToArray());

                            //Debug.Print("{0} : {1}", i, xxHash.CalculateHash(memStream.ToArray()));


                        }
                        catch (Exception ex)
                        {
                            //Debug.Print(i++ + ": " + ex.Message + " : "+ img.ImageUrl);                            
                            i++;
                        }
                    });

                    watch.Stop();
                    Debug.Print("Computed hash for {0} img urls in {1} ms : ", imgs.Count, watch.ElapsedMilliseconds);

                    watch.Reset();
                    watch.Start();

                    Debug.Print("Inserting records");

                    //InsertHashes(imgs);
                    //watch.Stop();

                    //Debug.Print("Total time taken to insert {0} : ", watch.ElapsedMilliseconds);
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
