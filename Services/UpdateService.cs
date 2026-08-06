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
                    return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.4.2";
                }
                catch
                {
                    return "1.4.2";
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ClipboardPro-AutoUpdater/1.0");

                string tagName = "";
                string notes = "New version available with improvements and performance optimizations.";
                string downloadUrl = "";

                // Attempt 1: GitHub API
                try
                {
                    var jsonStr = await client.GetStringAsync(ReleasesUrl);
                    var json = JObject.Parse(jsonStr);

                    tagName = json.Value<string>("tag_name") ?? json.Value<string>("name") ?? "";
                    notes = json.Value<string>("body") ?? notes;

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
                }
                catch
                {
                    // Fallback to HTML Redirect check if GitHub API is rate-limited or fails
                    tagName = await GetLatestTagFromHtmlAsync();
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        downloadUrl = $"https://github.com/{GITHUB_REPO}/releases/download/{tagName}/ClipboardPro-Setup.exe";
                    }
                }

                if (string.IsNullOrEmpty(tagName))
                {
                    result.Error = "Could not check for updates";
                    return result;
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
                    result.Available = false;
                    result.Version = remoteVersionStr;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        private static async Task<string> GetLatestTagFromHtmlAsync()
        {
            try
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(8);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ClipboardPro-AutoUpdater/1.0");

                var resp = await client.GetAsync($"https://github.com/{GITHUB_REPO}/releases/latest");
                if (resp.StatusCode == System.Net.HttpStatusCode.Redirect || 
                    resp.StatusCode == System.Net.HttpStatusCode.MovedPermanently || 
                    resp.StatusCode == System.Net.HttpStatusCode.Found ||
                    resp.StatusCode == (System.Net.HttpStatusCode)302)
                {
                    var location = resp.Headers.Location?.ToString() ?? "";
                    int idx = location.LastIndexOf('/');
                    if (idx >= 0 && idx < location.Length - 1)
                    {
                        return location.Substring(idx + 1);
                    }
                }
            }
            catch { }
            return "";
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

            // Launch installer or updated binary with elevation to prevent Inno Setup CallSpawnServer error
            var psi = new ProcessStartInfo
            {
                FileName = tempFilePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            try
            {
                Process.Start(psi);
            }
            catch
            {
                // Fallback without runas verb if not elevated or user declined prompt
                Process.Start(new ProcessStartInfo { FileName = tempFilePath, UseShellExecute = true });
            }

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
