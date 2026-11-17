// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateScriptLauncher.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Starts the update script (hidden Windows PowerShell) and decides
//          whether its copy needs elevation: installation folder not writable,
//          or a service of the tool to stop, while the tool is not elevated.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace LiteSuite
{
    public static class UpdateScriptLauncher
    {
        /// <summary>Windows PowerShell 5.1 of the system (powershell.exe of the PATH when absent).</summary>
        public static string PowerShellPath
        {
            get
            {
                string path = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                return File.Exists(path) ? path : "powershell.exe";
            }
        }

        /// <summary>Command line of the script: no profile, execution policy bypassed for this script only, hidden.</summary>
        public static ProcessStartInfo CreateStartInfo(string scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath) || scriptPath.Contains("\"")) throw new ArgumentException("Invalid script path.");
            return new ProcessStartInfo(PowerShellPath, "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath)
            };
        }

        /// <summary>Starts the script. Throws UpdateException(LaunchFailed) on failure.</summary>
        public static void Start(string scriptPath)
        {
            try
            {
                using (Process.Start(CreateStartInfo(scriptPath))) { }
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.LaunchFailed, "The update script could not be started: " + ex.Message, ex);
            }
        }

        /// <summary>True when the current process runs elevated (administrator).</summary>
        public static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True when a file can be created in the folder (e.g. false in Program Files without elevation).</summary>
        public static bool CanWrite(string folder)
        {
            try
            {
                string probe = Path.Combine(folder, ".litesuite-write-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The copy needs elevation: not elevated, and the folder is not writable or a service is active.</summary>
        public static bool NeedsElevation(string applicationDirectory, IEnumerable<string> serviceNames)
        {
            if (IsElevated()) return false;
            if (!CanWrite(applicationDirectory)) return true;
            return (serviceNames ?? Enumerable.Empty<string>()).Any(ServiceLocator.IsActive);
        }
    }
}
