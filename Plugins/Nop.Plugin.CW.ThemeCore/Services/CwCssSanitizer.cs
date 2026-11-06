using System.Text;
using System.Text.RegularExpressions;
using Nop.Plugin.CW.ThemeCore.Domain;

namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Heuristic CSS sanitizer. Not a full CSS parser — it strips known-dangerous constructs and
/// applies the url() policy. The generated output is served as <c>text/css</c> (not executed),
/// and only a <c>CW.ThemeCore.Manage</c> admin can author it, so this defence-in-depth pass
/// targets the realistic vectors rather than attempting exhaustive parsing.
/// </summary>
/// <remarks>
/// The browser decodes CSS backslash escapes (<c>\68</c> is "h") before it acts on a keyword or a URL, so the checks
/// must see decoded text too. Escaped letters are decoded in the output itself — an escaped letter means the same as
/// the letter in every CSS context, so this changes nothing the browser renders. Other escapes (digits, ':', quotes,
/// icon-font code points such as <c>\f101</c>) can change meaning when decoded, so they stay escaped in the output,
/// and URL values are decoded in full only to decide whether they are external.
/// </remarks>
public partial class CwCssSanitizer : ICwCssSanitizer
{
    private const int MaxLength = 100_000;

    //what the URL parser trims from both ends of a URL: C0 controls and space
    private static readonly char[] _spaceAndControlCharacters = Enumerable.Range(0, 0x21).Select(c => (char)c).ToArray();

    #region Compiled patterns

