using System;
using System.Configuration;
using System.IO;
using log4net;

namespace Zenviro.Ninja
{
    static class AppConfig
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Monitor));

        public static string DataDir
        {
            get
            {
                var dataDir = ConfigurationManager.AppSettings.Get("DataDir");
                if (string.IsNullOrWhiteSpace(dataDir))
                {
                    dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "zenviro.ninja", "data");
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    config.AppSettings.Settings["DataDir"].Value = dataDir;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    Log.Info(string.Format("DataDir set to: {0} in configuration.", dataDir));
                }
                return dataDir;
            }
        }

        public static string GitRemote
        {
            get
            {
                var gitRemote = ConfigurationManager.AppSettings.Get("GitRemote");
                return gitRemote;
            }
        }

        public static string GitConfigName
        {
            get { return ConfigurationManager.AppSettings.Get("GitConfigName"); }
        }

        public static string GitConfigEmail
        {
            get { return ConfigurationManager.AppSettings.Get("GitConfigEmail"); }
        }

        public static void InitDataDir()
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
            if (!File.Exists(Path.Combine(DataDir, ".git", "HEAD")))
                Git.Instance.Clone();
            else
                Git.Instance.Pull(); //todo: if this fails, bot should possibly delete datadir and clone again...
            Discovery.DiscoverServices();
            Discovery.DiscoverSites();
            Discovery.DiscoverApps();
            Git.Instance.AddChanges();
        }
    }
}
