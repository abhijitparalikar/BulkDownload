using Serilog;
using System.Diagnostics;

namespace BulkMD5
{
    class Program
    {
        static void Main(string[] args)
        {
            string logFileType = "log-debug.log";

#if DEBUG
            logFileType = "log-debug.log";
#else
            logFileType = "log-release.log";
#endif

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(string.Format(@"C:\temp\md5\{0}",logFileType), rollingInterval: RollingInterval.Day)
            .CreateLogger();
            /*
            Stopwatch watch = new Stopwatch();

            ParallelDownload pd = new ParallelDownload();
            watch.Start();
            pd.GetImgs();
            
            watch.Stop();

            Log.Information("Images processed in {0} mins", ((float)watch.ElapsedMilliseconds / (float)60000));
            */

            Stopwatch watch = new Stopwatch();

            HashBBHDevImages hashbbh = new HashBBHDevImages();
            watch.Start();
            hashbbh.GetImgs();

            watch.Stop();


            //Stopwatch watch = new Stopwatch();

            //HashUnitPlanImages hashUnitPlans = new HashUnitPlanImages();
            //watch.Start();
            //hashUnitPlans.GetImgs();

            //watch.Stop();
        }

    }
}
