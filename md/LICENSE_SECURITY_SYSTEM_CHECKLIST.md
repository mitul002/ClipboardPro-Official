# 🛡️ ClipboardPro — License & Security System Checklist
> **Source:** Ported from `OrbitSwipe MODULER` — fully battle-tested system  
> **Reference Files:** `core/license.py`, `core/constants.py`, `ui/dialogs.py`, `ui/settings.py`, `api/validate.js`  
> **Status:** Use this as the single source of truth when implementing in ClipboardPro

---

## 📋 MASTER IMPLEMENTATION CHECKLIST

---

## ✅ SECTION 1 — CORE CONSTANTS & CONFIGURATION
> **OrbitSwipe ref:** `core/constants.py`

- [ ] Define `APP_NAME = "ClipboardPro"` — used in all registry paths & file names
- [ ] Define `APP_VERSION = "1.0.0"` — update before every release
- [ ] Define `APPDATA_DIR` using `%LOCALAPPDATA%\ClipboardPro`
- [ ] Define `LICENSE_FILE = APPDATA_DIR / "license.dat"`
- [ ] Define `TRIAL_FILE = APPDATA_DIR / "trial.dat"`
- [ ] Define `LICENSE_URL` pointing to ClipboardPro Vercel API endpoint
- [ ] Set `TRIAL_DAYS = 30` (change to `1/24` for 1-hour test loop during dev)
- [ ] Set `LOG_FILE = APPDATA_DIR / "crash.log"` for error tracing

> ⚠️ **IMPORTANT:** Never hard-code the `LICENSE_URL` in plain text inside a production build — store it in a constants file that PyArmor will obfuscate.

---

## ✅ SECTION 2 — HARDWARE FINGERPRINTING (Machine ID)
> **OrbitSwipe ref:** `core/license.py → _machine_id()`

- [ ] Read `MachineGuid` from `HKLM\SOFTWARE\Microsoft\Cryptography` (64-bit key)
- [ ] Read `ComputerNameW` via `ctypes.windll.kernel32.GetComputerNameW`
- [ ] Combine both parts with `_` separator
- [ ] SHA-256 hash the combined string → take first 32 hex characters
- [ ] Use fallback string `"fallback"` if both reads fail
- [ ] `_machine_id()` must return **same value** on every call on the same PC
- [ ] Machine ID must be **hardware-bound** — survives app reinstall, registry clears, `.dat` file deletions

> ⚠️ **Security Note:** The Machine ID is the foundation of ALL security. Never expose it in logs or UI.

---

## ✅ SECTION 3 — CRYPTOGRAPHIC STORAGE ENGINE
> **OrbitSwipe ref:** `core/license.py → _xor_encode / _xor_decode / _hmac_sign / _write_lic_file / _read_lic_file`

- [ ] Define a private `_LIC_SALT` bytes constant (e.g. `b"ClipPr0_X9#mK$secret..."`) — never expose
- [ ] Implement `_hmac_sign(data)`:
  - Key = `_LIC_SALT + machine_id().encode()`
  - Use `hmac.new(key, data.encode(), hashlib.sha256).hexdigest()`
- [ ] Implement `_xor_encode(data)`:
  - XOR each character with `machine_id()[i % len(machine_id)]`
  - Base64-encode the result (latin-1 encoding)
- [ ] Implement `_xor_decode(encoded)`:
  - Base64-decode, then reverse XOR using machine_id
- [ ] Implement `_write_lic_file(path, payload)`:
  - JSON-serialize payload → HMAC-sign → XOR-encode → write `{"d": encoded, "s": sig}`
- [ ] Implement `_read_lic_file(path)`:
  - Read blob → XOR-decode → HMAC verify with `hmac.compare_digest()` → return payload or `None`
  - Log "tamper detected" if signature fails

> ⚠️ **Security Note:** Always use `hmac.compare_digest()` — NEVER use `==` for signature comparison (timing attack prevention).

---

## ✅ SECTION 4 — 4-LAYER STEALTH TRIAL STORAGE (Self-Healing)
> **OrbitSwipe ref:** `core/license.py → _get_stealth_locations() / _get_trial_info()`

