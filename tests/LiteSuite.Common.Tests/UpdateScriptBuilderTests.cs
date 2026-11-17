// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    UpdateScriptBuilderTests.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Tests of UpdateScriptBuilder: ASCII-only script, PowerShell
//          literals, options; on Windows, the script is run on temporary
//          folders (files replaced, files of the user kept, rollback when a
//          file is locked). No service, no elevation, no restart in the tests.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LiteSuite.Tests
{
    [TestClass]
    public class UpdateScriptBuilderTests
    {
        string _dir;

        [TestInitialize]
        public void Init()
        {
            // Non-ASCII characters and a quote in the path: the script must still be ASCII and correct
            _dir = Path.Combine(Path.GetTempPath(), "LiteSuiteTests-" + Guid.NewGuid().ToString("N"), "Mise \u00e0 jour d'essai");
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(Path.GetDirectoryName(_dir), true); } catch (Exception) { }
        }

        UpdateScriptOptions Options(int processId = 4242)
        {
            return new UpdateScriptOptions
            {
                ApplicationName = "Tool",
                ProcessId = processId,
                ApplicationDirectory = Path.Combine(_dir, "app"),
                SourceDirectory = Path.Combine(_dir, "work", "extract"),
                ExecutableName = "Tool.exe",
                Restart = false,
                PreservePatterns = new List<string> { "adcache.xml", "*.local.*", "settings.xml" },
                ServiceNames = new List<string>(),
                WorkDirectory = Path.Combine(_dir, "work"),
                LogPath = Path.Combine(_dir, "update.log")
            };
        }

        static bool IsAscii(string text)
        {
            return text.All(c => c < 128);
        }

        [TestMethod]
        public void Literal_EscapesQuotesAndNonAscii()
        {
            Assert.AreEqual("''", UpdateScriptBuilder.Literal(null));
            Assert.AreEqual("'C:\\Tools'", UpdateScriptBuilder.Literal("C:\\Tools"));
            Assert.AreEqual("'it''s'", UpdateScriptBuilder.Literal("it's"));
            Assert.AreEqual("('Fr' + [char]0x00E9 + 'd')", UpdateScriptBuilder.Literal("Fr\u00e9d"));
            Assert.AreEqual("('' + [char]0x00C9 + 't' + [char]0x00E9)", UpdateScriptBuilder.Literal("\u00c9t\u00e9"));
            Assert.AreEqual("('a' + [char]0x000A + 'b')", UpdateScriptBuilder.Literal("a\nb"));
            Assert.AreEqual("('' + [char]0xD83D + [char]0xDE00)", UpdateScriptBuilder.Literal("\U0001F600"));
            Assert.AreEqual("('' + [char]0x2019 + '$x')", UpdateScriptBuilder.Literal("\u2019$x"));
        }

        [TestMethod]
        public void Build_IsAsciiWithCrlfAndHasTheValues()
        {
            UpdateScriptOptions options = Options();
            options.ServiceNames.Add("LitePMRemoteService");
            options.RestartArguments = "-HIDE";
            options.Elevate = true;
            options.ApplicationName = "T\u00f6\u00f6l";
            string script = UpdateScriptBuilder.Build(options);

            Assert.IsTrue(IsAscii(script));
            Assert.IsFalse(script.Replace("\r\n", "").Contains("\n"));
            StringAssert.Contains(script, "$processId = 4242");
            StringAssert.Contains(script, "$preserve = @('adcache.xml', '*.local.*', 'settings.xml')");
            StringAssert.Contains(script, "$services = @('LitePMRemoteService')");
            StringAssert.Contains(script, "$restartArguments = '-HIDE'");
            StringAssert.Contains(script, "$elevate = $true");
            StringAssert.Contains(script, "$restart = $false");
            StringAssert.Contains(script, "d''essai");
            StringAssert.Contains(script, "[char]0x00E0");
            StringAssert.Contains(script, "# T?? update script");
            Assert.IsFalse(script.Contains("{{"), "placeholder left");
        }

        [TestMethod]
        public void Build_NormalizesSlashesAndSkipsEmptyItems()
        {
            UpdateScriptOptions options = Options();
            options.PreservePatterns = new List<string> { "config/*.xml", "", null };
            options.ServiceNames = null;
            string script = UpdateScriptBuilder.Build(options);
            StringAssert.Contains(script, "$preserve = @('config\\*.xml')");
            StringAssert.Contains(script, "$services = @()");
        }

        [TestMethod]
        public void Build_ValidatesTheOptions()
        {
            Assert.ThrowsException<ArgumentNullException>(() => UpdateScriptBuilder.Build(null));
            UpdateScriptOptions options = Options();
            options.ExecutableName = "..\\Tool.exe";
            Assert.ThrowsException<ArgumentException>(() => UpdateScriptBuilder.Build(options));
            options = Options(0);
            Assert.ThrowsException<ArgumentException>(() => UpdateScriptBuilder.Build(options));
            options = Options();
            options.ApplicationDirectory = " ";
            Assert.ThrowsException<ArgumentException>(() => UpdateScriptBuilder.Build(options));
        }

        [TestMethod]
        public void Write_CreatesAnAsciiFile()
        {
            string path = Path.Combine(_dir, "Tool-Update.ps1");
            UpdateScriptBuilder.Write(Options(), path);
            Assert.IsFalse(File.ReadAllBytes(path).Any(b => b > 127));
        }

        [TestMethod]
        public void LauncherStartInfo_RunsTheScriptHidden()
        {
            ProcessStartInfo info = UpdateScriptLauncher.CreateStartInfo(@"C:\Temp\Tool-Update.ps1");
            StringAssert.EndsWith(info.FileName, "powershell.exe");
            Assert.AreEqual("-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"C:\\Temp\\Tool-Update.ps1\"", info.Arguments);
            Assert.IsTrue(info.CreateNoWindow);
            Assert.ThrowsException<ArgumentException>(() => UpdateScriptLauncher.CreateStartInfo("a\"b.ps1"));
            Assert.IsTrue(UpdateScriptLauncher.CanWrite(_dir));
            Assert.IsFalse(UpdateScriptLauncher.CanWrite(Path.Combine(_dir, "missing")));
        }

        // ---- The script itself (Windows PowerShell) --------------------------------

        static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        /// <summary>A process that exits by itself after about a second, for the script to wait for.</summary>
        static Process ShortProcess()
        {
            return Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 > nul") { UseShellExecute = false, CreateNoWindow = true });
        }

        static void RequirePowerShell()
        {
            if (!Path.IsPathRooted(UpdateScriptLauncher.PowerShellPath)) Assert.Inconclusive("Windows PowerShell is not available.");
        }

        void Run(UpdateScriptOptions options)
        {
            string scriptPath = Path.Combine(_dir, "Tool-Update.ps1");
            UpdateScriptBuilder.Write(options, scriptPath);
            ProcessStartInfo info = UpdateScriptLauncher.CreateStartInfo(scriptPath);
            using (Process script = Process.Start(info))
            {
                Assert.IsTrue(script.WaitForExit(120000), "the script did not end");
            }
        }

        [TestMethod]
        public void Script_ReplacesTheFilesAndKeepsThoseOfTheUser()
        {
            RequirePowerShell();
            UpdateScriptOptions options = Options();
            string app = options.ApplicationDirectory, source = options.SourceDirectory;
            Write(Path.Combine(app, "Tool.exe"), "old exe");
            Write(Path.Combine(app, "adcache.xml"), "user cache");
            Write(Path.Combine(app, "lang", "fr.xml"), "old fr");
            Write(Path.Combine(app, "lang", "x.local.xml"), "user local");
            Write(Path.Combine(app, "modules", "Mine.ps1"), "user module");
            Write(Path.Combine(source, "Tool.exe"), "new exe");
            Write(Path.Combine(source, "adcache.xml"), "example cache");
            Write(Path.Combine(source, "lang", "fr.xml"), "new fr");
            Write(Path.Combine(source, "lang", "en.xml"), "new en");
            Write(Path.Combine(source, "lang", "x.local.xml"), "example local");
            Write(Path.Combine(source, "settings.xml"), "default settings");

            using (Process app0 = ShortProcess())
            {
                options.ProcessId = app0.Id;
                Run(options);
            }

            Assert.AreEqual("new exe", File.ReadAllText(Path.Combine(app, "Tool.exe")));
            Assert.AreEqual("new fr", File.ReadAllText(Path.Combine(app, "lang", "fr.xml")));
            Assert.AreEqual("new en", File.ReadAllText(Path.Combine(app, "lang", "en.xml")));
            Assert.AreEqual("user cache", File.ReadAllText(Path.Combine(app, "adcache.xml")));
            Assert.AreEqual("user local", File.ReadAllText(Path.Combine(app, "lang", "x.local.xml")));
            Assert.AreEqual("user module", File.ReadAllText(Path.Combine(app, "modules", "Mine.ps1")));
            // A file to keep that did not exist yet is installed
            Assert.AreEqual("default settings", File.ReadAllText(Path.Combine(app, "settings.xml")));
            Assert.IsFalse(Directory.Exists(options.WorkDirectory), "work folder not deleted");
            string log = File.ReadAllText(options.LogPath, Encoding.UTF8);
            StringAssert.Contains(log, "Kept adcache.xml");
            StringAssert.Contains(log, "Files replaced: 2, added: 2");
            StringAssert.Contains(log, "Done");
        }

        [TestMethod]
        public void Script_RestoresTheReplacedFilesWhenACopyFails()
        {
            RequirePowerShell();
            UpdateScriptOptions options = Options();
            string app = options.ApplicationDirectory, source = options.SourceDirectory;
            Write(Path.Combine(app, "a.dll"), "old a");
            Write(Path.Combine(app, "z.dll"), "old z");
            Write(Path.Combine(source, "a.dll"), "new a");
            Write(Path.Combine(source, "b.txt"), "new b");
            Write(Path.Combine(source, "z.dll"), "new z");

            using (new FileStream(Path.Combine(app, "z.dll"), FileMode.Open, FileAccess.Read, FileShare.None))
            using (Process app0 = ShortProcess())
            {
                options.ProcessId = app0.Id;
                Run(options);
            }

            Assert.AreEqual("old a", File.ReadAllText(Path.Combine(app, "a.dll")));
            Assert.AreEqual("old z", File.ReadAllText(Path.Combine(app, "z.dll")));
            Assert.IsFalse(File.Exists(Path.Combine(app, "b.txt")));
            StringAssert.Contains(File.ReadAllText(options.LogPath, Encoding.UTF8), "Update failed");
        }
    }
}
