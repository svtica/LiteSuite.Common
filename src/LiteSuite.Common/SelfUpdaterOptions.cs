// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    SelfUpdaterOptions.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: The tool that SelfUpdater updates: its GitHub repository, package,
//          executable, publisher, folders and the files of the user to keep.
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace LiteSuite
{
    public sealed class SelfUpdaterOptions
    {
        /// <summary>SHA-1 thumbprint of the LiteSuite publisher certificate (res\certificate.cer of the tools).</summary>
        public const string LiteSuiteThumbprint = "D21B5D39EA4C7BDC69C94C86FC4602E8A1CA15B4";

        /// <summary>Name shown in the script and its log (e.g. "LiteDeploy").</summary>
        public string ApplicationName;
        /// <summary>GitHub owner and repository of the releases.</summary>
        public string Owner = "svtica";
        public string Repository;
        /// <summary>Asset name pattern (regular expression, case ignored), e.g. ^LiteDeploy.*\.zip$.</summary>
        public string PackagePattern;
        /// <summary>Asset name preferred when several match (e.g. "LiteDeploy.zip").</summary>
        public string PackageName;
        /// <summary>Executable of the tool in the package and in the installation folder (e.g. "LiteDeploy.exe").</summary>
        public string ExecutableName;
        /// <summary>Version of the running tool (e.g. "1.0.12.0").</summary>
        public string CurrentVersion;
        /// <summary>Path of the running executable: its signer is accepted too, and its services are found from it.</summary>
        public string CurrentExecutablePath;
        /// <summary>Installation folder (default: the folder of CurrentExecutablePath).</summary>
        public string ApplicationDirectory;
        /// <summary>Pinned publisher thumbprint (default: LiteSuite).</summary>
        public string PinnedThumbprint = LiteSuiteThumbprint;
        /// <summary>User-Agent of the GitHub requests (default: ApplicationName + "-Updater").</summary>
        public string UserAgent;
        /// <summary>Files of the user kept when they exist (see UpdateScriptOptions.PreservePatterns).</summary>
        public List<string> PreservePatterns = new List<string>();
        /// <summary>Arguments of the restarted tool.</summary>
        public string RestartArguments = "";
        /// <summary>Parent of the work folders (default: %TEMP%).</summary>
        public string TempDirectory;
    }
}
