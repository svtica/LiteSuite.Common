// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateDownloadPolicy.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Which release files the self-update may download: a release asset
//          of the expected repository on github.com, over HTTPS, whose name
//          matches the package name of the tool. Redirects (GitHub serves the
//          files from another host) must stay on HTTPS.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LiteSuite
{
    public static class UpdateDownloadPolicy
    {
        /// <summary>Largest release package accepted (bytes).</summary>
        public const long MaxPackageBytes = 512L * 1024 * 1024;

        /// <summary>The GitHub API address of the latest release of owner/repo.</summary>
        public static string LatestReleaseApiUrl(string owner, string repo)
        {
            if (!IsValidName(owner) || !IsValidName(repo)) throw new ArgumentException("Invalid GitHub owner or repository name.");
            return "https://api.github.com/repos/" + owner + "/" + repo + "/releases/latest";
        }

        /// <summary>A GitHub owner or repository name: letters, digits, '.', '-', '_'.</summary>
        public static bool IsValidName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.Length <= 100 && name != "." && name != ".."
                && name.All(c => (c < 128 && char.IsLetterOrDigit(c)) || c == '.' || c == '-' || c == '_');
        }

        /// <summary>
        /// True for https://github.com/{owner}/{repo}/releases/download/... (no user info, default port).
        /// </summary>
        public static bool IsSafeDownloadUrl(string url, string owner, string repo)
        {
            Uri uri;
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return false;
            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            string prefix = "/" + owner + "/" + repo + "/releases/download/";
            return IsValidName(owner) && IsValidName(repo)
                && uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Length > prefix.Length
                && !uri.AbsolutePath.Contains("/../");
        }

        /// <summary>Where a download ended after redirects: only HTTPS is accepted.</summary>
        public static bool IsSafeFinalUrl(Uri uri)
        {
            return uri != null && uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// The release package: first asset whose name matches <paramref name="namePattern"/>
        /// (e.g. ^LiteDeploy.*\.zip$), whose link is safe and whose size is accepted. An asset named
        /// exactly <paramref name="preferredName"/> wins. Null when there is none.
        /// </summary>
        public static GitHubReleaseAsset FindPackage(GitHubRelease release, string owner, string repo, string namePattern, string preferredName)
        {
            if (release == null || release.Assets == null) return null;
            Regex pattern = new Regex(namePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            List<GitHubReleaseAsset> candidates = release.Assets
                .Where(a => a != null && !string.IsNullOrEmpty(a.Name) && pattern.IsMatch(a.Name)
                    && a.Size >= 0 && a.Size <= MaxPackageBytes
                    && IsSafeDownloadUrl(a.BrowserDownloadUrl, owner, repo))
                .ToList();
            GitHubReleaseAsset preferred = preferredName == null ? null
                : candidates.FirstOrDefault(a => string.Equals(a.Name, preferredName, StringComparison.OrdinalIgnoreCase));
            return preferred ?? candidates.FirstOrDefault();
        }
    }
}