- [ ] Implement **Layer 1** — Obvious Registry:
  - Path: `HKCU\Software\ClipboardPro`
  - Value name: `TrialStart`
  - Encoding: Hardware-bound XOR
- [ ] Implement **Layer 2** — Deep Stealth Registry (CLSID-like path):
  - Path: `HKCU\Software\Classes\CLSID\{SOME-UNIQUE-GUID-001}\SysState`
  - Use a fresh GUID — do **NOT** reuse OrbitSwipe's GUID
  - Encoding: Hardware-bound XOR
- [ ] Implement **Layer 3** — Stealth File (inside Microsoft folder):
  - Path: `%APPDATA%\Microsoft\Protect\clip_state.db`
  - Use `_write_lic_file` / `_read_lic_file` for HMAC protection
- [ ] Implement **Layer 4** — Local App Data File:
  - Path: `TRIAL_FILE` (`%LOCALAPPDATA%\ClipboardPro\trial.dat`)
  - Use `_write_lic_file` / `_read_lic_file`
- [ ] Implement **consensus logic** in `_get_trial_info()`:
  - Read from ALL 4 locations
  - Collect all valid timestamps
  - **Use the OLDEST (minimum) date** — prevents reset attacks
  - Reject any date more than `+86400s` in the future (clock tamper check)
- [ ] After consensus: **sync all 4 layers** with the oldest date (self-healing)
- [ ] Return dict: `{first_run, days_used, days_left, sec_left, expired}`

> ✅ **Why 4 layers?** A user deleting 1-2 locations won't break it — consensus rebuilds from remaining valid locations.

---

## ✅ SECTION 5 — LICENSE PAYLOAD STORAGE (Self-Healing Mirror)
> **OrbitSwipe ref:** `core/license.py → _write_license_payload / _read_license_payload`

- [ ] Choose a **fresh CLSID GUID** for license registry key — do NOT reuse OrbitSwipe's `{B54F3741-...-C002}`
- [ ] Implement `_write_license_payload(payload)`:
  - Write to `LICENSE_FILE` using `_write_lic_file()`
  - **Mirror** to Registry: `HKCU\Software\Classes\CLSID\{CLIPBOARDPRO-GUID}\LicToken`
  - Registry value = JSON blob `{"d": xor_encoded, "s": hmac_sig}`
- [ ] Implement `_read_license_payload()`:
  - **Attempt 1:** Read from `LICENSE_FILE`
  - **Attempt 2 (Self-Healing):** If file missing/tampered → read from Registry mirror
  - If registry valid → restore `LICENSE_FILE` from registry (log "Self-healing: Restoring from registry")
  - Return `None` if both fail

---

## ✅ SECTION 6 — ONLINE LICENSE VALIDATION (Vercel API)
> **OrbitSwipe ref:** `core/license.py → _validate_license_online()`

- [ ] POST to `LICENSE_URL` with JSON body: `{"key": key, "machine_id": machine_id, "app": "ClipboardPro"}`
- [ ] Include `email` field if provided
- [ ] Include `request_transfer: true` flag if transfer request mode
- [ ] Set `timeout=8` seconds
- [ ] **CRITICAL — Zero-Trust Signature Verification:**
  - Check server response `signature` field
  - Reconstruct expected sig: `HMAC-SHA256(_LIC_SALT, f"True:{key}:{machine_id}")`
  - Use `hmac.compare_digest(sig, expected_sig)` — reject if mismatch
  - Return `{valid: False, message: "Security Error: Invalid server response."}` on mismatch
- [ ] Handle network errors gracefully → return `{valid: False, message: "Network error..."}`
- [ ] Do NOT crash the app on connection failure

---

## ✅ SECTION 7 — LICENSE ACTIVATION FLOW
> **OrbitSwipe ref:** `core/license.py → activate_license()`

- [ ] Strip and uppercase the input key
- [ ] Validate key is non-empty
- [ ] Call `_validate_license_online(key, email=email)`
- [ ] On success, call `_write_license_payload({...})` with:
  - `key` (uppercase, stripped)
  - `machine` = `_machine_id()`
  - `plan` from server response (e.g. `"Pro"`)
  - `expires` from server (0 = lifetime)
  - `activated_at` = `time.time()`
  - `last_online_check` = `time.time()`
