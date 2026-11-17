// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    PreparedUpdate.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: A downloaded, extracted and verified package, ready to be applied
//          by SelfUpdater.Apply. Nothing of the installation has changed yet.
// -----------------------------------------------------------------------------
using System;
using System.IO;

namespace LiteSuite
{
    public sealed class PreparedUpdate
    {
        public string Version;
        /// <summary>Work folder of this update (package, extracted files, script, backup).</summary>
        public string WorkDirectory;
        /// <summary>Folder of the executable in the extracted package: its content is copied.</summary>
        public string SourceDirectory;
        /// <summary>The executable of the tool in the package.</summary>
        public string Executable;
        public UpdateSignatureVerdict Verdict;
        public AuthenticodeResult Signature;

        /// <summary>The package is not signed (accepted only because the running tool is not signed either).</summary>
        public bool IsUnsigned
        {
            get { return Verdict == UpdateSignatureVerdict.AcceptedUnsignedDevBuild; }
        }

        /// <summary>Deletes the work folder (update cancelled).</summary>
        public void Discard()
        {
            try
            {
                if (WorkDirectory != null && Directory.Exists(WorkDirectory)) Directory.Delete(WorkDirectory, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
