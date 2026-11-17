// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    GitHubReleaseTests.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Unit tests of GitHubReleaseClient.Parse and UpdateDownloadPolicy
//          (API answer of releases/latest, package choice, accepted links).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LiteSuite.Tests
{
    [TestClass]
    public class GitHubReleaseTests
    {
        const string Json = @"{
  ""url"": ""https://api.github.com/repos/svtica/LiteDeploy/releases/1"",
  ""html_url"": ""https://github.com/svtica/LiteDeploy/releases/tag/v1.0.13.0"",
  ""tag_name"": ""v1.0.13.0"",
  ""name"": ""LiteDeploy 1.0.13.0"",
  ""draft"": false,
  ""prerelease"": false,
  ""body"": ""### Added\r\n- Mise à jour automatique"",
  ""author"": { ""login"": ""someone"", ""id"": 1 },
  ""assets"": [
    { ""name"": ""LiteDeploy-symbols.zip"", ""size"": 10, ""browser_download_url"": ""https://github.com/svtica/LiteDeploy/releases/download/v1.0.13.0/LiteDeploy-symbols.zip"" },
    { ""name"": ""LiteDeploy.zip"", ""size"": 2048, ""browser_download_url"": ""https://github.com/svtica/LiteDeploy/releases/download/v1.0.13.0/LiteDeploy.zip"" },
    { ""name"": ""checksums.txt"", ""size"": 5, ""browser_download_url"": ""https://github.com/svtica/LiteDeploy/releases/download/v1.0.13.0/checksums.txt"" }
  ]
}";

        [TestMethod]
        public void Parse_ReadsTagNameBodyAndAssets()
        {
            GitHubRelease release = GitHubReleaseClient.Parse(Json);
            Assert.AreEqual("v1.0.13.0", release.TagName);
            Assert.AreEqual("LiteDeploy 1.0.13.0", release.DisplayName);
            StringAssert.Contains(release.Body, "Mise à jour");
            Assert.AreEqual(3, release.Assets.Count);
            Assert.AreEqual(2048, release.Assets[1].Size);
        }

        [TestMethod]
        public void Parse_RejectsEmptyOrTaglessAnswers()
        {
            Assert.ThrowsException<InvalidDataException>(() => GitHubReleaseClient.Parse(""));
            Assert.ThrowsException<InvalidDataException>(() => GitHubReleaseClient.Parse("{\"name\":\"x\"}"));
            Assert.AreEqual("v2", GitHubReleaseClient.Parse("{\"tag_name\":\"v2\"}").DisplayName);
        }

        [TestMethod]
        public void FindPackage_PrefersTheExactName()
        {
            GitHubRelease release = GitHubReleaseClient.Parse(Json);
            GitHubReleaseAsset asset = UpdateDownloadPolicy.FindPackage(release, "svtica", "LiteDeploy", @"^LiteDeploy.*\.zip$", "LiteDeploy.zip");
            Assert.AreEqual("LiteDeploy.zip", asset.Name);
            Assert.AreEqual("LiteDeploy-symbols.zip", UpdateDownloadPolicy.FindPackage(release, "svtica", "LiteDeploy", @"^LiteDeploy.*\.zip$", null).Name);
            Assert.IsNull(UpdateDownloadPolicy.FindPackage(release, "svtica", "LiteDeploy", @"^LitePM.*\.zip$", "LitePM.zip"));
        }

        [TestMethod]
        public void FindPackage_IgnoresUnsafeLinksAndOtherRepositories()
        {
            GitHubRelease release = new GitHubRelease
            {
                TagName = "v1",
                Assets = new List<GitHubReleaseAsset>
                {
                    new GitHubReleaseAsset { Name = "LitePM.zip", Size = 1, BrowserDownloadUrl = "http://github.com/svtica/LitePM/releases/download/v1/LitePM.zip" },
                    new GitHubReleaseAsset { Name = "LitePM.zip", Size = 1, BrowserDownloadUrl = "https://example.com/svtica/LitePM/releases/download/v1/LitePM.zip" },
                    new GitHubReleaseAsset { Name = "LitePM.zip", Size = 1, BrowserDownloadUrl = "https://github.com/other/LitePM/releases/download/v1/LitePM.zip" },
                    new GitHubReleaseAsset { Name = "LitePM.zip", Size = UpdateDownloadPolicy.MaxPackageBytes + 1, BrowserDownloadUrl = "https://github.com/svtica/LitePM/releases/download/v1/LitePM.zip" }
                }
            };
            Assert.IsNull(UpdateDownloadPolicy.FindPackage(release, "svtica", "LitePM", @"^LitePM.*\.zip$", "LitePM.zip"));
            Assert.IsNull(UpdateDownloadPolicy.FindPackage(null, "svtica", "LitePM", @"^LitePM.*\.zip$", null));
            Assert.IsNull(UpdateDownloadPolicy.FindPackage(new GitHubRelease { TagName = "v1" }, "svtica", "LitePM", @"^LitePM.*\.zip$", null));
        }

        [TestMethod]
        public void IsSafeDownloadUrl_OnlyReleaseDownloadsOfTheRepositoryOverHttps()
        {
            Assert.IsTrue(UpdateDownloadPolicy.IsSafeDownloadUrl("https://github.com/svtica/LitePM/releases/download/v1.1.3.0/LitePM.zip", "svtica", "LitePM"));
            Assert.IsTrue(UpdateDownloadPolicy.IsSafeDownloadUrl("https://GitHub.com/svtica/litepm/releases/download/v1/LitePM.zip", "svtica", "LitePM"));
            foreach (string url in new[]
            {
                null, "", "not a url",
                "http://github.com/svtica/LitePM/releases/download/v1/LitePM.zip",
                "https://github.com.evil.example/svtica/LitePM/releases/download/v1/LitePM.zip",
                "https://user@github.com/svtica/LitePM/releases/download/v1/LitePM.zip",
                "https://github.com:8443/svtica/LitePM/releases/download/v1/LitePM.zip",
                "https://github.com/svtica/LitePMX/releases/download/v1/LitePM.zip",
                "https://github.com/svtica/LitePM/archive/v1.zip",
                "https://github.com/svtica/LitePM/releases/download/",
                "https://objects.githubusercontent.com/svtica/LitePM/releases/download/v1/LitePM.zip"
            })
                Assert.IsFalse(UpdateDownloadPolicy.IsSafeDownloadUrl(url, "svtica", "LitePM"), url ?? "null");
            Assert.IsFalse(UpdateDownloadPolicy.IsSafeDownloadUrl("https://github.com/svtica/LitePM/releases/download/v1/LitePM.zip", "svtica", "../x"));
        }

        [TestMethod]
        public void IsSafeFinalUrl_RequiresHttps()
        {
            Assert.IsTrue(UpdateDownloadPolicy.IsSafeFinalUrl(new Uri("https://release-assets.githubusercontent.com/x")));
            Assert.IsFalse(UpdateDownloadPolicy.IsSafeFinalUrl(new Uri("http://release-assets.githubusercontent.com/x")));
            Assert.IsFalse(UpdateDownloadPolicy.IsSafeFinalUrl(null));
        }

        [TestMethod]
        public void LatestReleaseApiUrl_ValidatesNames()
        {
            Assert.AreEqual("https://api.github.com/repos/svtica/LiteTask/releases/latest", UpdateDownloadPolicy.LatestReleaseApiUrl("svtica", "LiteTask"));
            Assert.ThrowsException<ArgumentException>(() => UpdateDownloadPolicy.LatestReleaseApiUrl("svtica", "Lite/Task"));
            Assert.ThrowsException<ArgumentException>(() => UpdateDownloadPolicy.LatestReleaseApiUrl("", "LiteTask"));
            Assert.IsFalse(UpdateDownloadPolicy.IsValidName(".."));
        }
    }
}
