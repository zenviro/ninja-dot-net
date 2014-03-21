using System;
using System.ServiceProcess;
using log4net;

namespace Zenviro.Ninja
{
    static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                Console.WriteLine("ninja is running...");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("press any key to stop.");
                Console.ResetColor();

                Fleck.Instance.Init();
                Fleck.Instance.Run();
                AppConfig.InitDataDir();

                Monitor.Instance.Init();
                Monitor.Instance.Run();

                Console.ReadKey();

                Monitor.Instance.Stop();
                Fleck.Instance.Stop();
            }
            else
            {
                try
                {
                    ServiceBase.Run(new NinjaService());
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                }
            }
        }
    }
}
