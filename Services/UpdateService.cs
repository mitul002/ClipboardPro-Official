using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace ClipboardPro.Services
{
    public class UpdateResult
    {
        public bool Available { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public static class UpdateService
    {
        public const string GITHUB_REPO = "mitul002/ClipboardPro-Official";
        public static string ReleasesUrl => $"https://api.github.com/repos/{GITHUB_REPO}/releases/latest";

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var ver = Assembly.GetExecutingAssembly().GetName().Version;
                    return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.4.0";
                }
                catch
                {
                    return "1.4.0";
                }
            }
        }

        public static async Task<UpdateResult> CheckForUpdatesAsync()
        {
            var result = new UpdateResult();
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ClipboardPro-AutoUpdater/1.0");

                var jsonStr = await client.GetStringAsync(ReleasesUrl);
                var json = JObject.Parse(jsonStr);

                string tagName = json.Value<string>("tag_name") ?? json.Value<string>("name") ?? "";
                string notes = json.Value<string>("body") ?? "New version available with improvements and bug fixes.";

                string downloadUrl = "";
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    foreach (var a in assets)
                    {
                        string name = a.Value<string>("name")?.ToLowerInvariant() ?? "";
                        if (name.EndsWith(".exe") || name.EndsWith(".msi") || name.EndsWith(".zip"))
                        {
                            downloadUrl = a.Value<string>("browser_download_url") ?? "";
                            if (name.Contains("clipboardpro") || name.Contains("setup"))
                                break;
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl) && assets.Count > 0)
                    {
                        downloadUrl = assets[0].Value<string>("browser_download_url") ?? "";
                    }
                }

                string remoteVersionStr = tagName.TrimStart('v', 'V');
                if (IsNewerVersion(remoteVersionStr, CurrentVersion))
                {
                    result.Available = true;
                    result.Version = remoteVersionStr;
                    result.Notes = notes;
                    result.DownloadUrl = downloadUrl;
                }
                else
                {
                    result.Version = remoteVersionStr;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        public static async Task DownloadAndInstallAsync(string downloadUrl, IProgress<(long downloaded, long total, int percent)> progress, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL is empty.");

            string tempDir = Path.GetTempPath();
            string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrEmpty(fileName)) fileName = "ClipboardPro-Setup.exe";
            string tempFilePath = Path.Combine(tempDir, fileName);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ClipboardPro-AutoUpdater/1.0");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalDownloaded = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalDownloaded += bytesRead;

                int percent = (totalBytes > 0) ? (int)((totalDownloaded * 100) / totalBytes) : 0;
                progress?.Report((totalDownloaded, totalBytes, percent));
            }

            fileStream.Close();

            // Launch installer or updated binary
            var psi = new ProcessStartInfo
            {
                FileName = tempFilePath,
                UseShellExecute = true
            };
            Process.Start(psi);

            // Shutdown application gracefully
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }

        private static bool IsNewerVersion(string remoteStr, string currentStr)
        {
            try
            {
                var rParts = remoteStr.Split('.').Select(p => int.TryParse(p, out int v) ? v : 0).ToArray();
                var cParts = currentStr.Split('.').Select(p => int.TryParse(p, out int v) ? v : 0).ToArray();

                int maxLen = Math.Max(rParts.Length, cParts.Length);
                for (int i = 0; i < maxLen; i++)
                {
                    int r = i < rParts.Length ? rParts[i] : 0;
                    int c = i < cParts.Length ? cParts[i] : 0;

                    if (r > c) return true;
                    if (r < c) return false;
                }
            }
            catch { }
            return false;
        }
    }
}
