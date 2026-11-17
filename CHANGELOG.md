# Changelog

All notable changes to LiteSuite.Common are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Self-update from the GitHub releases, generalized from the `UpdateManager` of LiteTask, for
  LiteDeploy, LitePM and LiteTask:
  - `SelfUpdater` (check, prepare, apply) and `SelfUpdaterOptions`, `UpdateCheckResult`,
    `PreparedUpdate`, `UpdateStage`, `UpdateException`, `UpdateFailure`;
  - `GitHubReleaseClient`, `GitHubRelease`, `GitHubReleaseAsset`: latest release read with
    `DataContractJsonSerializer` (no `System.Text.Json` on .NET Framework 4.8);
  - `ReleaseVersion`: tags and versions (`v1.2.3`, `1.0.12.0`), comparison;
  - `UpdateDownloadPolicy`: release assets of the expected repository on `github.com` only, over
    HTTPS (redirects included), 512 MB at most;
  - `UpdatePackage`: extraction refusing entries outside the folder, Mark of the Web removed,
    executable located, Authenticode signature checked with `UpdateSignaturePolicy`;
  - `ServiceLocator`: services that run the executable of the tool, from their `ImagePath`;
  - `UpdateScriptBuilder`, `UpdateScriptOptions`, `UpdateScriptLauncher`: ASCII-only PowerShell
    script that waits for the tool, stops and restarts its services, copies the files without
    deleting anything, keeps the files of the user, restores the replaced files when a copy
    fails, and restarts the tool; only the copy is elevated when needed.
- Tests of all of the above; on Windows the generated script is run on temporary folders.
- `System.ServiceProcess.ServiceController` (Microsoft) on .NET 10, for the service status.

## [1.0.1]

### Added
- Library and tests moved from LiteDeploy; NuGet package on GitHub Packages, CI and Release
  workflow.
