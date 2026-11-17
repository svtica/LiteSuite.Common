// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateFailure.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Why a self-update stopped (UpdateException.Failure), so that each
//          tool shows its own translated message.
// -----------------------------------------------------------------------------
namespace LiteSuite
{
    public enum UpdateFailure
    {
        /// <summary>The GitHub API could not be read or its answer is not a release.</summary>
        CheckFailed,
        /// <summary>The release has no package matching the tool.</summary>
        NoPackage,
        /// <summary>The package link or a redirect is not accepted (UpdateDownloadPolicy).</summary>
        UnsafeUrl,
        /// <summary>The download failed or the file is too large.</summary>
        DownloadFailed,
        /// <summary>The archive is invalid, too large or has an entry outside its folder.</summary>
        InvalidPackage,
        /// <summary>The executable of the tool is not in the package.</summary>
        ExecutableNotFound,
        /// <summary>The signature of the executable was refused (see UpdateException.Verdict).</summary>
        SignatureRejected,
        /// <summary>The replacement script could not be written or started.</summary>
        LaunchFailed
    }
}
