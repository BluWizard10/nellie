using System;
using System.Collections.Generic;

namespace Nellie.Services
{
    /// <summary>
    /// Case-insensitive "natural" string comparison: digit runs are compared by
    /// numeric value, so "Track 2" sorts before "Track 10" rather than after it.
    /// </summary>
    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y) => CompareNatural(x, y);

        public static int CompareNatural(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                char cx = x[i];
                char cy = y[j];

                if (char.IsDigit(cx) && char.IsDigit(cy))
                {
                    int startX = i;
                    int startY = j;
                    while (i < x.Length && char.IsDigit(x[i]))
                        i++;
                    while (j < y.Length && char.IsDigit(y[j]))
                        j++;

                    // Compare by magnitude, ignoring leading zeros.
                    ReadOnlySpan<char> nx = x.AsSpan(startX, i - startX).TrimStart('0');
                    ReadOnlySpan<char> ny = y.AsSpan(startY, j - startY).TrimStart('0');

                    if (nx.Length != ny.Length)
                        return nx.Length - ny.Length;

                    int digitCmp = nx.SequenceCompareTo(ny);
                    if (digitCmp != 0)
                        return digitCmp;
                }
                else
                {
                    int charCmp = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
                    if (charCmp != 0)
                        return charCmp;
                    i++;
                    j++;
                }
            }

            return (x.Length - i) - (y.Length - j);
        }
    }
}