- [ ] Return the result dict from server

---

## ✅ SECTION 8 — OFFLINE LICENSE VALIDATION
> **OrbitSwipe ref:** `core/license.py → _validate_license_offline()`

- [ ] Read payload via `_read_license_payload()`
- [ ] Verify `data["key"] == key` (key match)
- [ ] Verify `data["machine"] == _machine_id()` (machine binding)
- [ ] Check `data["expires"]` — if non-zero and past, reject as expired
- [ ] Return `{valid: True, plan: ...}` if all checks pass

---

## ✅ SECTION 9 — GET LICENSE STATUS (App-wide State)
> **OrbitSwipe ref:** `core/license.py → get_license_status()`

- [ ] Read license payload via `_read_license_payload()`
- [ ] Read trial info via `_get_trial_info()`
- [ ] If payload exists and machine matches and not expired:
  - Return `{licensed: True, plan: ..., key_preview: "XXXX-****-****-XXXX", trial: trial}`
- [ ] Otherwise return `{licensed: False, plan: None, key_preview: "", trial: trial}`
- [ ] `key_preview`: mask middle groups — show first 4 and last 4 chars only

---

## ✅ SECTION 10 — APP ACCESS GATE (Boot Check)
> **OrbitSwipe ref:** `core/license.py → is_app_allowed()` | `main.py lines 51–54`

- [ ] Implement `is_app_allowed()`:
  - Returns `True` if `get_license_status()["licensed"]` OR trial not expired
  - Returns `False` only when BOTH license is missing/invalid AND trial is expired
- [ ] In `main.py` / app startup — run gate BEFORE creating any main window:
  ```python
  if not is_app_allowed():
      gate = TrialGateDlg()
      if gate.exec() != QDialog.Accepted or not is_app_allowed():
          sys.exit(0)
  ```
- [ ] App must NOT start if gate is dismissed without activating a valid license

---

## ✅ SECTION 11 — SILENT BACKGROUND LICENSE SYNC
> **OrbitSwipe ref:** `core/license.py → check_license_online_silent()`

- [ ] Call this in a background thread on every app startup (after main window loads)
- [ ] Implement 3 cases:
  - **Case A — Revoked:** `valid=False` and not a network error → call `deactivate_license()` → return `{status: "revoked"}`
  - **Case B — Verified:** `valid=True` → update `last_online_check` timestamp → return `{status: "verified"}`
  - **Case C — Offline:** Network error → **do NOT revoke** → return `{status: "offline_approved"}`
- [ ] If revoked: show notification/dialog to user explaining their license was revoked
- [ ] Do NOT block the main thread — run in `QThread`

---

## ✅ SECTION 12 — LICENSE DEACTIVATION
> **OrbitSwipe ref:** `core/license.py → deactivate_license()`

- [ ] Delete `LICENSE_FILE` if it exists
- [ ] Delete Registry mirror key: `HKCU\Software\Classes\CLSID\{CLIPBOARDPRO-GUID}`
- [ ] Use `winreg.DeleteKey()` — handle `FileNotFoundError` silently
- [ ] Log success/failure of each step
- [ ] After deactivation, UI must refresh to show trial status

---

## ✅ SECTION 13 — TRIAL EXPIRED GATE DIALOG (UI)
> **OrbitSwipe ref:** `ui/dialogs.py → TrialGateDlg`

- [ ] Dialog must be `FramelessWindowHint + WindowStaysOnTopHint`
- [ ] Background must be `WA_TranslucentBackground`
- [ ] Show app icon, title ("ClipboardPro Trial Ended"), description
- [ ] Display `{TRIAL_DAYS}-day trial has reached its limit`
- [ ] Input fields:
  - License Key field (`XXXX-XXXX-XXXX-XXXX` placeholder, max 36 chars)
  - Email Address field (for anti-hijacking)
- [ ] Buttons:
  - `⚡ ACTIVATE PRO` — primary activation button
  - `🔄 REQUEST TRANSFER` — hidden by default, shown when `can_request_transfer=True`
