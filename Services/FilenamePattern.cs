using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Nellie.Services
{
    /// <summary>
    /// Compiles a user-supplied filename template such as
    /// <c>{artist} - {song} [{label}]</c> into a regular expression, then extracts
    /// the named fields from any filename. Literal text between tokens (spaces,
    /// dashes, brackets) becomes the delimiters the parser matches on.
    /// </summary>
    public sealed class FilenamePattern
    {
        private static readonly Regex TokenFinder = new(@"\{(\w+)\}", RegexOptions.Compiled);
        private static readonly IReadOnlyDictionary<string, string> NoFields =
            new Dictionary<string, string>();

        private readonly Regex? _matcher;

        public FilenamePattern(string template)
        {
            Template = template ?? string.Empty;

            var tokens = new List<string>();
            _matcher = TryCompile(Template, tokens);
            Tokens = tokens;
        }

        public string Template { get; }

        /// <summary>Token names in the order they appear, e.g. artist, song, label.</summary>
        public IReadOnlyList<string> Tokens { get; }

        public bool IsValid => _matcher is not null;

        /// <summary>
        /// Parses a filename (without extension) into its token values. Returns an
        /// empty map when the pattern is invalid or the name doesn't match.
        /// </summary>
        public IReadOnlyDictionary<string, string> Parse(string fileNameWithoutExtension)
        {
            if (_matcher is null)
                return NoFields;

            var match = _matcher.Match(fileNameWithoutExtension);
            if (!match.Success)
                return NoFields;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in Tokens)
            {
                var group = match.Groups[token];
                if (group.Success)
                    fields[token] = group.Value.Trim();
            }

            return fields;
        }

        private static Regex? TryCompile(string template, List<string> tokens)
        {
            var matches = TokenFinder.Matches(template);
            if (matches.Count == 0)
                return null;

            var sb = new StringBuilder("^");
            int cursor = 0;
            bool isFirst = true;

            foreach (Match token in matches)
            {
                // Literal text preceding this token is matched verbatim.
                sb.Append(Regex.Escape(template.Substring(cursor, token.Index - cursor)));

                string name = token.Groups[1].Value;
                if (tokens.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    tokens.Clear();
                    return null; // duplicate token names aren't allowed
                }

                tokens.Add(name);

                // The first token is non-greedy so it stops at the first delimiter
                // (e.g. artist ends at the first " - "). Later tokens are greedy so
                // they extend to the *last* occurrence of their delimiter — this lets
                // a song keep internal brackets like "[Extended Mix]" while the final
                // "[...]" is still recognised as the label.
                sb.Append("(?<").Append(name).Append('>').Append(isFirst ? ".+?" : ".+").Append(')');
                isFirst = false;
                cursor = token.Index + token.Length;
            }

            sb.Append(Regex.Escape(template.Substring(cursor)));
            sb.Append('$');

            try
            {
                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException)
            {
                tokens.Clear();
                return null;
            }
        }
    }
}
