using Serilog;
using System.Diagnostics;

namespace BulkMD5
{
    class Program
    {
        static void Main(string[] args)
        {
            string logFile = "log-debug.log";

#if DEBUG
            logFileType = "log-debug.log";
#else
            logFile = "log-release.log";
#endif

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(string.Format(@"C:\temp\md5\{0}",logFile), rollingInterval: RollingInterval.Day)
            .CreateLogger();
            
            Stopwatch watch = new Stopwatch();
            /*
            ParallelDownload pd = new ParallelDownload();
            watch.Start();
            pd.GetImgs();
            
            watch.Stop();

            Log.Information("Images processed in {0} mins", ((float)watch.ElapsedMilliseconds / (float)60000));
            */



            //HashBBHDevImages hashbbh = new HashBBHDevImages();
            //watch.Start();
            //hashbbh.GetImgs();

            //watch.Stop();

            HashUnitPlanImages hashunitplans = new HashUnitPlanImages();

            watch.Start();

            hashunitplans.GetImgs();

            watch.Stop();
        }

    }
}
