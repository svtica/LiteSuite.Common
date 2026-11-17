// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    GitHubReleaseAsset.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: A file attached to a GitHub release (name, size, download link).
// -----------------------------------------------------------------------------
using System.Runtime.Serialization;

namespace LiteSuite
{
    [DataContract]
    public sealed class GitHubReleaseAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "size")] public long Size;
        [DataMember(Name = "browser_download_url")] public string BrowserDownloadUrl;
    }
}
