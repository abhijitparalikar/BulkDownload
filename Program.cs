using System.Diagnostics;

namespace BulkMD5
{
    class Program
    {
        static void Main(string[] args)
        {
            //MD5Hash.ComputeMD5();   
            
            Stopwatch watch = new Stopwatch();

            ParallelDownload pd = new ParallelDownload();
            watch.Start();
            pd.GetImgs();
            watch.Stop();
            
            Debug.Print("Downloaded images in {0} mins", (watch.ElapsedMilliseconds/60000));

            /*
            AsyncDownload ad = new AsyncDownload();
            
            watch.Start();
            ad.GetImgs();
            
            watch.Stop();
            
            Debug.Print("Downloaded images in {0} mins", (watch.ElapsedMilliseconds/60000));
            */
        }

    }
}
