// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    SelfUpdater.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Self-update of a LiteSuite tool from its GitHub releases, without
//          user interface (generalized from LiteTask's UpdateManager):
//            1. CheckAsync:   latest release, its package, newer or not;
//            2. PrepareAsync: download (github.com only), extraction, Mark of
//                             the Web removed, signature of the executable;
//            3. Apply:        writes and starts the replacement script
//                             (services of the tool stopped and restarted,
//                             files of the user kept); the tool then exits.
//          Nothing of the installation changes before step 3. Failures throw
//          UpdateException; the steps are reported through IProgress.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LiteSuite
{
    public sealed class SelfUpdater : IDisposable
    {
        readonly SelfUpdaterOptions _options;
        readonly GitHubReleaseClient _client;

        public SelfUpdater(SelfUpdaterOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (!UpdateDownloadPolicy.IsValidName(options.Owner) || !UpdateDownloadPolicy.IsValidName(options.Repository))
                throw new ArgumentException("Invalid GitHub owner or repository.");
            if (string.IsNullOrWhiteSpace(options.ExecutableName)) throw new ArgumentException("ExecutableName is required.");
            if (string.IsNullOrWhiteSpace(options.PackagePattern)) throw new ArgumentException("PackagePattern is required.");
            if (string.IsNullOrWhiteSpace(options.ApplicationName)) options.ApplicationName = Path.GetFileNameWithoutExtension(options.ExecutableName);
            if (string.IsNullOrWhiteSpace(options.ApplicationDirectory) && !string.IsNullOrEmpty(options.CurrentExecutablePath))
                options.ApplicationDirectory = Path.GetDirectoryName(options.CurrentExecutablePath);
            if (string.IsNullOrWhiteSpace(options.ApplicationDirectory)) throw new ArgumentException("ApplicationDirectory or CurrentExecutablePath is required.");
            if (string.IsNullOrWhiteSpace(options.TempDirectory)) options.TempDirectory = Path.GetTempPath();
            _options = options;
            _client = new GitHubReleaseClient(string.IsNullOrWhiteSpace(options.UserAgent) ? options.ApplicationName + "-Updater" : options.UserAgent);
        }

        public SelfUpdaterOptions Options
        {
            get { return _options; }
        }

        /// <summary>Log of the replacement script (%TEMP%\&lt;Application&gt;-update.log by default).</summary>
        public string LogPath
        {
            get { return Path.Combine(_options.TempDirectory, _options.ApplicationName + "-update.log"); }
        }

        /// <summary>Reads the latest release. Throws UpdateException(CheckFailed) when GitHub cannot be read.</summary>
        public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellation = default(CancellationToken))
        {
            GitHubRelease release;
            try
            {
                release = await _client.GetLatestAsync(_options.Owner, _options.Repository, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch (UpdateException) { throw; }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.CheckFailed, "The latest release could not be read: " + ex.Message, ex);
            }
            return new UpdateCheckResult
            {
                CurrentVersion = ReleaseVersion.Normalize(_options.CurrentVersion),
                LatestVersion = ReleaseVersion.Normalize(release.TagName),
                Release = release,
                Package = UpdateDownloadPolicy.FindPackage(release, _options.Owner, _options.Repository, _options.PackagePattern, _options.PackageName)
            };
        }

        /// <summary>
        /// Downloads, extracts and verifies the package of a release. Throws UpdateException; the work
        /// folder is then deleted. The installation is not changed.
        /// </summary>
        public async Task<PreparedUpdate> PrepareAsync(UpdateCheckResult check, IProgress<UpdateStage> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            if (check == null) throw new ArgumentNullException("check");
            if (check.Package == null)
                throw new UpdateException(UpdateFailure.NoPackage, "The release " + (check.Release == null ? "" : check.Release.TagName) + " has no package for " + _options.ApplicationName + ".");

            string work = Path.Combine(_options.TempDirectory, _options.ApplicationName + "-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                Report(progress, UpdateStage.Downloading);
                string zip = Path.Combine(work, "package.zip");
                try
                {
                    await _client.DownloadAsync(check.Package, _options.Owner, _options.Repository, zip, cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (UpdateException) { throw; }
                catch (Exception ex)
                {
                    throw new UpdateException(UpdateFailure.DownloadFailed, "The package could not be downloaded: " + ex.Message, ex);
                }

                Report(progress, UpdateStage.Extracting);
                string extract = Path.Combine(work, "extract");
                UpdatePackage.Extract(zip, extract);
                File.Delete(zip);
                string exe = UpdatePackage.LocateExecutable(extract, _options.ExecutableName);
                if (exe == null)
                    throw new UpdateException(UpdateFailure.ExecutableNotFound, _options.ExecutableName + " is not in the package.");

                Report(progress, UpdateStage.VerifyingSignature);
                AuthenticodeResult signature;
                UpdateSignatureVerdict verdict = UpdatePackage.VerifySignature(exe, _options.PinnedThumbprint, _options.CurrentExecutablePath, out signature);
                Report(progress, verdict == UpdateSignatureVerdict.Accepted ? UpdateStage.SignatureAccepted : UpdateStage.UnsignedAccepted);

                return new PreparedUpdate
                {
                    Version = check.LatestVersion,
                    WorkDirectory = work,
                    SourceDirectory = Path.GetDirectoryName(exe),
                    Executable = exe,
                    Verdict = verdict,
                    Signature = signature
                };
            }
            catch
            {
                try { Directory.Delete(work, true); } catch (Exception) { }
                throw;
            }
        }

        /// <summary>The services that run the executable of the installation.</summary>
        public List<string> FindServices()
        {
            return ServiceLocator.FindServices(Path.Combine(_options.ApplicationDirectory, _options.ExecutableName));
        }

        /// <summary>
        /// Writes and starts the replacement script, which waits for <paramref name="processId"/> to
        /// exit. The caller must then exit. Throws UpdateException(LaunchFailed).
        /// </summary>
        public void Apply(PreparedUpdate update, int processId, IProgress<UpdateStage> progress = null)
        {
            if (update == null) throw new ArgumentNullException("update");
            Report(progress, UpdateStage.LaunchingScript);
            List<string> services = FindServices();
            UpdateScriptOptions script = new UpdateScriptOptions
            {
                ApplicationName = _options.ApplicationName,
                ProcessId = processId,
                ApplicationDirectory = _options.ApplicationDirectory,
                SourceDirectory = update.SourceDirectory,
                ExecutableName = Path.GetFileName(_options.ExecutableName),
                RestartArguments = _options.RestartArguments ?? "",
                PreservePatterns = new List<string>(_options.PreservePatterns ?? new List<string>()),
                ServiceNames = services,
                Elevate = UpdateScriptLauncher.NeedsElevation(_options.ApplicationDirectory, services),
                WorkDirectory = update.WorkDirectory,
                LogPath = LogPath
            };
            string scriptPath = Path.Combine(update.WorkDirectory, _options.ApplicationName + "-Update.ps1");
            try
            {
                UpdateScriptBuilder.Write(script, scriptPath);
            }
            catch (Exception ex)
            {
                throw new UpdateException(UpdateFailure.LaunchFailed, "The update script could not be written: " + ex.Message, ex);
            }
            UpdateScriptLauncher.Start(scriptPath);
        }

        static void Report(IProgress<UpdateStage> progress, UpdateStage stage)
        {
            if (progress != null) progress.Report(stage);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
