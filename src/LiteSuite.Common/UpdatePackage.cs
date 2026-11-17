// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdatePackage.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: The downloaded release package: extraction (no entry outside the
//          folder, size limit), Mark of the Web removed, executable of the tool
//          located (archive root, single sub-folder, then anywhere) and its
//          Authenticode signature checked with UpdateSignaturePolicy (pinned
//          LiteSuite publisher, or the signer of the running executable).
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LiteSuite
{
    public static class UpdatePackage
    {
        /// <summary>Largest total size of the extracted files (bytes).</summary>
        public const long MaxExtractedBytes = 2L * 1024 * 1024 * 1024;

        /// <summary>
        /// Extracts a zip into <paramref name="folder"/> (created, must be empty or missing).
        /// Throws UpdateException(InvalidPackage) for an entry that would escape the folder, an
        /// invalid archive or extracted files larger than MaxExtractedBytes.
        /// </summary>
        public static void Extract(string zipPath, string folder)
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    long total = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        total += entry.Length;
                        if (total > MaxExtractedBytes) throw new UpdateException(UpdateFailure.InvalidPackage, "The package is too large once extracted.");
                    }
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw new UpdateException(UpdateFailure.InvalidPackage, "The package has an entry outside its folder: " + entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(target);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        entry.ExtractToFile(target, false);
                    }
                }
            }
            catch (UpdateException) { throw; }
            catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is NotSupportedException || ex is ArgumentException)
            {
                throw new UpdateException(UpdateFailure.InvalidPackage, "The package cannot be extracted: " + ex.Message, ex);
            }
            MarkOfTheWeb.RemoveFromDirectory(root);
        }

        /// <summary>
        /// The executable of the tool in an extracted package: at its root, in its only sub-folder,
        /// then the shortest path anywhere. Null when absent.
        /// </summary>
        public static string LocateExecutable(string folder, string executableName)
        {
            if (!Directory.Exists(folder) || string.IsNullOrEmpty(executableName)) return null;
            string name = Path.GetFileName(executableName);
            string atRoot = Path.Combine(folder, name);
            if (File.Exists(atRoot)) return atRoot;
            string[] subFolders = Directory.GetDirectories(folder);
            if (subFolders.Length == 1)
            {
                string nested = Path.Combine(subFolders[0], name);
                if (File.Exists(nested)) return nested;
            }
            return Directory.GetFiles(folder, name, SearchOption.AllDirectories).OrderBy(p => p.Length).FirstOrDefault();
        }

        /// <summary>
        /// Checks the signature of the executable of the package. Returns the verdict when it is
        /// Accepted or AcceptedUnsignedDevBuild; throws UpdateException(SignatureRejected) otherwise.
        /// </summary>
        /// <param name="runningExecutable">Path of the running executable (its signer is also accepted; unsigned: development build).</param>
        public static UpdateSignatureVerdict VerifySignature(string packageExecutable, string pinnedThumbprint, string runningExecutable, out AuthenticodeResult signature)
        {
            AuthenticodeResult running = string.IsNullOrEmpty(runningExecutable) || !File.Exists(runningExecutable)
                ? null : AuthenticodeVerifier.Verify(runningExecutable);
            signature = AuthenticodeVerifier.Verify(packageExecutable);
            UpdateSignatureVerdict verdict = UpdateSignaturePolicy.Evaluate(signature, running, UpdateSignaturePolicy.ExpectedThumbprints(pinnedThumbprint, running));
            if (verdict != UpdateSignatureVerdict.Accepted && verdict != UpdateSignatureVerdict.AcceptedUnsignedDevBuild)
                throw new UpdateException(verdict, signature, "The package was refused: " + verdict + " (" + signature + ").");
            return verdict;
        }
    }
}