    [GeneratedRegex(@"@import[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ImportRegex();

    [GeneratedRegex(@"expression\s*\([^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpressionRegex();

    //the colon may itself be escaped (\3a, \00003a, \:)
    [GeneratedRegex(@"(javascript|vbscript)\s*(?::|\\0{0,4}3a\s?|\\:)", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptUriRegex();

    [GeneratedRegex(@"(behavior|-moz-binding)\s*:[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex BehaviorRegex();

    //a quoted value may contain ')' and escaped quotes; an unquoted one ends at ')' or whitespace, except the one
    //whitespace that ends a hex escape (\3a )
    [GeneratedRegex(@"url\s*\(\s*(?:""(?<val>(?:[^""\\]|\\.)*)""|'(?<val>(?:[^'\\]|\\.)*)'|(?<val>(?:[^)\s'""\\]|\\[0-9a-fA-F]{1,6}\s?|\\.)*))\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UrlRegex();

    //image-set(), -webkit-image-set() and image() take bare strings as URLs, not only url()
    [GeneratedRegex(@"(?<![\w-])(?:-webkit-)?image(?:-set)?\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex ImageFunctionStartRegex();

    [GeneratedRegex(@"""(?<val>(?:[^""\\]|\\.)*)""|'(?<val>(?:[^'\\]|\\.)*)'", RegexOptions.Singleline)]
    private static partial Regex QuotedStringRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:")]
    private static partial Regex UrlSchemeRegex();

    #endregion

    #region Utilities

    private static bool IsHexDigit(char c) => Uri.IsHexDigit(c);

    private static bool IsAsciiLetter(int codePoint) => codePoint is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>
    /// Decodes the CSS escapes whose code point <paramref name="decode"/> accepts and keeps the rest as written.
    /// A kept hex escape is always written with its terminating space, so a letter decoded right after it can never
    /// run into it and change its code point.
    /// </summary>
    private static string DecodeEscapes(string css, Func<int, bool> decode)
    {
        if (css.IndexOf('\\') < 0)
            return css;

        var builder = new StringBuilder(css.Length);
        var i = 0;
        while (i < css.Length)
        {
            var c = css[i];
            if (c != '\\' || i + 1 >= css.Length || css[i + 1] is '\n' or '\r' or '\f')
            {
                //not an escape (a backslash before a newline is a line continuation inside a string)
                builder.Append(c);
                i++;
                continue;
            }

            if (IsHexDigit(css[i + 1]))
            {
                var start = i + 1;
                var end = start;
                while (end < css.Length && end - start < 6 && IsHexDigit(css[end]))
                    end++;

                var hex = css[start..end];
                var codePoint = Convert.ToInt32(hex, 16);
                if (codePoint == 0 || codePoint is >= 0xD800 and <= 0xDFFF || codePoint > 0x10FFFF)
                    codePoint = 0xFFFD;

                //one whitespace after a hex escape belongs to it (\r\n counts as one)
                var next = end;
                if (next < css.Length && css[next] is ' ' or '\t' or '\n' or '\f' or '\r')
                    next += css[next] == '\r' && next + 1 < css.Length && css[next + 1] == '\n' ? 2 : 1;

                if (decode(codePoint))
                    builder.Append(char.ConvertFromUtf32(codePoint));
                else
                    builder.Append('\\').Append(hex).Append(' ');

                i = next;
                continue;
            }

            //backslash + any other character stands for that character
            if (decode(css[i + 1]))
                builder.Append(css[i + 1]);
            else
                builder.Append('\\').Append(css[i + 1]);

            i += 2;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether a URL value (url() argument or image-set() string, as written) stays on this site: a relative URL or a
    /// <c>data:</c> URI. Read the way the browser reads it — CSS escapes decoded, tabs and newlines dropped, surrounding
    /// spaces and control characters trimmed, '\' taken as '/' — so any other scheme and any protocol-relative form is
    /// external.
    /// </summary>
    private static bool IsSameOriginOrData(string rawValue)
    {
        var value = DecodeEscapes(rawValue, _ => true)
            .Replace("\t", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty)
            .Trim(_spaceAndControlCharacters)
            .Replace('\\', '/');

        if (value.StartsWith("//", StringComparison.Ordinal))
            return false;

        var scheme = UrlSchemeRegex().Match(value);
        return !scheme.Success || scheme.Value.Equals("data:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The index just past the ')' that closes the function opened before <paramref name="start"/>
    /// (strings skipped); a function left open ends at the declaration's ';' or '}', or at the end of the text</summary>
    private static int FindFunctionEnd(string css, int start)
    {
        var depth = 1;
        var i = start;
        while (i < css.Length)
        {
            var c = css[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c is '"' or '\'')
            {
                i++;
                while (i < css.Length && css[i] != c)
                    i += css[i] == '\\' ? 2 : 1;
                i++;
                continue;
            }

            if (c == '(')
                depth++;
            else if (c == ')' && --depth == 0)
                return i + 1;
            else if (c is ';' or '}' && depth == 1)
                return i;

            i++;
        }

        return css.Length;
    }

    /// <summary>
    /// Applies the policy to image-set() / image(): under Block every such function goes; under data: / same-origin
    /// only, one whose strings include an external URL goes. A url() inside a kept function is left to the url() pass.
    /// </summary>
    private static string ApplyImageFunctionPolicy(string css, CustomCssUrlPolicy policy, ref bool removedAny)
    {
        var builder = new StringBuilder(css.Length);
        var position = 0;
        var match = ImageFunctionStartRegex().Match(css);
        while (match.Success)
        {
            if (match.Index < position)
            {
                match = match.NextMatch();
                continue;
            }

            var end = FindFunctionEnd(css, match.Index + match.Length);
            var arguments = css[(match.Index + match.Length)..end];

            var remove = policy == CustomCssUrlPolicy.Block
                         || QuotedStringRegex().Matches(arguments).Any(s => !IsSameOriginOrData(s.Groups["val"].Value));

            builder.Append(css, position, match.Index - position);
            if (remove)
                removedAny = true;
            else
                builder.Append(css, match.Index, end - match.Index);

            position = end;
            match = match.NextMatch();
        }

        builder.Append(css, position, css.Length - position);
        return builder.ToString();
    }

    private static string ApplyUrlPolicy(string css, CustomCssUrlPolicy policy, CwCssSanitizeResult result)
    {
        if (policy == CustomCssUrlPolicy.Allow)
            return css;

        var removedAny = false;

        css = ApplyImageFunctionPolicy(css, policy, ref removedAny);

        var output = UrlRegex().Replace(css, match =>
        {
            //Block: every url(); DataAndSameOriginOnly: keep data: and relative/same-origin, strip the rest
            if (policy == CustomCssUrlPolicy.Block || !IsSameOriginOrData(match.Groups["val"].Value))
            {
                removedAny = true;
                return string.Empty;
            }

            return match.Value;
        });

        if (removedAny)
        {
            result.Warnings.Add(policy == CustomCssUrlPolicy.Block
                ? "Removed all url(...) and image-set(...) references (url() policy = Block)."
                : "Removed external url(...) and image-set(...) references (url() policy = data: / same-origin only).");
        }

        return output;
    }

    #endregion

    #region Methods

    public CwCssSanitizeResult Sanitize(string css, CustomCssUrlPolicy urlPolicy)
    {
        var result = new CwCssSanitizeResult();

        if (string.IsNullOrWhiteSpace(css))
            return result;

        //size cap
        if (css.Length > MaxLength)
        {
            css = css.Substring(0, MaxLength);
            result.Warnings.Add($"Custom CSS exceeded {MaxLength:N0} characters and was truncated.");
        }

        //escaped letters (@\69mport, \75rl, \68ttps) are decoded, so every check below sees the keyword the browser sees
        css = DecodeEscapes(css, IsAsciiLetter);

        //always-blocked: markup-breakout characters
        if (css.Contains('<') || css.Contains('>'))
        {
            css = css.Replace("<", string.Empty).Replace(">", string.Empty);
            result.Warnings.Add("Removed '<' / '>' characters (not valid in CSS; markup-injection guard).");
        }

        //always-blocked: @import
        if (ImportRegex().IsMatch(css))
        {
            css = ImportRegex().Replace(css, string.Empty);
            result.Warnings.Add("Removed @import rule(s) — external stylesheet imports are not allowed.");
        }

        //always-blocked: expression()
        if (ExpressionRegex().IsMatch(css))
        {
            css = ExpressionRegex().Replace(css, string.Empty);
            result.Warnings.Add("Removed CSS expression() — script-in-CSS is not allowed.");
        }

        //always-blocked: javascript:/vbscript:
        if (ScriptUriRegex().IsMatch(css))
        {
            css = ScriptUriRegex().Replace(css, string.Empty);
            result.Warnings.Add("Removed javascript:/vbscript: URI(s).");
        }

        //always-blocked: behavior / -moz-binding
        if (BehaviorRegex().IsMatch(css))
        {
            css = BehaviorRegex().Replace(css, string.Empty);
            result.Warnings.Add("Removed behavior/-moz-binding declaration(s).");
        }

        //configurable: url() policy
        css = ApplyUrlPolicy(css, urlPolicy, result);

        result.Css = css.Trim();
        return result;
    }

    #endregion
}
