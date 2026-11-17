// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateCheckResult.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Result of SelfUpdater.CheckAsync: the latest release, its package
//          and whether it is newer than the running version.
// -----------------------------------------------------------------------------
namespace LiteSuite
{
    public sealed class UpdateCheckResult
    {
        public string CurrentVersion;
        /// <summary>Version of the latest release, without "v" (e.g. "1.0.13.0").</summary>
        public string LatestVersion;
        public GitHubRelease Release;
        /// <summary>The package of the tool in the release; null when there is none.</summary>
        public GitHubReleaseAsset Package;

        /// <summary>The latest release is newer and has a package.</summary>
        public bool IsUpdateAvailable
        {
            get { return Package != null && ReleaseVersion.IsNewer(CurrentVersion, LatestVersion); }
        }
    }
}
