using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ClipboardPro.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ClipboardPro — Enterprise Trial Service
    //  4-Layer Stealth Self-Healing Consensus Storage
    //
    //  Layer 1: HKCU\Software\ClipboardPro  (TrialStart)
    //  Layer 2: HKCU\Software\Classes\CLSID\{C72F...-C003}\SysState  (stealth)
    //  Layer 3: %APPDATA%\Microsoft\Protect\clip_state.db  (stealth file)
    //  Layer 4: %LOCALAPPDATA%\ClipboardPro\trial.dat  (standard file)
    //
    //  Consensus: Uses OLDEST valid date → prevents trial reset attacks.
    //  Self-Healing: Repairs any deleted/tampered layer automatically.
    //  Clock-Tamper: Clock rollback instantly triggers expiry lockout.
    // ══════════════════════════════════════════════════════════════════════════
    public class TrialService
    {
        private static readonly string _appName = "ClipboardPro";
        public static readonly int TrialPeriodDays = 30;

        // Set this to true to test the auto-lock feature!
        // When true, the trial has exactly 30 seconds remaining from the moment you launch the application.
        // Once the 30 seconds pass, the app will automatically lock and display the Trial Gate popup.
        // Set this to false for normal operation and production release.
        public static readonly bool IsTestingAutoLock = false;

        private static readonly DateTime _appLaunchTime = DateTime.UtcNow;

        // Registry layer paths
        private static readonly string _regLayer1 = $@"Software\{_appName}";
        private static readonly string _regValue1 = "TrialStart";
        private static readonly string _regLayer2 = @"Software\Classes\CLSID\{C72F4A81-9D03-4E25-AF46-B65D3C900A09}";
        private static readonly string _regValue2 = "SysState";

        // File layer paths
        private readonly string _fileLayer3;   // stealth inside Microsoft folder
        private readonly string _fileLayer4;   // standard AppData file

        public TrialService()
        {
            _fileLayer3 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Protect", "clip_state.db");

            _fileLayer4 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _appName, "trial.dat");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  XOR Cipher — Machine-Bound Hardware Encryption
        // ══════════════════════════════════════════════════════════════════════
        private static string EncryptDate(DateTime date)
        {
            var raw = date.ToUniversalTime().ToString("o");  // ISO 8601 UTC
            var key = LicenseService.GetMachineId();
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
                sb.Append((char)(raw[i] ^ key[i % key.Length]));
            return Convert.ToBase64String(Encoding.GetEncoding("latin1").GetBytes(sb.ToString()));
        }

        private static DateTime? DecryptDate(string? encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return null;
            try
            {
                var rawBytes = Convert.FromBase64String(encrypted);
                var decoded = Encoding.GetEncoding("latin1").GetString(rawBytes);
                var key = LicenseService.GetMachineId();
                var sb = new StringBuilder();
                for (int i = 0; i < decoded.Length; i++)
                    sb.Append((char)(decoded[i] ^ key[i % key.Length]));
                if (DateTime.TryParse(sb.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToUniversalTime();
                return null;
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Layer Readers & Writers
        // ══════════════════════════════════════════════════════════════════════
        private static DateTime? ReadRegistry(string subKey, string valueName)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(subKey, false);
                return DecryptDate(key?.GetValue(valueName)?.ToString());
            }
            catch { return null; }
        }

        private static void WriteRegistry(string subKey, string valueName, DateTime date)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
                key?.SetValue(valueName, EncryptDate(date));
            }
            catch { }
        }

        private static DateTime? ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                // Temporarily unhide if hidden
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.Hidden) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.Hidden);
                var text = File.ReadAllText(path);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
                return DecryptDate(text);
            }
            catch { return null; }
        }

        private static void WriteFile(string path, DateTime date)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Unhide before writing
                if (File.Exists(path))
                {
                    var attrs = File.GetAttributes(path);
                    if ((attrs & FileAttributes.Hidden) != 0)
                        File.SetAttributes(path, attrs & ~FileAttributes.Hidden);
                }
                File.WriteAllText(path, EncryptDate(date));
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Core: Consensus Logic — The Heart of the Trial System
        // ══════════════════════════════════════════════════════════════════════
        public DateTime GetTrialStartDate()
        {
            var now = DateTime.UtcNow;

            var d1 = ReadRegistry(_regLayer1, _regValue1);
            var d2 = ReadRegistry(_regLayer2, _regValue2);
            var d3 = ReadFile(_fileLayer3);
            var d4 = ReadFile(_fileLayer4);

            // If ALL layers are missing → first run → start trial now
            if (d1 == null && d2 == null && d3 == null && d4 == null)
            {
                var startNow = now;
                SyncAllLayers(startNow);
                return startNow;
            }

            // Collect valid candidates (reject future-dated values — clock tampering)
            var oldest = DateTime.MaxValue;
            bool repairNeeded = false;

            void Evaluate(DateTime? d)
            {
                if (d.HasValue)
                {
                    // Clock tamper guard: reject dates more than 1 day in the future
                    if (d.Value <= now.AddDays(1) && d.Value < oldest)
                        oldest = d.Value;
                }
                else repairNeeded = true;
            }

            Evaluate(d1);
            Evaluate(d2);
            Evaluate(d3);
            Evaluate(d4);

            // All dates were future-dated (clock tamper) → instant expiry lockout
            if (oldest == DateTime.MaxValue)
                return now.AddDays(-(TrialPeriodDays + 1));

            // Clock rollback attack: if current time is BEFORE our oldest recorded start
            if (now < oldest)
                return now.AddDays(-(TrialPeriodDays + 1));

            // Auto-repair any missing/different layers with the consensus oldest date
            if (repairNeeded || d1 != oldest || d2 != oldest || d3 != oldest || d4 != oldest)
                SyncAllLayers(oldest);

            // Return the actual oldest consensus start date (no testing offset)
            return oldest;
        }

        private void SyncAllLayers(DateTime date)
        {
            WriteRegistry(_regLayer1, _regValue1, date);
            WriteRegistry(_regLayer2, _regValue2, date);
            WriteFile(_fileLayer3, date);
            WriteFile(_fileLayer4, date);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Public API
        // ══════════════════════════════════════════════════════════════════════
        public bool IsTrialExpired()
        {
            if (IsTestingAutoLock)
            {
                return (DateTime.UtcNow - _appLaunchTime).TotalSeconds > 30;
            }
            var start = GetTrialStartDate();
            return (DateTime.UtcNow - start).TotalDays > TrialPeriodDays;
        }

        public int GetRemainingDays()
        {
            if (IsTestingAutoLock)
            {
                var elapsed = DateTime.UtcNow - _appLaunchTime;
                var remainingSec = 30.0 - elapsed.TotalSeconds;
                return Math.Max(0, (int)Math.Ceiling(remainingSec / 86400.0));
            }
            else
            {
                var start = GetTrialStartDate();
                var used = (DateTime.UtcNow - start).TotalDays;
                return Math.Max(0, (int)Math.Ceiling(TrialPeriodDays - used));
            }
        }

        public TimeSpan GetRemainingTime()
        {
            if (IsTestingAutoLock)
            {
                var elapsed = DateTime.UtcNow - _appLaunchTime;
                var remaining = TimeSpan.FromSeconds(30) - elapsed;
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
            else
            {
                var start = GetTrialStartDate();
                var elapsed = DateTime.UtcNow - start;
                var trialSpan = TimeSpan.FromDays(TrialPeriodDays);
                var remaining = trialSpan - elapsed;
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }

        public double GetTrialPercentUsed()
        {
            if (IsTestingAutoLock)
            {
                var elapsed = (DateTime.UtcNow - _appLaunchTime).TotalSeconds;
                return Math.Min(100.0, Math.Max(0.0, (elapsed / 30.0) * 100.0));
            }
            else
            {
                var start = GetTrialStartDate();
                var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                var total = TrialPeriodDays * 86400.0;
                return Math.Min(100.0, Math.Max(0.0, (elapsed / total) * 100.0));
            }
        }
    }
}
