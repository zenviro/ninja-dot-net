using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using log4net;
using NodaTime;
using Zenviro.Model;

namespace Zenviro.Ninja
{
    public static class AssemblyInformation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AssemblyInformation));
        public static AssemblyModel GetAssemblyModel(string assemblyPath)
        {
            FileVersionInfo versionInfo;
            Version version;
            try
            {
                versionInfo = GetFileVersionInfo(assemblyPath);
                version = GetVersion(assemblyPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                versionInfo = null;
                version = null;
            }
            if (versionInfo == null || version == null) return null;
            var name = Path.GetFileNameWithoutExtension(assemblyPath);
            Log.Debug(string.Format("Processing assembly: {0}, from: {1}", name, Path.GetDirectoryName(assemblyPath)));
            return new AssemblyModel
            {
                Name = name,
                ProductName = versionInfo.ProductName,
                CompanyName = versionInfo.CompanyName,
                IsDebug = versionInfo.IsDebug,
                IsPreRelease = versionInfo.IsPreRelease,
                Path = assemblyPath,
                Version = new VersionModel
                {
                    AssemblyVersion = version,
                    CompileDate = GetCompileDate(assemblyPath),
                    FileVersion = versionInfo.FileVersion,
                    ProductVersion = versionInfo.ProductVersion
                }
            };
        }

        /// <summary>
        /// Gets the version of a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public static Version Version(this Assembly assembly)
        {
            return assembly.GetName().Version;
        }

        public static Version GetVersion(string assemblyLocation)
        {
            return AssemblyName.GetAssemblyName(assemblyLocation).Version;
        }

        /// <summary>
        /// Gets the file version of a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public static string FileVersion(this Assembly assembly)
        {
            return GetFileVersion(assembly.Location);
        }

        public static string GetFileVersion(string assemblyLocation)
        {
            return GetFileVersionInfo(assemblyLocation).FileVersion;
        }

        /// <summary>
        /// Gets the informational version of a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public static string InformationalVersion(this Assembly assembly)
        {
            return GetInformationalVersion(assembly.Location);
        }

        public static string GetInformationalVersion(string assemblyLocation)
        {
            return GetFileVersionInfo(assemblyLocation).ProductVersion;
        }

        public static FileVersionInfo GetFileVersionInfo(string assemblyLocation)
        {
            return FileVersionInfo.GetVersionInfo(assemblyLocation);
        }

        /// <summary>
        /// Gets the compile date of a given assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public static ZonedDateTime CompileDate(this Assembly assembly)
        {
            return GetCompileDate(assembly.Location);
        }

        public static ZonedDateTime GetCompileDate(string assemblyLocation)
        {
            return RetrieveLinkerTimestamp(assemblyLocation);
        }

        /// <summary>
        /// Retrieves the linker timestamp.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        /// <remarks>http://www.codinghorror.com/blog/2005/04/determining-build-date-the-hard-way.html</remarks>
        private static ZonedDateTime RetrieveLinkerTimestamp(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var b = new byte[2048];
            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                fileStream.Read(b, 0, 2048);
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Close();
            }
            var secondsSince1970 = BitConverter.ToInt32(b, BitConverter.ToInt32(b, peHeaderOffset) + linkerTimestampOffset);
            return new LocalDateTime(1970, 1, 1, 0, 0, 0).PlusSeconds(secondsSince1970).InUtc();
        }
    }
}