- [ ] `REQUEST TRANSFER` button transforms to green `🔄 REFRESH STATUS` after successful request
- [ ] Message label: colored feedback (green=success, red=error, yellow=pending)
- [ ] **Top-right controls:** Minimize `—` and Close `✕` buttons
- [ ] **Drag to move:** Implement `mousePressEvent / mouseMoveEvent / mouseReleaseEvent`
- [ ] **Fade-in animation:** `QPropertyAnimation` on `windowOpacity` (0 → 1, 220ms)
- [ ] **Minimize animation:** fade out then `showMinimized()`
- [ ] **Auto-prefill:** On open, check for `pending_transfer.json` → prefill key/email and show green REFRESH button
- [ ] Buy link at bottom: `Don't have a key? Buy at clipboardpro.vercel.app`
- [ ] Run activation in `QThread` — never block the UI thread

---

## ✅ SECTION 14 — SETTINGS PANEL LICENSE TAB (UI)
> **OrbitSwipe ref:** `ui/settings.py → _build_license_tab()`

- [ ] Status card showing:
  - `✅ Licensed — Pro Plan` (green) when active
  - `⏳ Free Trial — Xd Xh Xm remaining` (purple) during trial
  - `🔒 Trial Expired` (red) when expired
  - Key preview: `XXXX-****-****-XXXX`
  - Trial progress bar (percentage used)
- [ ] License Key input field
- [ ] Email Address input field
- [ ] `✅ Activate License` button
- [ ] `🔄 Request Transfer` button (hidden by default)
- [ ] `Deactivate` button (red outlined, shown only when licensed)
- [ ] Feedback message label (colored)
- [ ] **Auto-prefill:** Same `pending_transfer.json` prefill logic as Trial Gate
- [ ] Refresh state after activation: call `_update()` to refresh card display

---

## ✅ SECTION 15 — DEVICE SWITCHING / LICENSE TRANSFER SYSTEM
> **OrbitSwipe ref:** `api/validate.js`, `core/license.py`, `ui/dialogs.py`, `ui/settings.py`

### Client Side:
- [ ] When activation fails with `can_request_transfer: true` → show `REQUEST TRANSFER` button
- [ ] `REQUEST TRANSFER` call: POST with `request_transfer: true` flag
- [ ] On successful request:
  - Save `pending_transfer.json` to `APPDATA_DIR` with `{key, email, transfer_requested: true}`
  - Transform button to green `🔄 REFRESH STATUS`
  - Show `"Transfer request submitted. Wait for admin approval (24h)."`
- [ ] On `REFRESH STATUS` click → call normal activate → detect result:
  - `transfer_pending: true` → show `⏳ Transfer request pending admin approval`
  - Rejection detected (machine mismatch + refresh mode) → delete `pending_transfer.json` → reset button to purple `REQUEST TRANSFER`
  - `valid: true` → delete `pending_transfer.json` → activate → open app

### Server Side (Vercel/Firebase):
- [ ] When `machine_id` mismatch + `email` matches + `request_transfer=true`:
  - Write `{transfer_requested: true, requested_machine_id: ..., requested_at: serverTimestamp}` to Firestore
  - Return `{valid: true, message: "Transfer request submitted..."}`
- [ ] When `machine_id` mismatch + `transfer_requested === true` in DB (pending):
  - Return `{valid: false, transfer_pending: true, message: "Transfer pending approval..."}`
- [ ] Admin Approve endpoint: update `machine_id` to `requested_machine_id`, clear transfer fields
- [ ] Admin Reject endpoint: delete `transfer_requested`, `requested_machine_id`, `requested_at` fields
- [ ] Admin Dashboard: show pending requests widget, email column for identity verification

---

## ✅ SECTION 16 — EMAIL ANTI-HIJACKING SYSTEM
> **OrbitSwipe ref:** `api/validate.js lines 45–50`

- [ ] On first activation: save `email` to Firestore key document
- [ ] On all subsequent activations: verify `data.email === request.email`
- [ ] If email mismatch: return `{valid: false, message: "License bound to different email"}`
- [ ] Even if someone steals the key, they cannot use it without the registered email
- [ ] Server stores: `{machine_id, email, activated_at, plan}` per key document

