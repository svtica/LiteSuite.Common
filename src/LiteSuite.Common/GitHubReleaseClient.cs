// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    GitHubReleaseClient.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Reads the latest release of a GitHub repository and downloads its
//          package (HttpClient, TLS 1.2, system proxy with the Windows
//          credentials of the user). Only the GitHub API and release download
//          links accepted by UpdateDownloadPolicy are contacted.
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteSuite
{
    public sealed class GitHubReleaseClient : IDisposable
    {
        readonly HttpClient _http;

        /// <param name="userAgent">Required by the GitHub API, e.g. "LiteDeploy-Updater".</param>
        public GitHubReleaseClient(string userAgent)
        {
#if NETFRAMEWORK
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
#endif
            HttpClientHandler handler = new HttpClientHandler
            {
                UseProxy = true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(userAgent) ? "LiteSuite-Updater" : userAgent);
        }

        /// <summary>Reads a release from the JSON of the GitHub API.</summary>
        public static GitHubRelease Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Empty answer from the GitHub API.");
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                GitHubRelease release = (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(stream);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                    throw new InvalidDataException("The GitHub API answer has no tag_name.");
                return release;
            }
        }

        /// <summary>The latest (non pre-release) release of owner/repo.</summary>
        public async Task<GitHubRelease> GetLatestAsync(string owner, string repo, CancellationToken cancellation)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, UpdateDownloadPolicy.LatestReleaseApiUrl(owner, repo)))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (HttpResponseMessage response = await _http.SendAsync(request, cancellation).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return Parse(json);
                }
            }
        }

        /// <summary>
        /// Downloads a release asset to <paramref name="filePath"/>. Refuses a link that is not a
        /// release download of owner/repo, a redirect away from HTTPS and a file larger than
        /// UpdateDownloadPolicy.MaxPackageBytes. The file is deleted on failure.
        /// </summary>
        public async Task DownloadAsync(GitHubReleaseAsset asset, string owner, string repo, string filePath, CancellationToken cancellation)
        {
            if (asset == null) throw new ArgumentNullException("asset");
            if (!UpdateDownloadPolicy.IsSafeDownloadUrl(asset.BrowserDownloadUrl, owner, repo))
                throw new UpdateException(UpdateFailure.UnsafeUrl, "Refusing to download from " + asset.BrowserDownloadUrl);
            try
            {
                using (HttpResponseMessage response = await _http.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (!UpdateDownloadPolicy.IsSafeFinalUrl(response.RequestMessage == null ? null : response.RequestMessage.RequestUri))
                        throw new UpdateException(UpdateFailure.UnsafeUrl, "The download was redirected away from HTTPS.");
                    long? length = response.Content.Headers.ContentLength;
                    if (length.HasValue && length.Value > UpdateDownloadPolicy.MaxPackageBytes)
                        throw new UpdateException(UpdateFailure.DownloadFailed, "The package is too large (" + length.Value + " bytes).");

                    using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream target = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
                        {
                            total += read;
                            if (total > UpdateDownloadPolicy.MaxPackageBytes)
                                throw new UpdateException(UpdateFailure.DownloadFailed, "The package is too large.");
                            await target.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch
            {
                try { File.Delete(filePath); } catch (Exception) { }
                throw;
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
