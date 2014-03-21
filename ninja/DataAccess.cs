using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Zenviro.Model;

namespace Zenviro.Ninja
{
    public static class DataAccess
    {
        public static IEnumerable<SearchPathModel> GetPaths(HostModel host = null)
        {
            var filter = "*.json";
            if (host != null)
                filter = string.Format("{0}.*.json", host);
            return Directory.GetFiles(Path.Combine(AppConfig.DataDir, "config", "path"), filter, SearchOption.AllDirectories)
                .Where(x => !Path.GetFileName(x).StartsWith("dummy"))
                .Select(x => JsonConvert.DeserializeObject<SearchPathModel>(File.ReadAllText(x)));
        }

        public static IEnumerable<HostModel> GetHosts()
        {
            return GetPaths().Select(x => x.Host).Distinct();
        }

        public static IEnumerable<WebsiteModel> GetSites(HostModel host = null)
        {
            var filter = "*.json";
            if (host != null)
                filter = string.Format("{0}.*.json", host);
            return Directory.GetFiles(Path.Combine(AppConfig.DataDir, "infrastructure", "site"), filter, SearchOption.AllDirectories)
                .Select(hostFile => JsonConvert.DeserializeObject<WebsiteModel>(File.ReadAllText(hostFile)));
        }

        public static IEnumerable<WindowsServiceModel> GetServices(HostModel host = null)
        {
            var filter = "*.json";
            if (host != null)
                filter = string.Format("{0}.json", host);
            return Directory.GetFiles(Path.Combine(AppConfig.DataDir, "infrastructure", "service"), filter, SearchOption.AllDirectories)
                .SelectMany(hostFile => JsonConvert.DeserializeObject<IEnumerable<WindowsServiceModel>>(File.ReadAllText(hostFile)));
        }

        public static IEnumerable<string> GetPrefixes()
        {
            return JsonConvert.DeserializeObject<IEnumerable<string>>(File.ReadAllText(Path.Combine(AppConfig.DataDir, "config", "default", "assembly.startswith.json")));
        } 
    }
}
