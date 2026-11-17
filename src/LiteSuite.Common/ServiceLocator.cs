// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    ServiceLocator.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Windows services that run an executable (LiteTaskService, the
//          LitePM remote service "LitePM.exe -SERVERSERVICE"...), found from the
//          ImagePath of the services in the registry rather than from a fixed
//          name, so that a service installed under another name is found too.
//          The update script stops them before replacing the files and starts
//          again those that were running.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace LiteSuite
{
    public static class ServiceLocator
    {
        const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

        /// <summary>
        /// The executable of a service command line (ImagePath): quoted path, or an unquoted path
        /// ending with ".exe"; environment variables, "\??\" and "\SystemRoot\" expanded. Null when
        /// there is none.
        /// </summary>
        public static string ExecutableFromImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;
            string value = imagePath.Trim();
            string path;
            if (value.StartsWith("\""))
            {
                int end = value.IndexOf('"', 1);
                if (end < 0) return null;
                path = value.Substring(1, end - 1);
            }
            else
            {
                int exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                path = exe < 0 ? value.Split(' ')[0] : value.Substring(0, exe + 4);
            }
            path = Environment.ExpandEnvironmentVariables(path.Trim());
            if (path.StartsWith(@"\??\")) path = path.Substring(4);
            if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", path.Substring(12));
            return path.Length == 0 ? null : path;
        }

        /// <summary>True when the service command line runs <paramref name="executablePath"/>.</summary>
        public static bool ImagePathRuns(string imagePath, string executablePath)
        {
            string exe = ExecutableFromImagePath(imagePath);
            if (exe == null || string.IsNullOrEmpty(executablePath)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(exe), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Names of the installed services whose ImagePath runs <paramref name="executablePath"/>.</summary>
        public static List<string> FindServices(string executablePath)
        {
            List<string> names = new List<string>();
            try
            {
                using (RegistryKey services = Registry.LocalMachine.OpenSubKey(ServicesKey))
                {
                    if (services == null) return names;
                    foreach (string name in services.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey service = services.OpenSubKey(name))
                            {
                                string imagePath = service == null ? null : service.GetValue("ImagePath") as string;
                                if (ImagePathRuns(imagePath, executablePath)) names.Add(name);
                            }
                        }
                        catch (Exception)
                        {
                            // Unreadable service key: not ours
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return names;
        }

        /// <summary>True when the service exists and is not stopped (running, starting, paused...).</summary>
        public static bool IsActive(string serviceName)
        {
            try
            {
                using (ServiceController controller = new ServiceController(serviceName))
                {
                    return controller.Status != ServiceControllerStatus.Stopped;
                }
            }
            catch (Exception)
            {
                // Not installed or not readable
                return false;
            }
        }
    }
}
