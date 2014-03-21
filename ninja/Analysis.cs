using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using log4net;
using Zenviro.Model;

namespace Zenviro.Ninja
{
    public static class Analysis
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Analysis));

        internal static string GetWebAssemblyPath(string applicationPath)
        {
            var globalAsaxPath = Path.Combine(applicationPath, "Global.asax");
            if (File.Exists(globalAsaxPath))
            {
                try
                {
                    var inherits = new Regex(" Inherits=\"([^\"]*)\" ").Matches(File.ReadAllText(globalAsaxPath))[0].Groups[1].Value;
                    return Path.Combine(applicationPath, "bin", inherits.Replace(inherits.Split('.').Last(), "dll"));
                }
                catch
                {
                    return null;
                }
            }
            if (Directory.Exists(Path.Combine(applicationPath, "bin")))
            {
                try
                {
                    var candidates = new[] { "api.dll", "web.dll", "ui.dll" };
                    return Directory.GetFiles(Path.Combine(applicationPath, "bin"), string.Concat(applicationPath.Split(Path.DirectorySeparatorChar).Last(), "*.dll")).First(x => candidates.Any(x.ToLower().EndsWith));
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        internal static string GetSvcAssemblyPath(string applicationPath)
        {
            try
            {
                var candidates = new[] { ".exe.config", ".dll.config" };
                var config = Directory.GetFiles(applicationPath, "*.config").First(x => candidates.Any(x.ToLower().EndsWith));
                return config.Remove(config.Length - 7);
            }
            catch
            {
                return null;
            }
        }

        internal static IEnumerable<AssemblyModel> GetDependencies(string assemblyPath, IEnumerable<string> filePrefixes = null)
        {
            if(filePrefixes == null)
                filePrefixes = DataAccess.GetPrefixes();
            var folder = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                return Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories)
                    .Where(x => !x.Equals(assemblyPath, StringComparison.InvariantCultureIgnoreCase))
                    .Where(x => filePrefixes.Any(fp => Path.GetFileName(x).StartsWith(fp, StringComparison.InvariantCultureIgnoreCase)))
                    .Select(AssemblyInformation.GetAssemblyModel);
            return null;
        }

        internal static void LinkWebsite(SearchPathModel searchPath, AppModel app, IEnumerable<WebsiteModel> sites = null)
        {
            if (sites == null)
                sites = DataAccess.GetSites(searchPath.Host);
            if (searchPath.Role == "web" && sites.Any())
            {
                var sitePath = Path.GetDirectoryName(app.MainAssembly.Path);
                if (sitePath != null)
                {
                    sitePath = sitePath.Remove(sitePath.Length - 4); //trim "\bin"
                    var t = sitePath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    t.RemoveAt(0);
                    sitePath = string.Join(@"\", t).Replace('$', ':');
                    app.Website = sites.FirstOrDefault(s => s.Host == app.Host && s.Applications.Any(a => a.PhysicalPath.Equals(sitePath, StringComparison.InvariantCultureIgnoreCase)));
                    if (app.Website != null)
                    {
                        var binding = app.Website.Bindings.FirstOrDefault(b => b.Protocol == "http");
                        if (binding != null)
                        {
                            var bi = binding.BindingInformation.Split(':');
                            var hostHeader = string.IsNullOrWhiteSpace(bi[2]) || bi[2] == "*" || bi[2] == "localhost"
                                ? app.Host.ToString()
                                : bi[2];
                            app.Url = string.Format("{0}://{1}:{2}", binding.Protocol, hostHeader, bi[1]);
                            app.Url +=
                                app.Website.Applications.First(
                                    a => a.PhysicalPath.Equals(sitePath, StringComparison.InvariantCultureIgnoreCase)).Path;
                        }
                        Log.Debug(string.Format("Application: {0}, linked to IIS Website: {1}.", app.Name, app.Url));
                    }
                }
            }
        }

        internal static void LinkWindowsService(SearchPathModel searchPath, AppModel app, IEnumerable<WindowsServiceModel> services = null)
        {
            if (services == null)
                services = DataAccess.GetServices(searchPath.Host);
            if (searchPath.Role == "svc" && services.Any())
            {
                var servicePath = Path.GetDirectoryName(app.MainAssembly.Path);
                if (servicePath != null)
                {
                    var t = servicePath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    t.RemoveAt(0);
                    servicePath = string.Join(@"\", t).Replace('$', ':');
                    app.WindowsService = services.FirstOrDefault(s => s.Host == app.Host && s.Path.Contains(servicePath, StringComparison.InvariantCultureIgnoreCase));
                }
            }
            if (app.WindowsService != null)
                Log.Debug(string.Format("Application: {0}, linked to Windows Service: {1}/{2}.", app.Name, app.WindowsService.Host, app.WindowsService.Name));
        }

        internal static void LinkDatabaseConnections(AppModel app)
        {
            var configSearchPath = app.Role == "web"
                ? Path.GetDirectoryName(Path.GetDirectoryName(app.MainAssembly.Path))
                : Path.GetDirectoryName(app.MainAssembly.Path);
            app.DatabaseConnections = GetDatabaseConnections(configSearchPath);
        }

        internal static void LinkEndpointConnections(AppModel app)
        {
            var configSearchPath = app.Role == "web"
                ? Path.GetDirectoryName(Path.GetDirectoryName(app.MainAssembly.Path))
                : Path.GetDirectoryName(app.MainAssembly.Path);
            app.EndpointConnections = GetEndpointConnections(configSearchPath);
        }

        private static List<DatabaseConnectionModel> GetDatabaseConnections(string applicationPath)
        {
            var connections = new List<DatabaseConnectionModel>();
            const string expression = "connectionstring(\" value)?(\\s)?=(\\s)?\"([^\"]*)";
            var configs = Directory.GetFiles(applicationPath, "*.config", SearchOption.AllDirectories);
            foreach (var config in configs)
            {
                var matches = new Regex(expression, RegexOptions.IgnoreCase).Matches(File.ReadAllText(config));
                foreach (Match match in matches)
                {
                    var connectionString = match.Groups[4].Captures[0].Value;
                    try
                    {
                        connections.Add(GetDatabaseConnection(connectionString));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(string.Format("Failed to parse connection string: '{0}' from config: {1}", connectionString, config));
                        Log.Error(ex);
                        connections.Add(new DatabaseConnectionModel { ConnectionString = connectionString });
                    }
                }
            }
            return connections;
        }

        private static DatabaseConnectionModel GetDatabaseConnection(string connectionString)
        {
            // Handle RavenDB connection strings
            if (connectionString.StartsWith("url", StringComparison.InvariantCultureIgnoreCase))
            {
                var dict = connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => new KeyValuePair<string, string>(x.Split('=').First().ToLowerInvariant().Trim(), x.Split('=').Last().Trim())).ToDictionary(x => x.Key, x => x.Value);
                return new DatabaseConnectionModel
                {
                    Provider = "RavenDB",
                    ConnectionString = connectionString,
                    Database = dict["database"],
                    Instance = dict["url"],
                    Host = new HostModel(dict["url"])
                };
            }

            // Handle MSLDAP connection strings
            if (connectionString.StartsWith("msldap://", StringComparison.InvariantCultureIgnoreCase))
            {
                var url = connectionString.Substring(0, connectionString.LastIndexOf('/')).ToLowerInvariant().Replace("msldap", "http");
                var dict = connectionString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Last()
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => new KeyValuePair<string, string>(x.Split('=').First().ToLowerInvariant(), x.Split('=').Last())).ToDictionary(x => x.Key, x => x.Value);
                return new DatabaseConnectionModel
                {
                    Provider = "LDAP",
                    ConnectionString = connectionString,
                    Database = dict["ou"],
                    Instance = dict["cn"],
                    Host = new HostModel(url)
                };
            }

            // Handle MSSql connection strings
            var b = new SqlConnectionStringBuilder(connectionString);
            string host = null;
            string domain = null;
            string instance = null;
            int? port = null;
            if (b.DataSource.Contains(","))
            {
                port = Convert.ToInt32(b.DataSource.Split(',').Last());
                host = b.DataSource.Split(',').First().ToLowerInvariant();
            }
            if (b.DataSource.Contains(@"\"))
            {
                host = b.DataSource.Split('\\').First().ToLowerInvariant();
                instance = b.DataSource.Split('\\').Last().Split(',').First().ToLowerInvariant();
            }
            if (string.IsNullOrWhiteSpace(host))
            {
                host = b.DataSource.ToLowerInvariant();
            }
            if (host.Contains('.'))
            {
                domain = host.Replace(string.Concat(host.Split('.').First(), '.'), string.Empty);
                host = host.Split('.').First();
            }
            return new DatabaseConnectionModel
            {
                Provider = "MSSQL",
                ConnectionString = b.ConnectionString,
                Database = b.InitialCatalog,
                Username = b.UserID,
                Host = new HostModel(host),
                Port = port,
                Instance = instance
            };
        }

        private static List<EndpointConnectionModel> GetEndpointConnections(string applicationPath)
        {
            var connections = new List<EndpointConnectionModel>();
            var configs = Directory.GetFiles(applicationPath, "*.config", SearchOption.AllDirectories);
            foreach (var config in configs)
            {
                try
                {
                    var endpoints = XDocument.Load(config).GetEndpointConnections(config).ToList();
                    if (endpoints.Any())
                        connections.AddRange(endpoints);
                }
                catch (Exception ex)
                {
                    Log.Warn(string.Format("Failed to retrieve endpoints from config: {0}", config));
                    Log.Error(ex);
                }
            }
            return connections;
        }

        internal static IEnumerable<WebsiteApplicationPoolModel> GetApplicationPools(this XContainer applicationHostConfig)
        {
            return applicationHostConfig.Descendants("applicationPools")
                .First()
                .Elements("add")
                .Select(p => new WebsiteApplicationPoolModel
                {
                    Name = p.Attribute("name").Value,
                    RuntimeVersion =
                        p.Attributes().Any(x => x.Name == "managedRuntimeVersion")
                            ? p.Attribute("managedRuntimeVersion").Value
                            : null,
                    PipelineMode =
                        p.Attributes().Any(x => x.Name == "managedPipelineMode")
                            ? p.Attribute("managedPipelineMode").Value
                            : null,
                    Username =
                        p.HasElements && p.Elements().Any(x => x.Name == "processModel") &&
                        p.Element("processModel").Attributes().Any(x => x.Name == "userName")
                            ? p.Element("processModel").Attribute("userName").Value
                            : null,
                });
        }

        internal static IEnumerable<WebsiteModel> GetWebsites(this XContainer applicationHostConfig, HostModel host)
        {
            return applicationHostConfig.Descendants("site").Select(s => new WebsiteModel
            {
                Host = host,
                Id = Convert.ToInt32(s.Attribute("id").Value),
                Name = s.Attribute("name").Value,
                Applications = s.Elements("application").Select(a => new WebsiteApplicationModel
                {
                    Path = a.Attribute("path").Value,
                    PhysicalPath = a.Element("virtualDirectory").Attribute("physicalPath").Value,
                    ApplicationPool = a.Attribute("applicationPool").Value
                }).ToList(),
                Bindings = s.Descendants("binding").Select(b => new WebsiteBindingModel
                {
                    Protocol = b.Attribute("protocol").Value,
                    BindingInformation = b.Attribute("bindingInformation").Value
                }).ToList()
            });
        }

        private static IEnumerable<EndpointConnectionModel> GetEndpointConnections(this XContainer appConfig, string configPath)
        {
            return appConfig.Descendants("endpoint").Select(x => GetEndpointConnection(x, configPath)).Where(x => x != null);
        }

        private static EndpointConnectionModel GetEndpointConnection(this XElement element, string configPath)
        {
            try
            {
                return new EndpointConnectionModel
                {
                    Address = element.Attribute("address").Value,
                    Host = new HostModel(element.Attribute("address").Value),
                    Username = element.HasElements && element.Elements().Any(x => x.Name == "identity")
                               && element.Element("identity").HasElements
                               && element.Element("identity").Elements().Any(x => x.Name == "userPrincipalName")
                               && element.Element("identity")
                                   .Element("userPrincipalName")
                                   .Attributes()
                                   .Any(x => x.Name == "value")
                        ? element.Element("identity").Element("userPrincipalName").Attribute("value").Value
                        : null
                };
            }
            catch (Exception ex)
            {
                Log.Warn(string.Format("Failed to parse endpoint from xelement in config: {0}.", configPath));
                Log.Error(ex);
            }
            return null;
        }
    }
}