---

## ✅ SECTION 17 — VERCEL SERVERLESS BACKEND
> **OrbitSwipe ref:** `orbitswipe-web/api/`

- [ ] Create new Vercel project for `clipboardpro` — **separate from OrbitSwipe**
- [ ] Create new Firebase project — separate Firestore collection `clipboardpro_keys`
- [ ] Set environment variables in Vercel:
  - `FIREBASE_SERVICE_ACCOUNT` = Firebase service account JSON (stringified)
  - `CLIPBOARDPRO_SECRET` = new unique HMAC salt (generate fresh, NOT OrbitSwipe's salt)
  - `ADMIN_SECRET` = secret for admin API calls
- [ ] API endpoints to create:
  - `POST /api/validate` — main validation (port from `validate.js`)
  - `GET /api/admin-get-keys` — admin dashboard
  - `POST /api/admin-create-key` — create new license key
  - `DELETE /api/admin-delete-key` — revoke key
  - `POST /api/admin-approve-transfer` — approve device switch
  - `POST /api/admin-reject-transfer` — reject device switch
  - `POST /api/lemon-webhook` — LemonSqueezy payment webhook (auto-create keys)
- [ ] HMAC signature format: `HMAC-SHA256(SECRET, f"True:{key}:{machine_id}")`
- [ ] Server returns `signature` field on every valid response

---

## ✅ SECTION 18 — KEY GENERATION TOOL
> **OrbitSwipe ref:** `tools/key_gen.py`

- [ ] Key format: `XXXX-XXXX-XXXX-XXXX` (4 groups of 4 uppercase alphanumerics)
- [ ] Character set: uppercase letters + digits, **remove** `O, 0, I, 1` (confusion prevention)
- [ ] Generate keys → add to Firestore `clipboardpro_keys` collection
- [ ] Each key document structure:
  ```json
  {
    "plan": "Pro",
    "machine_id": null,
    "email": null,
    "activated_at": null,
    "expires": 0
  }
  ```
- [ ] `expires: 0` = lifetime license, non-zero = Unix timestamp expiry

---

## ✅ SECTION 19 — CODE SECURITY (PyArmor Obfuscation)
> **OrbitSwipe ref:** `.pyarmor/`, `tools/build_protected.py`, `build.py`

- [ ] Install PyArmor: `pip install pyarmor`
- [ ] Register PyArmor license (Basic or Pro tier)
- [ ] Configure PyArmor rules:
  - Enable bytecode encryption
  - Enable VM obfuscation
  - Enable function obfuscation
- [ ] Protect these files FIRST before PyInstaller build:
  - `core/license.py` — most critical
  - `core/constants.py` — contains salt reference
  - All other `core/` and `ui/` modules
- [ ] Build order: `pyarmor pack → pyinstaller`
- [ ] Test protected build: decompilers (`uncompyle6`, `pycdc`) should fail
- [ ] Ensure PyArmor runtime `.dll` / `.pyd` is included in PyInstaller bundle

---

## ✅ SECTION 20 — SECURITY AUDIT CHECKLIST (Before Release)

### Code Security:
- [ ] `_LIC_SALT` is unique to ClipboardPro (not OrbitSwipe's salt)
- [ ] All CLSID GUIDs are unique (generated fresh for ClipboardPro)
- [ ] No salt or secret stored in plain text in final binary (PyArmor handles this)
- [ ] No hardcoded debug keys in production code
- [ ] `TRIAL_DAYS` is set back to `30` (not `1/24` test value)

### Trial Security:
- [ ] All 4 stealth storage layers work independently
- [ ] Self-healing consensus logic tested: delete 2 layers → app recovers
- [ ] Clock tamper check working: setting clock back doesn't reset trial
- [ ] Trial expiry correctly blocks app launch

### License Security:
- [ ] HMAC signature verification fails on tampered files
- [ ] XOR encoding is machine-bound (different machine → decode fails)
- [ ] Registry mirror self-healing tested
- [ ] Machine ID binding prevents license file copy to another PC
- [ ] Deactivation clears both file and registry

### Online Security:
- [ ] Server signature (Zero-Trust) verification passes for valid responses
- [ ] Server signature verification REJECTS tampered/faked responses
- [ ] Email anti-hijacking blocks wrong-email activations
- [ ] Revocation tested: admin deletes key → silent sync deactivates locally
- [ ] Offline approval works: no network = app still runs if previously licensed

### UI/UX:
- [ ] Trial Gate dialog appears correctly when trial expires
- [ ] Gate cannot be bypassed (close = app exits)
- [ ] License key activation flow works end-to-end
- [ ] Transfer request flow works (request → pending → approve/reject → refresh)
- [ ] Settings panel License tab shows correct state
- [ ] Deactivation works from Settings

---

## 🔑 QUICK REFERENCE — KEY DIFFERENCES FROM ORBITSWIPE

| Item | OrbitSwipe Value | ClipboardPro (Required Change) |
|------|-----------------|-------------------------------|
| `APP_NAME` | `"OrbitSwipe"` | `"ClipboardPro"` |
| `APPDATA_DIR` | `%LOCALAPPDATA%\OrbitSwipe` | `%LOCALAPPDATA%\ClipboardPro` |
| `LICENSE_URL` | `https://orbitswipe.vercel.app/api/validate` | **New Vercel URL** |
| `_LIC_SALT` | `b"0rb1tSw1p3_X9#mK$vP2@qL7!nR4&wZ"` | **New unique salt** |
| Trial GUID (Layer 2) | `{B54F3741-5B07-4214-BE35-A43A6B64C001}` | **New GUID** |
| License GUID (Mirror) | `{B54F3741-5B07-4214-BE35-A43A6B64C002}` | **New GUID** |
| Firestore Collection | `orbitswipe_keys` | `clipboardpro_keys` |
| Server SECRET env var | `ORBITSWIPE_SECRET` | `CLIPBOARDPRO_SECRET` |
| Stealth file | `sys_info.db` | `clip_state.db` (new name) |
| App identifier in API | `"OrbitSwipe"` | `"ClipboardPro"` |

---

## 📁 RECOMMENDED FILE STRUCTURE FOR CLIPBOARDPRO

```
clipboardpro/
├── main.py                          ← App entry + license gate
├── core/
│   ├── constants.py                 ← APP_NAME, URLs, paths, TRIAL_DAYS
│   ├── license.py                   ← Full security engine (port from OrbitSwipe)
│   ├── config.py                    ← App config read/write
│   └── utils.py                     ← _log(), helpers
├── ui/
│   ├── dialogs.py                   ← TrialGateDlg + other dialogs
│   ├── settings.py                  ← SettingsDlg with License tab
│   └── ...
├── tools/
│   └── key_gen.py                   ← License key generator
└── clipboardpro-web/
    ├── api/
    │   ├── validate.js              ← Main validation endpoint
    │   ├── admin-get-keys.js
    │   ├── admin-create-key.js
    │   ├── admin-delete-key.js
    │   ├── admin-approve-transfer.js
    │   ├── admin-reject-transfer.js
    │   └── lemon-webhook.js         ← Auto-create key on payment
    └── vercel.json
```

---

## ⚡ IMPLEMENTATION PRIORITY ORDER

1. **`core/constants.py`** — Set all names, paths, URLs, TRIAL_DAYS
2. **`core/license.py`** — Port entire file, update salt + GUIDs + app name
3. **`clipboardpro-web/api/validate.js`** — Port + update collection + secret
4. **`main.py`** — Add `is_app_allowed()` gate at startup
5. **`ui/dialogs.py → TrialGateDlg`** — Port trial gate dialog
6. **`ui/settings.py → _build_license_tab`** — Port settings license tab
7. **`tools/key_gen.py`** — Port key generator
8. **Test end-to-end** — Trial → Expiry → Gate → Activate → Silent Sync → Transfer → Revoke
9. **PyArmor protection** — Wrap before final build
10. **Security audit** — Run full Section 20 checklist

---

*Generated by Antigravity IDE — Based on full analysis of OrbitSwipe MODULER source code — May 2026*
