// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateStage.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Steps of a self-update reported to the tool (IProgress), which
//          shows them with its own translated texts.
// -----------------------------------------------------------------------------
namespace LiteSuite
{
    public enum UpdateStage
    {
        Checking,
        Downloading,
        Extracting,
        VerifyingSignature,
        SignatureAccepted,
        /// <summary>Unsigned package accepted because the running tool is an unsigned development build.</summary>
        UnsignedAccepted,
        LaunchingScript
    }
}
