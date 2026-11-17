// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdatePackageTests.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Unit tests of UpdatePackage: extraction (entries outside the
//          folder refused), executable location, signature of an unsigned
//          package, and of ServiceLocator (service command lines).
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LiteSuite.Tests
{
    [TestClass]
    public class UpdatePackageTests
    {
        string _dir;

        [TestInitialize]
        public void Init()
        {
            _dir = Path.Combine(Path.GetTempPath(), "LiteSuiteTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        string Zip(params string[] entries)
        {
            string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".zip");
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (string entry in entries)
                {
                    ZipArchiveEntry item = archive.CreateEntry(entry);
                    if (entry.EndsWith("/")) continue;
                    using (Stream stream = item.Open())
                    {
                        byte[] content = Encoding.ASCII.GetBytes("content of " + entry);
                        stream.Write(content, 0, content.Length);
                    }
                }
            }
            return path;
        }

        [TestMethod]
        public void Extract_WritesFilesAndFolders()
        {
            string target = Path.Combine(_dir, "out");
            UpdatePackage.Extract(Zip("Tool.exe", "lang/", "lang/fr.xml", "modules/A.ps1"), target);
            Assert.IsTrue(File.Exists(Path.Combine(target, "Tool.exe")));
            Assert.AreEqual("content of lang/fr.xml", File.ReadAllText(Path.Combine(target, "lang", "fr.xml")));
            Assert.IsTrue(File.Exists(Path.Combine(target, "modules", "A.ps1")));
        }

        [TestMethod]
        public void Extract_RefusesEntriesOutsideTheFolder()
        {
            string target = Path.Combine(_dir, "out");
            UpdateException error = Assert.ThrowsException<UpdateException>(() => UpdatePackage.Extract(Zip("ok.txt", "../evil.txt"), target));
            Assert.AreEqual(UpdateFailure.InvalidPackage, error.Failure);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "evil.txt")));
        }

        [TestMethod]
        public void Extract_RefusesAnInvalidArchive()
        {
            string bad = Path.Combine(_dir, "bad.zip");
            File.WriteAllText(bad, "not a zip");
            Assert.AreEqual(UpdateFailure.InvalidPackage,
                Assert.ThrowsException<UpdateException>(() => UpdatePackage.Extract(bad, Path.Combine(_dir, "out"))).Failure);
        }

        [TestMethod]
        public void LocateExecutable_RootThenSingleFolderThenAnywhere()
        {
            string a = Path.Combine(_dir, "a");
            UpdatePackage.Extract(Zip("Tool.exe", "sub/Tool.exe"), a);
            Assert.AreEqual(Path.Combine(a, "Tool.exe"), UpdatePackage.LocateExecutable(a, "Tool.exe"));

            string b = Path.Combine(_dir, "b");
            UpdatePackage.Extract(Zip("Tool-1.0/Tool.exe", "Tool-1.0/Tool.dll"), b);
            Assert.AreEqual(Path.Combine(b, "Tool-1.0", "Tool.exe"), UpdatePackage.LocateExecutable(b, "Tool.exe"));

            string c = Path.Combine(_dir, "c");
            UpdatePackage.Extract(Zip("x/y/z/Tool.exe", "x/Tool.exe", "w/readme.txt"), c);
            Assert.AreEqual(Path.Combine(c, "x", "Tool.exe"), UpdatePackage.LocateExecutable(c, "Tool.exe"));

            Assert.IsNull(UpdatePackage.LocateExecutable(c, "Other.exe"));
            Assert.IsNull(UpdatePackage.LocateExecutable(Path.Combine(_dir, "missing"), "Tool.exe"));
        }

        [TestMethod]
        public void VerifySignature_UnsignedPackage_AcceptedOnlyByAnUnsignedTool()
        {
            string package = Path.Combine(_dir, "Tool.exe");
            string running = Path.Combine(_dir, "Running.exe");
            // The test assembly: a PE file without signature
            File.Copy(typeof(UpdatePackageTests).Assembly.Location, package);
            File.Copy(typeof(UpdatePackageTests).Assembly.Location, running);
            AuthenticodeResult signature;

            Assert.AreEqual(UpdateSignatureVerdict.AcceptedUnsignedDevBuild,
                UpdatePackage.VerifySignature(package, SelfUpdaterOptions.LiteSuiteThumbprint, running, out signature));
            Assert.AreEqual(AuthenticodeStatus.NotSigned, signature.Status);

            // Running executable unknown: fail closed
            UpdateException error = Assert.ThrowsException<UpdateException>(
                () => UpdatePackage.VerifySignature(package, SelfUpdaterOptions.LiteSuiteThumbprint, Path.Combine(_dir, "missing.exe"), out signature));
            Assert.AreEqual(UpdateFailure.SignatureRejected, error.Failure);
            Assert.AreEqual(UpdateSignatureVerdict.RejectedUnsigned, error.Verdict);
        }

        [TestMethod]
        public void ServiceLocator_ExecutableFromImagePath()
        {
            Assert.AreEqual(@"C:\Program Files\LitePM\LitePM.exe", ServiceLocator.ExecutableFromImagePath("\"C:\\Program Files\\LitePM\\LitePM.exe\" -SERVERSERVICE"));
            Assert.AreEqual(@"C:\Tools\LiteTask\LiteTask.exe", ServiceLocator.ExecutableFromImagePath(@"C:\Tools\LiteTask\LiteTask.exe -service"));
            Assert.AreEqual(@"C:\Program Files\X\x.exe", ServiceLocator.ExecutableFromImagePath(@"C:\Program Files\X\x.exe /run"));
            Assert.AreEqual(@"C:\drivers\a.sys", ServiceLocator.ExecutableFromImagePath(@"\??\C:\drivers\a.sys"));
            StringAssert.EndsWith(ServiceLocator.ExecutableFromImagePath(@"\SystemRoot\System32\drivers\b.sys"), @"\System32\drivers\b.sys");
            Assert.IsNull(ServiceLocator.ExecutableFromImagePath(null));
            Assert.IsNull(ServiceLocator.ExecutableFromImagePath("\"C:\\unterminated"));
        }

        [TestMethod]
        public void ServiceLocator_ImagePathRuns_ComparesFullPathsIgnoringCase()
        {
            Assert.IsTrue(ServiceLocator.ImagePathRuns("\"C:\\Tools\\LitePM\\LitePM.exe\" -SERVERSERVICE", @"c:\tools\litepm\LitePM.EXE"));
            Assert.IsTrue(ServiceLocator.ImagePathRuns(@"C:\Tools\LitePM\..\LitePM\LitePM.exe", @"C:\Tools\LitePM\LitePM.exe"));
            Assert.IsFalse(ServiceLocator.ImagePathRuns(@"C:\Other\LitePM.exe", @"C:\Tools\LitePM\LitePM.exe"));
            Assert.IsFalse(ServiceLocator.ImagePathRuns(@"C:\Windows\System32\svchost.exe -k netsvcs", @"C:\Tools\LitePM\LitePM.exe"));
            Assert.IsFalse(ServiceLocator.ImagePathRuns(null, @"C:\Tools\LitePM\LitePM.exe"));
        }

        [TestMethod]
        public void ServiceLocator_FindServices_NothingForAnUnknownExecutable()
        {
            Assert.AreEqual(0, ServiceLocator.FindServices(Path.Combine(_dir, "NotAService.exe")).Count);
            Assert.IsFalse(ServiceLocator.IsActive("LiteSuite-Test-" + Guid.NewGuid().ToString("N")));
        }
    }
}
