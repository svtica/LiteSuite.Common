// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    GitHubRelease.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: The part of a GitHub release (REST API, releases/latest) that the
//          self-update needs. Read with DataContractJsonSerializer, so that the
//          same code works on .NET Framework 4.8 (no System.Text.Json there).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LiteSuite
{
    [DataContract]
    public sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")] public string TagName;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "body")] public string Body;
        [DataMember(Name = "html_url")] public string HtmlUrl;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;
        [DataMember(Name = "assets")] public List<GitHubReleaseAsset> Assets;

        /// <summary>The release name, or its tag when it has none.</summary>
        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(Name) ? TagName : Name; }
        }
    }
}
