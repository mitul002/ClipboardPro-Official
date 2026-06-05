using System.Text.RegularExpressions;
using ClipboardPro.Models;

namespace ClipboardPro.Services
{
    public static class ContentDetectionService
    {
        private static readonly Regex UrlRegex =
            new(@"^(https?://|www\.)[a-zA-Z0-9.-]+\.[a-zA-Z]{2,15}(\/\S*)?$|^[a-zA-Z0-9.-]+\.(com|net|org|edu|gov|io|ai|me|info|sh|app|dev|xyz|so|online|site|tech)\b(\/\S*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EmailRegex =
            new(@"^[\w\.-]+@[\w\.-]+\.\w{2,}$", RegexOptions.Compiled);

        private static readonly Regex PhoneRegex =
            new(@"^(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4,9}$", RegexOptions.Compiled);

        private static readonly Regex CodeRegex =
            new(@"(^[\s\r\n]*(def |import |from |function |var |const |let |class |public |private |protected |internal |namespace |using |using static |#include |#define |#if |#endif |extern |SELECT |INSERT |UPDATE |DELETE |CREATE |ALTER |DROP |GRANT |REVOKE ))|(\{[\s\r\n]*[""'][^""']+[""'][\s\r\n]*:)|(<(html|div|script|style|body|head|span|p|a|ul|li|table|tr|td|img|form|input|button|link|meta|iframe))|(\b(if|for|while|foreach|switch|try|catch|finally)\s*\(.*\)\s*\{)|(\b(bool|int|string|var|float|double|char|long|byte|decimal)\s+[a-zA-Z_]\w*\s*[=;])",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JsonRegex =
            new(@"^[\s\r\n]*[\{\[]", RegexOptions.Compiled);

        private static readonly Regex HexColorRegex =
            new(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled);

        private static readonly Regex PathRegex =
            new(@"(^[a-zA-Z]:\\.*)|(^\/([^\/\0]+\/)+[^\/\0]*)|(^[\\\/]{2}[^\/]+\/.*)", RegexOptions.Compiled);

        public static ClipboardItemType Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return ClipboardItemType.Text;

            var trimmed = text.Trim().Trim('"');

            // 1. High-priority strict matches
            if (UrlRegex.IsMatch(trimmed))   return ClipboardItemType.URL;
            if (EmailRegex.IsMatch(trimmed)) return ClipboardItemType.Email;
            
            // 2. Phone numbers (often short, so check carefully)
            if (PhoneRegex.IsMatch(trimmed)) return ClipboardItemType.Phone;
            
            // 3. Colors
            if (HexColorRegex.IsMatch(trimmed)) return ClipboardItemType.Color;

            // 4. Code / JSON detection (Length check to avoid false positives on short text)
            if (trimmed.Length > 5)
            {
                if (JsonRegex.IsMatch(trimmed))
                {
                    // Strict JSON check for small snippets to avoid marking "[1] task" as code
                    if (trimmed.Length < 50 && (trimmed.StartsWith("[") || trimmed.StartsWith("{")))
                    {
                        try { Newtonsoft.Json.Linq.JToken.Parse(trimmed); return ClipboardItemType.Code; }
                        catch { /* Fall through to other checks */ }
                    }
                    else if (trimmed.Length >= 50)
                    {
                        return ClipboardItemType.Code;
                    }
                }

                if (CodeRegex.IsMatch(trimmed)) return ClipboardItemType.Code;
            }

            // 5. Paths
            if (PathRegex.IsMatch(trimmed))
            {
                try
                {
                    if (System.IO.Directory.Exists(trimmed)) return ClipboardItemType.Directory;
                }
                catch { }
            }

            return ClipboardItemType.Text;
        }
    }
}
