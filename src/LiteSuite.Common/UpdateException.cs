// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateException.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Failure of a self-update step. Nothing was replaced when it is
//          thrown: the files are only replaced by the script, after it.
// -----------------------------------------------------------------------------
using System;

namespace LiteSuite
{
    public sealed class UpdateException : Exception
    {
        public UpdateFailure Failure { get; private set; }

        /// <summary>For SignatureRejected: the verdict of UpdateSignaturePolicy.</summary>
        public UpdateSignatureVerdict? Verdict { get; private set; }

        /// <summary>For SignatureRejected: the signature of the executable of the package.</summary>
        public AuthenticodeResult Signature { get; private set; }

        public UpdateException(UpdateFailure failure, string message, Exception inner = null)
            : base(message, inner)
        {
            Failure = failure;
        }

        public UpdateException(UpdateSignatureVerdict verdict, AuthenticodeResult signature, string message)
            : base(message)
        {
            Failure = UpdateFailure.SignatureRejected;
            Verdict = verdict;
            Signature = signature;
        }
    }
}
