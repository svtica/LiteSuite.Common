// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    ReleaseVersionTests.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Unit tests of ReleaseVersion (tags and versions of the releases).
// -----------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LiteSuite.Tests
{
    [TestClass]
    public class ReleaseVersionTests
    {
        [TestMethod]
        public void TryParse_TagsAndVersions()
        {
            int[] parts;
            Assert.IsTrue(ReleaseVersion.TryParse("v1.2.3", out parts));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, parts);
            Assert.IsTrue(ReleaseVersion.TryParse("V1.0.12.0", out parts));
            CollectionAssert.AreEqual(new[] { 1, 0, 12, 0 }, parts);
            Assert.IsTrue(ReleaseVersion.TryParse(" 2.0-beta.1 ", out parts));
            CollectionAssert.AreEqual(new[] { 2, 0 }, parts);
            Assert.IsTrue(ReleaseVersion.TryParse("1.1.0+abcdef", out parts));
            CollectionAssert.AreEqual(new[] { 1, 1, 0 }, parts);
        }

        [TestMethod]
        public void TryParse_RejectsNonVersions()
        {
            int[] parts;
            foreach (string text in new[] { null, "", " ", "v", "latest", "1..2", "1.2.3.4.5", "1.-2", "1.a", "-1.0" })
                Assert.IsFalse(ReleaseVersion.TryParse(text, out parts), text ?? "null");
        }

        [TestMethod]
        public void IsNewer_ComparesPartByPart()
        {
            Assert.IsTrue(ReleaseVersion.IsNewer("1.0.12.0", "v1.0.13"));
            Assert.IsTrue(ReleaseVersion.IsNewer("1.1.3.0", "1.2"));
            Assert.IsTrue(ReleaseVersion.IsNewer("1.9.0", "1.10.0"));
            Assert.IsFalse(ReleaseVersion.IsNewer("1.2.0.0", "v1.2"));
            Assert.IsFalse(ReleaseVersion.IsNewer("1.2.1", "1.2.0"));
            Assert.IsFalse(ReleaseVersion.IsNewer("2.0.0", "1.99.99"));
            Assert.IsFalse(ReleaseVersion.IsNewer("1.2.0", "1.2.0-beta"));
        }

        [TestMethod]
        public void IsNewer_FalseWhenNotAVersion()
        {
            Assert.IsFalse(ReleaseVersion.IsNewer("1.0.0", "nightly"));
            Assert.IsFalse(ReleaseVersion.IsNewer(null, "2.0.0"));
        }

        [TestMethod]
        public void Compare_And_Normalize()
        {
            Assert.AreEqual(0, ReleaseVersion.Compare("1.2", "1.2.0.0"));
            Assert.AreEqual(-1, ReleaseVersion.Compare("1.2", "1.2.0.1"));
            Assert.AreEqual(1, ReleaseVersion.Compare("v3", "2.9"));
            Assert.ThrowsException<FormatException>(() => ReleaseVersion.Compare("x", "1"));
            Assert.AreEqual("1.2.3", ReleaseVersion.Normalize("v1.2.3-rc1"));
            Assert.AreEqual("nightly", ReleaseVersion.Normalize("nightly"));
        }
    }
}
