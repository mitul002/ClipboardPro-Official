package com.clipboardpro.share.service

import android.content.Context
import android.util.Base64
import android.util.Log
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

class TrialService(private val context: Context) {

    companion object {
        private const val TAG = "TrialService"
        const val TRIAL_PERIOD_DAYS = 30
        private val ISO_FORMAT = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
            timeZone = TimeZone.getTimeZone("UTC")
        }
    }

    private val prefs = context.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
    
    // File layers (mirror consensus strategy)
    private val fileLayer2 = File(context.filesDir, "trial_state.dat")
    private val fileLayer3 = File(context.cacheDir, "clip_state.db")

    private fun encryptDate(date: Long): String {
        val raw = ISO_FORMAT.format(Date(date))
        val key = LicenseService.getMachineId(context)
        val sb = StringBuilder()
        for (i in raw.indices) {
            sb.append((raw[i].code xor key[i % key.length].code).toChar())
        }
        return Base64.encodeToString(sb.toString().toByteArray(Charsets.ISO_8859_1), Base64.NO_WRAP)
    }

    private fun decryptDate(encrypted: String?): Long? {
        if (encrypted.isNullOrEmpty()) return null
        return try {
            val rawBytes = Base64.decode(encrypted, Base64.DEFAULT)
            val decoded = String(rawBytes, Charsets.ISO_8859_1)
            val key = LicenseService.getMachineId(context)
            val sb = StringBuilder()
            for (i in decoded.indices) {
                sb.append((decoded[i].code xor key[i % key.length].code).toChar())
            }
            ISO_FORMAT.parse(sb.toString())?.time
        } catch (e: Exception) {
            null
        }
    }

    // --- Layer Readers & Writers ---
    private fun readPrefLayer(): Long? {
        val enc = prefs.getString("trial_start_l1", null)
        return decryptDate(enc)
    }

    private fun writePrefLayer(date: Long) {
        prefs.edit().putString("trial_start_l1", encryptDate(date)).apply()
    }

    private fun readFileLayer(file: File): Long? {
        return try {
            if (!file.exists()) return null
            decryptDate(file.readText())
        } catch (e: Exception) {
            null
        }
    }

    private fun writeFileLayer(file: File, date: Long) {
        try {
            file.parentFile?.mkdirs()
            file.writeText(encryptDate(date))
        } catch (e: Exception) {
            Log.e(TAG, "Write file failed: ${e.localizedMessage}")
        }
    }

    // --- Consensus Logic ---
    fun getTrialStartDate(): Long {
        val now = System.currentTimeMillis()

        val d1 = readPrefLayer()
        val d2 = readFileLayer(fileLayer2)
        val d3 = readFileLayer(fileLayer3)

        // If ALL layers are missing -> first boot -> start trial now
        if (d1 == null && d2 == null && d3 == null) {
            syncAllLayers(now)
            return now
        }

        // Evaluate oldest valid date candidates (clock rollback security)
        var oldest = Long.MAX_VALUE
        var repairNeeded = false

        fun evaluate(d: Long?) {
            if (d != null) {
                // Clock tamper guard: reject future-dated records (over 1 day ahead)
                if (d <= now + 24 * 60 * 60 * 1000L && d < oldest) {
                    oldest = d
                }
            } else {
                repairNeeded = true
            }
        }

        evaluate(d1)
        evaluate(d2)
        evaluate(d3)

        // All layers future-dated -> instant expiry lock
        if (oldest == Long.MAX_VALUE) {
            return now - (TRIAL_PERIOD_DAYS + 1) * 24 * 60 * 60 * 1000L
        }

        // Clock rollback check: if current time is before our recorded oldest date
        if (now < oldest) {
            return now - (TRIAL_PERIOD_DAYS + 1) * 24 * 60 * 60 * 1000L
        }

        // Self-heal mismatching/deleted layers
        if (repairNeeded || d1 != oldest || d2 != oldest || d3 != oldest) {
            syncAllLayers(oldest)
        }

        return oldest
    }

    private fun syncAllLayers(date: Long) {
        writePrefLayer(date)
        writeFileLayer(fileLayer2, date)
        writeFileLayer(fileLayer3, date)
    }

    // --- Public APIs ---
    fun isTrialExpired(): Boolean {
        val start = getTrialStartDate()
        val elapsed = System.currentTimeMillis() - start
        val trialLimitMs = TRIAL_PERIOD_DAYS * 24L * 60 * 60 * 1000
        return elapsed > trialLimitMs
    }

    fun getRemainingDays(): Int {
        val remainingMs = getRemainingTimeMs()
        return (remainingMs / (24L * 60 * 60 * 1000)).toInt()
    }

    fun getRemainingTimeMs(): Long {
        val start = getTrialStartDate()
        val elapsed = System.currentTimeMillis() - start
        val limitMs = TRIAL_PERIOD_DAYS * 24L * 60 * 60 * 1000
        val remaining = limitMs - elapsed
        return if (remaining < 0) 0 else remaining
    }

    fun getTrialPercentUsed(): Float {
        val start = getTrialStartDate()
        val elapsed = (System.currentTimeMillis() - start).toFloat()
        val total = TRIAL_PERIOD_DAYS * 24f * 60 * 60 * 1000
        return (elapsed / total * 100f).coerceIn(0f, 100f)
    }
}
