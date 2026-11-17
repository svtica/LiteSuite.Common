// -----------------------------------------------------------------------------
// LiteSuite.Common - part of LiteSuite. Released under The Unlicense (see LICENSE).
// File:    ReleaseVersion.cs
// Author:  LiteSuite contributors
// Date:    2026-11-17
// Purpose: Versions of the LiteSuite releases: reads a tag ("v1.2.3",
//          "1.0.12.0") or a product version ("1.2.3+abcdef") and tells whether
//          a release is newer than the running version. Ported from LiteTask
//          (UpdateManager.IsNewerVersion).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LiteSuite
{
    public static class ReleaseVersion
    {
        /// <summary>
        /// Numeric parts of a version: leading "v" removed, anything after "-", "+" or a space
        /// ignored (pre-release, build metadata), 1 to 4 parts. False when it is not a version.
        /// </summary>
        public static bool TryParse(string text, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            int cut = value.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut >= 0) value = value.Substring(0, cut);
            string[] items = value.Split('.');
            if (items.Length < 1 || items.Length > 4) return false;

            List<int> result = new List<int>();
            foreach (string item in items)
            {
                int number;
                if (item.Length == 0 || !int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out number)) return false;
                result.Add(number);
            }
            parts = result.ToArray();
            return true;
        }

        /// <summary>The version without its "v" and suffixes ("v1.2.3-beta" -> "1.2.3"), or the text unchanged.</summary>
        public static string Normalize(string text)
        {
            int[] parts;
            return TryParse(text, out parts) ? string.Join(".", parts) : text;
        }

        /// <summary>
        /// Compares two versions part by part, missing parts counting as 0 (1.2 = 1.2.0.0).
        /// Throws FormatException when one of them is not a version.
        /// </summary>
        public static int Compare(string left, string right)
        {
            int[] a, b;
            if (!TryParse(left, out a)) throw new FormatException("Not a version: " + left);
            if (!TryParse(right, out b)) throw new FormatException("Not a version: " + right);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        /// <summary>True when <paramref name="remote"/> is newer than <paramref name="current"/>; false when either is not a version.</summary>
        public static bool IsNewer(string current, string remote)
        {
            int[] a, b;
            if (!TryParse(current, out a) || !TryParse(remote, out b)) return false;
            return Compare(current, remote) < 0;
        }
    }
}
