package com.clipboardpro.vault.service

import android.content.Context
import android.os.Build
import android.provider.Settings
import android.util.Base64
import android.util.Log
import com.google.gson.Gson
import com.google.gson.annotations.SerializedName
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

class LicenseService(private val context: Context) {

    companion object {
        private const val TAG = "LicenseService"
        private val LIC_SALT = "Cl1pb0ardPr0_K8#zP5@qL9!mN2&wX_S3cr3t".toByteArray(Charsets.UTF_8)
        private const val LICENSE_URL = "https://cross-tech-admin.vercel.app/api/validate"
        private const val APP_NAME = "Clipboard Vault"
        private const val OFFLINE_GRACE_PERIOD_MS = 7L * 24 * 60 * 60 * 1000 // 7 days

        fun getMachineId(context: Context): String {
            return try {
                val androidId = Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID) ?: "0000000000000000"
                val model = Build.MODEL ?: "UnknownModel"
                val raw = "${androidId}_${model}"
                val digest = MessageDigest.getInstance("SHA-256").digest(raw.toByteArray(Charsets.UTF_8))
                digest.joinToString("") { "%02x".format(it) }.substring(0, 32).lowercase()
            } catch (e: Exception) {
                Log.e(TAG, "getMachineId error: ${e.message}")
                "fallback_safe_id_clipboardvault"
            }
        }
    }

    private val licenseFile = File(context.filesDir, "license.dat")
    private val expiredFile = File(context.filesDir, "license_expired.dat")
    private val pendingTransferFile = File(context.filesDir, "pending_transfer.json")
    private val prefs = context.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
    private val gson = Gson()

    private fun getMachineId(): String = getMachineId(context)

    private fun xorEncode(data: String): String {
        val key = getMachineId()
        val sb = StringBuilder()
        for (i in data.indices) {
            sb.append((data[i].code xor key[i % key.length].code).toChar())
        }
        return Base64.encodeToString(sb.toString().toByteArray(Charsets.ISO_8859_1), Base64.NO_WRAP)
    }

    private fun xorDecode(encoded: String): String {
        val rawBytes = Base64.decode(encoded, Base64.DEFAULT)
        val decoded = String(rawBytes, Charsets.ISO_8859_1)
        val key = getMachineId()
        val sb = StringBuilder()
        for (i in decoded.indices) {
            sb.append((decoded[i].code xor key[i % key.length].code).toChar())
        }
        return sb.toString()
    }

    private fun hmacSign(data: String): String {
        val machineBytes = getMachineId().toByteArray(Charsets.UTF_8)
        val keyBytes = ByteArray(LIC_SALT.size + machineBytes.size)
        System.arraycopy(LIC_SALT, 0, keyBytes, 0, LIC_SALT.size)
        System.arraycopy(machineBytes, 0, keyBytes, LIC_SALT.size, machineBytes.size)

        val keySpec = SecretKeySpec(keyBytes, "HmacSHA256")
        val mac = Mac.getInstance("HmacSHA256").apply { init(keySpec) }
        val digest = mac.doFinal(data.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { "%02x".format(it) }.lowercase()
    }

    private fun serverHmac(msg: String): String {
        val keySpec = SecretKeySpec(LIC_SALT, "HmacSHA256")
        val mac = Mac.getInstance("HmacSHA256").apply { init(keySpec) }
        val digest = mac.doFinal(msg.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { "%02x".format(it) }.lowercase()
    }

    private fun secureEquals(a: String?, b: String?): Boolean {
        if (a == null || b == null || a.length != b.length) return false
        var result = 0
        for (i in a.indices) {
            result = result or (a[i].code xor b[i].code)
        }
        return result == 0
    }

    private fun encryptPayload(payload: LicensePayload): String? {
        return try {
            val sigInput = "${payload.key}:${payload.email}:${payload.machine}:${payload.plan}:${payload.licenseType}:${payload.licensedAt}"
            payload.hmacSignature = hmacSign(sigInput)
            val json = gson.toJson(payload)
            xorEncode(json)
        } catch (e: Exception) {
            null
        }
    }

    private fun decryptAndVerify(encrypted: String?): LicensePayload? {
        if (encrypted.isNullOrEmpty()) return null
        return try {
            val json = xorDecode(encrypted)
            val payload = gson.fromJson(json, LicensePayload::class.java) ?: return null

            if (payload.machine != getMachineId()) {
                Log.w(TAG, "License machine mismatch")
                return null
            }

            val sigInput = "${payload.key}:${payload.email}:${payload.machine}:${payload.plan}:${payload.licenseType}:${payload.licensedAt}"
            val expectedSig = hmacSign(sigInput)
            if (!secureEquals(payload.hmacSignature, expectedSig)) {
                Log.w(TAG, "License HMAC mismatch")
                return null
            }

            val expiresVal = payload.expires
            if (expiresVal != null && System.currentTimeMillis() > expiresVal) {
                Log.w(TAG, "License expired")
                return null
            }

            payload
        } catch (e: Exception) {
            null
        }
    }

    fun readLicensePayload(): LicensePayload? {
        var fileData: String? = null
        var prefData: String? = null

        try {
            if (licenseFile.exists()) {
                fileData = licenseFile.readText()
            }
        } catch (e: Exception) { }

        try {
            prefData = prefs.getString("lic_token", null)
        } catch (e: Exception) { }

        val payload = decryptAndVerify(fileData)
        if (payload != null) {
            if (fileData != prefData) {
                writeLicensePayload(payload)
            }
            return payload
        }

        val backupPayload = decryptAndVerify(prefData)
        if (backupPayload != null) {
            Log.i(TAG, "Self-healing: Restoring license from preferences mirror")
            try {
                licenseFile.writeText(prefData!!)
            } catch (e: Exception) { }
            return backupPayload
        }

        if (!fileData.isNullOrEmpty() || !prefData.isNullOrEmpty()) {
            Log.w(TAG, "License tamper detected. Deactivating.")
            deactivateLicense()
        }

        return null
    }

    fun writeLicensePayload(payload: LicensePayload) {
        try {
            val encrypted = encryptPayload(payload) ?: return
            try {
                licenseFile.writeText(encrypted)
            } catch (e: Exception) { }
            try {
                prefs.edit().putString("lic_token", encrypted).apply()
            } catch (e: Exception) { }
        } catch (e: Exception) { }
    }

    fun deactivateLicense() {
        markLicenseExpired("revoked")
        try {
            if (licenseFile.exists()) licenseFile.delete()
        } catch (e: Exception) { }
        try {
            prefs.edit().remove("lic_token").apply()
        } catch (e: Exception) { }
        deletePendingTransferCache()
        Log.i(TAG, "License deactivated and wiped from all mirrors")
    }

    fun markLicenseExpired(reason: String) {
        try {
            expiredFile.writeText(reason)
        } catch (e: Exception) { }
    }

    fun clearLicenseExpiredMarker() {
        try {
            if (expiredFile.exists()) expiredFile.delete()
        } catch (e: Exception) { }
    }

    fun isLicenseExpiredMarkerSet(): Boolean = expiredFile.exists()

    suspend fun validateLicenseOnlineAsync(
        key: String,
        email: String? = null,
        requestTransfer: Boolean = false
    ): ValidationResult = withContext(Dispatchers.IO) {
        try {
            val cleanKey = key.trim().uppercase()
            val url = URL(LICENSE_URL)
            val conn = url.openConnection() as HttpURLConnection
            conn.requestMethod = "POST"
            conn.setRequestProperty("Content-Type", "application/json")
            conn.connectTimeout = 10000
            conn.readTimeout = 10000
            conn.doOutput = true

            val body = mutableMapOf<String, Any>(
                "key" to cleanKey,
                "machine_id" to getMachineId(),
                "app" to APP_NAME,
                "software_id" to "clipboardpro"
            )
            if (!email.isNullOrBlank()) {
                body["email"] = email.trim().lowercase()
            }
            if (requestTransfer) {
                body["request_transfer"] = true
            }

            val jsonOutput = gson.toJson(body)
            OutputStreamWriter(conn.outputStream).use { it.write(jsonOutput) }

            if (conn.responseCode != 200) {
                return@withContext ValidationResult(
                    valid = false,
                    message = "Server error ${conn.responseCode}. Check connection."
                )
            }

            val responseJson = InputStreamReader(conn.inputStream).use { it.readText() }
            val data = gson.fromJson(responseJson, ServerResponse::class.java)
                ?: return@withContext ValidationResult(valid = false, message = "Invalid server response.")

            if (data.valid == true && !requestTransfer) {
                val licType = data.licenseType ?: "lifetime"
                val expectedMsg = "True:${cleanKey}:${getMachineId()}:${licType}"
                val expectedSig = serverHmac(expectedMsg)

                if (data.signature.isNullOrEmpty() || !secureEquals(data.signature, expectedSig)) {
                    Log.w(TAG, "Server signature verification failed!")
                    return@withContext ValidationResult(
                        valid = false,
                        message = "Security Error: Invalid response signature."
                    )
                }
            }

            ValidationResult(
                valid = data.valid ?: false,
                message = data.message ?: if (data.valid == true) "License validated." else "Invalid key.",
                plan = data.plan ?: "Pro",
                licenseType = data.licenseType ?: "lifetime",
                canRequestTransfer = data.canRequestTransfer ?: false,
                transferPending = data.transferPending ?: false,
                transferRequestSubmitted = data.valid == true && requestTransfer
            )
        } catch (e: Exception) {
            ValidationResult(
                valid = false,
                message = "Network error - check your internet connection."
            )
        }
    }

    suspend fun activateLicenseAsync(key: String, email: String? = null): ValidationResult {
        val cleanKey = key.trim().uppercase()
        if (cleanKey.isEmpty()) return ValidationResult(valid = false, message = "Please enter a license key.")

        val result = validateLicenseOnlineAsync(cleanKey, email)
        if (result.valid) {
            writeLicensePayload(
                LicensePayload(
                    key = cleanKey,
                    email = email?.trim()?.lowercase() ?: "",
                    machine = getMachineId(),
                    plan = result.plan,
                    licenseType = result.licenseType,
                    licensedAt = System.currentTimeMillis(),
                    lastOnlineCheck = System.currentTimeMillis()
                )
            )
            deletePendingTransferCache()
            clearLicenseExpiredMarker()
        }
        return result
    }

    suspend fun requestTransferAsync(key: String, email: String): ValidationResult {
        val cleanKey = key.trim().uppercase()
        val result = validateLicenseOnlineAsync(cleanKey, email, requestTransfer = true)
        if (result.valid && result.transferRequestSubmitted) {
            savePendingTransferCache(cleanKey, email)
        }
        return result
    }

    suspend fun checkLicenseOnlineSilentAsync(): ValidationResult {
        val payload = readLicensePayload() ?: return ValidationResult(valid = false, message = "No license active.")

        return try {
            val result = validateLicenseOnlineAsync(payload.key, payload.email)
            if (!result.valid && !result.isNetworkError() && !result.transferPending && !result.canRequestTransfer) {
                Log.w(TAG, "License revoked by server. Deactivating.")
                deactivateLicense()
                ValidationResult(valid = false, message = result.message, revoked = true)
            } else if (result.valid) {
                payload.lastOnlineCheck = System.currentTimeMillis()
                payload.plan = result.plan
                payload.licenseType = result.licenseType
                writeLicensePayload(payload)
                ValidationResult(valid = true, message = "License verified online.")
            } else {
                // Mismatch or pending - check local offline grace period
                checkOfflineGracePeriod(payload)
            }
        } catch (e: Exception) {
            checkOfflineGracePeriod(payload)
        }
    }

    private fun checkOfflineGracePeriod(payload: LicensePayload): ValidationResult {
        val elapsed = System.currentTimeMillis() - payload.lastOnlineCheck
        return if (elapsed < OFFLINE_GRACE_PERIOD_MS) {
            val daysLeft = ((OFFLINE_GRACE_PERIOD_MS - elapsed) / (24 * 60 * 60 * 1000)).toInt()
            Log.i(TAG, "Offline verified. $daysLeft days remaining.")
            ValidationResult(valid = true, message = "Offline approved.")
        } else {
            Log.w(TAG, "Offline lease period expired.")
            ValidationResult(valid = false, message = "Offline lease period expired.", offlineExpired = true)
        }
    }

    fun getLicenseStatus(): LicenseStatus {
        val payload = readLicensePayload()
        val trial = TrialService(context)

        if (payload != null) {
            val elapsed = System.currentTimeMillis() - payload.lastOnlineCheck
            if (elapsed > OFFLINE_GRACE_PERIOD_MS) {
                return LicenseStatus(
                    isLicensed = false,
                    plan = null,
                    trialExpired = trial.isTrialExpired(),
                    trialRemainingMs = trial.getRemainingTimeMs(),
                    offlineExpired = true
                )
            }

            val keyPreview = if (payload.key.length >= 8) {
                payload.key.substring(0, 4) + "-****-****-" + payload.key.substring(payload.key.length - 4)
            } else payload.key

            return LicenseStatus(
                isLicensed = true,
                plan = payload.plan,
                licenseType = payload.licenseType,
                keyPreview = keyPreview,
                email = payload.email,
                licensedAt = payload.licensedAt,
                trialExpired = trial.isTrialExpired(),
                trialRemainingMs = trial.getRemainingTimeMs()
            )
        }

        return LicenseStatus(
            isLicensed = false,
            plan = null,
            trialExpired = trial.isTrialExpired(),
            trialRemainingMs = trial.getRemainingTimeMs()
        )
    }

    fun isAppAllowed(): Boolean {
        val status = getLicenseStatus()
        return status.isLicensed || !status.trialExpired
    }

    fun savePendingTransferCache(key: String, email: String) {
        try {
            val cache = PendingTransfer(key, email, true)
            pendingTransferFile.writeText(gson.toJson(cache))
        } catch (e: Exception) { }
    }

    fun readPendingTransferCache(): PendingTransfer? {
        return try {
            if (!pendingTransferFile.exists()) return null
            gson.fromJson(pendingTransferFile.readText(), PendingTransfer::class.java)
        } catch (e: Exception) {
            null
        }
    }

    fun deletePendingTransferCache() {
        try {
            if (pendingTransferFile.exists()) pendingTransferFile.delete()
        } catch (e: Exception) { }
    }
}

// ── Models ────────────────────────────────────────────────────────────────
data class LicensePayload(
    @SerializedName("Key") var key: String = "",
    @SerializedName("Email") var email: String = "",
    @SerializedName("Machine") var machine: String = "",
    @SerializedName("Plan") var plan: String = "Pro",
    @SerializedName("LicenseType") var licenseType: String = "lifetime",
    @SerializedName("LicensedAt") var licensedAt: Long = 0,
    @SerializedName("LastOnlineCheck") var lastOnlineCheck: Long = 0,
    @SerializedName("Expires") var expires: Long? = null,
    @SerializedName("HmacSignature") var hmacSignature: String = ""
)

data class ValidationResult(
    val valid: Boolean,
    val message: String,
    val plan: String = "Pro",
    val licenseType: String = "lifetime",
    val canRequestTransfer: Boolean = false,
    val transferPending: Boolean = false,
    val transferRequestSubmitted: Boolean = false,
    val revoked: Boolean = false,
    val offlineExpired: Boolean = false
) {
    fun isNetworkError(): Boolean = message.contains("Network error", ignoreCase = true)
}

data class LicenseStatus(
    val isLicensed: Boolean,
    val plan: String?,
    val licenseType: String? = null,
    val keyPreview: String? = null,
    val email: String? = null,
    val licensedAt: Long = 0,
    val trialExpired: Boolean,
    val trialRemainingMs: Long,
    val offlineExpired: Boolean = false
)

data class ServerResponse(
    @SerializedName("valid") val valid: Boolean?,
    @SerializedName("message") val message: String?,
    @SerializedName("plan") val plan: String?,
    @SerializedName("license_type") val licenseType: String?,
    @SerializedName("signature") val signature: String?,
    @SerializedName("can_request_transfer") val canRequestTransfer: Boolean?,
    @SerializedName("transfer_pending") val transferPending: Boolean?
)

data class PendingTransfer(
    @SerializedName("key") val key: String = "",
    @SerializedName("email") val email: String = "",
    @SerializedName("transfer_requested") val transferRequested: Boolean = false
)
