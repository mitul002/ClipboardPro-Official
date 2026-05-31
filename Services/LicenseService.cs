using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ClipboardPro.Services
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ClipboardPro — Enterprise License Service
    //  Security layers: Machine-bound XOR encryption, HMAC-SHA256 tamper
    //  detection, dual-mirror self-healing storage (File + Registry), Zero-Trust
    //  server signature verification, email anti-hijacking, device transfer.
    // ══════════════════════════════════════════════════════════════════════════
    public class LicenseService
    {
        // ── Private Security Constants ──────────────────────────────────────
        // WARNING: These are obfuscated by the build pipeline (Obfuscar + ILProtector).
        // Never ship a build without code protection enabled.
        private static readonly byte[] _licSalt = Encoding.UTF8.GetBytes("Cl1pb0ardPr0_K8#zP5@qL9!mN2&wX_S3cr3t");
        private static readonly string _licenseUrl = "https://cross-tech-admin.vercel.app/api/validate";
        private static readonly string _appName = "ClipboardPro";

        // Offline grace period: 7 days (same as OrbitSwipe)
        private static readonly TimeSpan _offlineGracePeriod = TimeSpan.FromDays(7);

        // Stealth registry key — unique GUID never used by any other app
        private static readonly string _stealthRegPath = @"Software\Classes\CLSID\{C72F4A81-9D03-4E25-AF46-B65D3C900A08}";
        private static readonly string _stealthRegValue = "LicToken";

        private readonly string _licenseFile;
        private readonly string _pendingTransferFile;
        private readonly string _licenseExpiredFile;

        public LicenseService()
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _appName);
            _licenseFile = Path.Combine(dataDir, "license.dat");
            _pendingTransferFile = Path.Combine(dataDir, "pending_transfer.json");
            _licenseExpiredFile = Path.Combine(dataDir, "license_expired.dat");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 1 — Hardware Fingerprinting
        //  Combines MachineGuid (64-bit HKLM) + ComputerName → SHA-256 → 32 hex
        // ══════════════════════════════════════════════════════════════════════
        public static string GetMachineId()
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>();

                // Read 64-bit MachineGuid
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var crypto = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    var guid = crypto?.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(guid)) parts.Add(guid);
                }

                // Read computer name
                parts.Add(Environment.MachineName);

                var raw = parts.Count > 0 ? string.Join("_", parts) : "fallback";
                using (var sha = SHA256.Create())
                {
                    var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                    return Convert.ToHexString(hashBytes).Substring(0, 32).ToLower();
                }
            }
            catch
            {
                return "fallback_safe_id_clipboardpro";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 2 — Cryptographic Engine
        //  XOR cipher (machine-bound key) + HMAC-SHA256 tamper signature
        // ══════════════════════════════════════════════════════════════════════
        private static string XorEncode(string data)
        {
            var key = GetMachineId();
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
                sb.Append((char)(data[i] ^ key[i % key.Length]));
            return Convert.ToBase64String(Encoding.GetEncoding("latin1").GetBytes(sb.ToString()));
        }

        private static string XorDecode(string encoded)
        {
            var rawBytes = Convert.FromBase64String(encoded);
            var decoded = Encoding.GetEncoding("latin1").GetString(rawBytes);
            var key = GetMachineId();
            var sb = new StringBuilder();
            for (int i = 0; i < decoded.Length; i++)
                sb.Append((char)(decoded[i] ^ key[i % key.Length]));
            return sb.ToString();
        }

        private static string HmacSign(string data)
        {
            var machineBytes = Encoding.UTF8.GetBytes(GetMachineId());
            var keyBytes = new byte[_licSalt.Length + machineBytes.Length];
            Buffer.BlockCopy(_licSalt, 0, keyBytes, 0, _licSalt.Length);
            Buffer.BlockCopy(machineBytes, 0, keyBytes, _licSalt.Length, machineBytes.Length);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLower();
            }
        }

        // Timing-safe comparison — prevents timing attacks
        private static bool SecureEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 3 — License Payload Read/Write with Self-Healing Mirror
        //  Primary: %LOCALAPPDATA%\ClipboardPro\license.dat
        //  Mirror:  HKCU\Software\Classes\CLSID\{C72F...}\LicToken
        // ══════════════════════════════════════════════════════════════════════
        private string? EncryptPayload(LicensePayload payload)
        {
            try
            {
                // Generate HMAC over key + email + machine + plan + license_type
                var sigInput = $"{payload.Key}:{payload.Email}:{payload.Machine}:{payload.Plan}:{payload.LicenseType}:{payload.LicensedAt:o}";
                payload.HmacSignature = HmacSign(sigInput);
                var json = JsonConvert.SerializeObject(payload);
                return XorEncode(json);
            }
            catch { return null; }
        }

        private LicensePayload? DecryptAndVerify(string? encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return null;
            try
            {
                var json = XorDecode(encrypted);
                var payload = JsonConvert.DeserializeObject<LicensePayload>(json);
                if (payload == null) return null;

                // Machine binding check — license file copied to another PC will fail
                if (payload.Machine != GetMachineId())
                {
                    Log("License tamper: machine_id mismatch.");
                    return null;
                }

                // HMAC integrity check — also covers license_type if present (backward-compat: try new format first, then old)
                var sigInput = $"{payload.Key}:{payload.Email}:{payload.Machine}:{payload.Plan}:{payload.LicenseType}:{payload.LicensedAt:o}";
                var expectedSig = HmacSign(sigInput);
                if (!SecureEquals(payload.HmacSignature, expectedSig))
                {
                    // Fallback: try old format without LicenseType (for licenses stored before this update)
                    var sigInputLegacy = $"{payload.Key}:{payload.Email}:{payload.Machine}:{payload.Plan}:{payload.LicensedAt:o}";
                    if (!SecureEquals(payload.HmacSignature, HmacSign(sigInputLegacy)))
                    {
                        Log("License tamper: HMAC signature mismatch.");
                        return null;
                    }
                }

                // Expiry check
                if (payload.Expires.HasValue && DateTime.UtcNow > payload.Expires.Value)
                {
                    Log("License expired (time-limited key).");
                    return null;
                }

                return payload;
            }
            catch
            {
                return null;
            }
        }

        public LicensePayload? ReadLicensePayload()
        {
            string? fileData = null;
            string? regData = null;

            // Attempt 1: Read from AppData file
            try
            {
                if (File.Exists(_licenseFile))
                    fileData = File.ReadAllText(_licenseFile);
            }
            catch { }

            // Attempt 2: Read from stealth registry mirror
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_stealthRegPath, false);
                regData = key?.GetValue(_stealthRegValue)?.ToString();
            }
            catch { }

            // Try file first
            var payload = DecryptAndVerify(fileData);
            if (payload != null)
            {
                // If registry mirror is missing/different — self-heal it
                if (fileData != regData) WriteLicensePayload(payload);
                return payload;
            }

            // Self-healing: try registry mirror
            payload = DecryptAndVerify(regData);
            if (payload != null)
            {
                Log("Self-healing: Restoring license.dat from registry mirror.");
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_licenseFile)!);
                    File.WriteAllText(_licenseFile, regData!);
                }
                catch { }
                return payload;
            }

            // Both tampered/missing → wipe both
            if (!string.IsNullOrEmpty(fileData) || !string.IsNullOrEmpty(regData))
            {
                Log("License tamper detected on both mirrors. Wiping.");
                DeactivateLicense();
            }

            return null;
        }

        public void WriteLicensePayload(LicensePayload payload)
        {
            try
            {
                var encrypted = EncryptPayload(payload);
                if (encrypted == null) return;

                // Write to AppData file
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_licenseFile)!);
                    File.WriteAllText(_licenseFile, encrypted);
                }
                catch { }

                // Write to stealth registry mirror
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(_stealthRegPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                    key?.SetValue(_stealthRegValue, encrypted);
                }
                catch { }
            }
            catch { }
        }

        public void DeactivateLicense()
        {
            // Write expiration marker BEFORE deleting license payload
            MarkLicenseExpired("revoked");

            // Delete AppData file
            try { if (File.Exists(_licenseFile)) File.Delete(_licenseFile); }
            catch { }

            // Delete registry mirror
            try
            {
                using var clsid = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID", true);
                clsid?.DeleteSubKeyTree("{C72F4A81-9D03-4E25-AF46-B65D3C900A08}", throwOnMissingSubKey: false);
            }
            catch { }

            Log("License deactivated and wiped from all mirrors.");
        }

        public void MarkLicenseExpired(string reason)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_licenseExpiredFile)!);
                File.WriteAllText(_licenseExpiredFile, reason);
                Log($"License expiration marker written: {reason}");
            }
            catch (Exception ex) { Log($"MarkLicenseExpired error: {ex.Message}"); }
        }

        public void ClearLicenseExpiredMarker()
        {
            try
            {
                if (File.Exists(_licenseExpiredFile))
                {
                    File.Delete(_licenseExpiredFile);
                    Log("License expiration marker cleared successfully.");
                }
            }
            catch (Exception ex) { Log($"ClearLicenseExpiredMarker error: {ex.Message}"); }
        }

        public bool IsLicenseExpiredMarkerSet()
        {
            return File.Exists(_licenseExpiredFile);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 4 — Online API Validation (Zero-Trust)
        //  Verifies server HMAC signature on every valid response.
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ValidationResult> ValidateLicenseOnlineAsync(
            string key,
            string? email = null,
            bool requestTransfer = false)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

                var body = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["key"] = key.Trim().ToUpper(),
                    ["machine_id"] = GetMachineId(),
                    ["app"] = _appName,
                    ["software_id"] = "clipboardpro"
                };
                if (!string.IsNullOrWhiteSpace(email))
                    body["email"] = email.Trim().ToLower();
                if (requestTransfer)
                    body["request_transfer"] = true;

                var json = JsonConvert.SerializeObject(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync(_licenseUrl, content);

                if (!response.IsSuccessStatusCode)
                    return Fail($"Server error {(int)response.StatusCode}. Check your connection.");

                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<ServerResponse>(responseJson);

                if (data == null)
                    return Fail("Invalid server response. Try again.");

                // ── Zero-Trust: Verify Server HMAC Signature ──────────────
                if (data.Valid == true && !requestTransfer)
                {
                    // The server signs: format_msg + ":" + license_type
                    // (e.g. "True:{KEY}:{MACHINE_ID}:{license_type}")
                    // If someone tampers license_type in the response, HMAC will fail.
                    var licType = data.LicenseType ?? "lifetime";
                    var expectedMsg = $"True:{key.Trim().ToUpper()}:{GetMachineId()}:{licType}";
                    var expectedSig = ServerHmac(expectedMsg);

                    if (string.IsNullOrEmpty(data.Signature) || !SecureEquals(data.Signature, expectedSig))
                    {
                        Log("SECURITY ALERT: Server signature mismatch! Rejecting response.");
                        return Fail("Security Error: Invalid server response signature. Contact support.");
                    }
                }
                // ─────────────────────────────────────────────────────────

                return new ValidationResult
                {
                    Valid = data.Valid ?? false,
                    Message = data.Message ?? (data.Valid == true ? "License validated." : "Invalid key."),
                    Plan = data.Plan ?? "Pro",
                    LicenseType = data.LicenseType ?? "lifetime",   // "annual" or "lifetime"
                    CanRequestTransfer = data.CanRequestTransfer ?? false,
                    TransferPending = data.TransferPending ?? false,
                    TransferRequestSubmitted = data.Valid == true && requestTransfer
                };
            }
            catch (TaskCanceledException)
            {
                return Fail("Network error — request timed out. Check your connection.");
            }
            catch (Exception)
            {
                return Fail("Network error — check your internet connection and try again.");
            }
        }

        // Server-side HMAC — must match the SECRET used in validate.js
        private static string ServerHmac(string msg)
        {
            // Uses _licSalt alone (not machine-bound) to match server computation
            using var hmac = new HMACSHA256(_licSalt);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(msg))).ToLower();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 5 — Activate & Store
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ValidationResult> ActivateLicenseAsync(string key, string? email = null)
        {
            key = key.Trim().ToUpper();
            if (string.IsNullOrEmpty(key)) return Fail("Please enter a license key.");

            var result = await ValidateLicenseOnlineAsync(key, email);

            if (result.Valid)
            {
                WriteLicensePayload(new LicensePayload
                {
                    Key = key,
                    Email = email?.Trim().ToLower() ?? "",
                    Machine = GetMachineId(),
                    Plan = result.Plan,
                    LicenseType = result.LicenseType,   // "annual" or "lifetime"
                    LicensedAt = DateTime.UtcNow,
                    LastOnlineCheck = DateTime.UtcNow,
                    Expires = null  // null = lifetime
                });

                // Clear any pending transfer state and license expired marker
                DeletePendingTransferCache();
                ClearLicenseExpiredMarker();
                Log($"License activated: {key[..Math.Min(8, key.Length)]}... type={result.LicenseType}");
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 6 — Device Transfer Request
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ValidationResult> RequestTransferAsync(string key, string email)
        {
            key = key.Trim().ToUpper();
            var result = await ValidateLicenseOnlineAsync(key, email, requestTransfer: true);

            if (result.Valid && result.TransferRequestSubmitted)
            {
                // Cache pending transfer state locally
                SavePendingTransferCache(key, email);
            }

            return result;
        }

        public async Task<ValidationResult> RefreshTransferStatusAsync(string key, string email)
        {
            // Same as normal activation — server will return valid:true if approved
            var result = await ActivateLicenseAsync(key, email);
            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 7 — Silent Background License Check (runs every 6 hours)
        // ══════════════════════════════════════════════════════════════════════
        public async Task<ValidationResult> CheckLicenseOnlineSilentAsync()
        {
            var payload = ReadLicensePayload();
            if (payload == null)
                return Fail("No license active.");

            ValidationResult result;
            try
            {
                result = await ValidateLicenseOnlineAsync(payload.Key, payload.Email);
            }
            catch
            {
                // Network error — check grace period
                var offlineElapsed = DateTime.UtcNow - payload.LastOnlineCheck;
                if (offlineElapsed < _offlineGracePeriod)
                {
                    var daysLeft = (int)(_offlineGracePeriod - offlineElapsed).TotalDays;
                    Log($"Offline launch approved. ~{daysLeft} day(s) remaining on lease.");
                    return new ValidationResult { Valid = true, Message = "Offline approved." };
                }
                else
                {
                    Log("Offline grace period expired. Internet required for license verification.");
                    return new ValidationResult { Valid = false, Message = "Offline grace period expired. Please connect to the internet to verify your license.", OfflineExpired = true };
                }
            }

            // Case A: Explicit revocation/expiry — wipe only when server clearly says so.
            // Do NOT deactivate for machine-mismatch (CanRequestTransfer) or pending
            // transfer (TransferPending) — those are NOT revocations.
            if (!result.Valid && !result.IsNetworkError
                              && !result.TransferPending
                              && !result.CanRequestTransfer)
            {
                Log($"License revoked/expired by server: {result.Message}. Deactivating...");
                DeactivateLicense();
                return new ValidationResult { Valid = false, Message = result.Message, Revoked = true };
            }

            // Case A2: Machine mismatch or transfer pending — do NOT deactivate.
            // The license is still real; user just needs to transfer devices.
            // Fall through to grace-period logic below.
            if (!result.Valid && (result.TransferPending || result.CanRequestTransfer))
            {
                Log($"License machine-mismatch or transfer pending ({result.Message}). Keeping offline grace period.");
            }

            // Case B: Online verified — update timestamp AND refresh plan/license_type from server
            if (result.Valid)
            {
                payload.LastOnlineCheck = DateTime.UtcNow;
                // Refresh plan and license_type in case admin changed them
                if (!string.IsNullOrEmpty(result.Plan))
                    payload.Plan = result.Plan;
                if (!string.IsNullOrEmpty(result.LicenseType))
                    payload.LicenseType = result.LicenseType;
                WriteLicensePayload(payload);
                Log($"License silently verified online. plan={payload.Plan}, type={payload.LicenseType}");
                return new ValidationResult { Valid = true, Message = "License verified online." };
            }

            // Case C: Network error → check offline grace period
            var elapsed = DateTime.UtcNow - payload.LastOnlineCheck;
            if (elapsed < _offlineGracePeriod)
            {
                var daysLeft = (int)(_offlineGracePeriod - elapsed).TotalDays;
                Log($"Silent sync: Offline. ~{daysLeft} day(s) remaining on lease.");
                return new ValidationResult { Valid = true, Message = "Offline approved." };
            }
            else
            {
                Log("Offline grace period expired. Locking app until reconnected.");
                return new ValidationResult { Valid = false, Message = "Offline grace period expired. Please connect to the internet to verify your license.", OfflineExpired = true };
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 8 — License Status
        // ══════════════════════════════════════════════════════════════════════
        public LicenseStatus GetLicenseStatus()
        {
            var payload = ReadLicensePayload();
            var trial = new TrialService();

            if (payload != null)
            {
                // ── 7-day Offline Grace Period Enforcement ────────────────────
                var offlineElapsed = DateTime.UtcNow - payload.LastOnlineCheck;
                if (offlineElapsed > _offlineGracePeriod)
                {
                    Log("Offline lease period expired. Online verification required.");
                    return new LicenseStatus
                    {
                        IsLicensed = false,
                        Plan = null,
                        TrialExpired = trial.IsTrialExpired(),
                        TrialRemaining = trial.GetRemainingTime(),
                        OfflineExpired = true
                    };
                }
                // ─────────────────────────────────────────────────────────────

                var keyPreview = payload.Key.Length >= 8
                    ? payload.Key[..4] + "-****-****-" + payload.Key[^4..]
                    : payload.Key;

                return new LicenseStatus
                {
                    IsLicensed = true,
                    Plan = payload.Plan,
                    LicenseType = payload.LicenseType,
                    KeyPreview = keyPreview,
                    Email = payload.Email,
                    LicensedAt = payload.LicensedAt,
                    TrialExpired = trial.IsTrialExpired(),
                    TrialRemaining = trial.GetRemainingTime()
                };
            }

            return new LicenseStatus
            {
                IsLicensed = false,
                Plan = null,
                TrialExpired = trial.IsTrialExpired(),
                TrialRemaining = trial.GetRemainingTime()
            };
        }

        public bool IsAppAllowed()
        {
            var status = GetLicenseStatus();
            return status.IsLicensed || !status.TrialExpired;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SECTION 9 — Pending Transfer Cache
        //  Persists across dialog close/open cycles
        // ══════════════════════════════════════════════════════════════════════
        public void SavePendingTransferCache(string key, string email)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_pendingTransferFile)!);
                var json = JsonConvert.SerializeObject(new { key, email, transfer_requested = true });
                File.WriteAllText(_pendingTransferFile, json);
            }
            catch { }
        }

        public PendingTransfer? ReadPendingTransferCache()
        {
            try
            {
                if (!File.Exists(_pendingTransferFile)) return null;
                var json = File.ReadAllText(_pendingTransferFile);
                return JsonConvert.DeserializeObject<PendingTransfer>(json);
            }
            catch { return null; }
        }

        public void DeletePendingTransferCache()
        {
            try { if (File.Exists(_pendingTransferFile)) File.Delete(_pendingTransferFile); }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════════
        private static ValidationResult Fail(string message) =>
            new ValidationResult { Valid = false, Message = message };

        private static void Log(string msg)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClipboardPro");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "license.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
            }
            catch { }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Data Models
    // ══════════════════════════════════════════════════════════════════════════
    public class LicensePayload
    {
        public string Key { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Plan { get; set; } = "Pro";
        public string LicenseType { get; set; } = "lifetime";  // "lifetime" or "annual"
        public DateTime LicensedAt { get; set; }
        public DateTime LastOnlineCheck { get; set; }
        public DateTime? Expires { get; set; }   // null = lifetime
        public string HmacSignature { get; set; } = string.Empty;
    }

    public class ValidationResult
    {
        public bool Valid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Plan { get; set; } = "Pro";
        public string LicenseType { get; set; } = "lifetime";  // "lifetime" or "annual"
        public bool CanRequestTransfer { get; set; }
        public bool TransferPending { get; set; }
        public bool TransferRequestSubmitted { get; set; }
        public bool Revoked { get; set; }
        public bool OfflineExpired { get; set; }  // true when 7-day grace period ended
        public bool IsNetworkError => Message.Contains("Network error", StringComparison.OrdinalIgnoreCase)
                                   || Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    public class LicenseStatus
    {
        public bool IsLicensed { get; set; }
        public string? Plan { get; set; }
        public string? LicenseType { get; set; }  // "lifetime" or "annual"
        public string? KeyPreview { get; set; }
        public string? Email { get; set; }
        public DateTime LicensedAt { get; set; }
        public bool TrialExpired { get; set; }
        public TimeSpan TrialRemaining { get; set; }
        public bool OfflineExpired { get; set; }  // true when offline grace period exceeded
    }

    public class ServerResponse
    {
        [JsonProperty("valid")] public bool? Valid { get; set; }
        [JsonProperty("message")] public string? Message { get; set; }
        [JsonProperty("plan")] public string? Plan { get; set; }
        [JsonProperty("license_type")] public string? LicenseType { get; set; }  // "lifetime" or "annual"
        [JsonProperty("signature")] public string? Signature { get; set; }
        [JsonProperty("can_request_transfer")] public bool? CanRequestTransfer { get; set; }
        [JsonProperty("transfer_pending")] public bool? TransferPending { get; set; }
    }

    public class PendingTransfer
    {
        [JsonProperty("key")] public string Key { get; set; } = string.Empty;
        [JsonProperty("email")] public string Email { get; set; } = string.Empty;
        [JsonProperty("transfer_requested")] public bool TransferRequested { get; set; }
    }
}
