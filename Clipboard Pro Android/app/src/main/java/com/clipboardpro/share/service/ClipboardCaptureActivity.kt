package com.clipboardpro.share.service

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.lifecycle.lifecycleScope
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.model.ClipboardItemType
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/**
 * A completely transparent, theme-less Activity used solely to capture the
 * system clipboard on Android 10+.
 *
 * How it works (copied from XClipper's approach):
 *  1. The TextExpanderService (AccessibilityService) detects a "copy" event
 *     via accessibility events.
 *  2. It launches this Activity with FLAG_ACTIVITY_NEW_TASK.
 *  3. Because this Activity is now in the foreground, Android 10+ allows it
 *     to read the clipboard freely without any security exception.
 *  4. Once the clip is saved to Room, the Activity immediately finishes.
 *
 * The Activity style (Theme.Transparent.NoAnimation) must be declared in
 * AndroidManifest to make it invisible to the user.
 */
class ClipboardCaptureActivity : ComponentActivity() {

    private val TAG = "ClipboardCaptureActivity"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // No UI — just read clipboard and finish.
        readAndSaveClipboard()
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        // If already running, treat a new launch as another copy event.
        readAndSaveClipboard()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            readAndSaveClipboard()
        }
    }

    private fun readAndSaveClipboard() {
        lifecycleScope.launch {
            try {
                // Small delay: give ClipboardManager time to receive the new clip
                // (XClipper uses 500ms; this matches that proven approach).
                delay(300)
                val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager
                val clip = cm?.primaryClip
                if (clip == null || clip.itemCount == 0) {
                    finish()
                    return@launch
                }

                val label = clip.description?.label?.toString() ?: ""
                // Ignore clips we set ourselves (text-expander pastes, sync echoes, etc.)
                if (label == "ClipExpand" || label == "ClipboardPro Sync") {
                    finish()
                    return@launch
                }

                val text = clip.getItemAt(0)?.text?.toString()?.trim() ?: run {
                    finish()
                    return@launch
                }
                if (text.isBlank()) {
                    finish()
                    return@launch
                }

                saveToDatabase(text)
            } catch (e: Exception) {
                Log.e(TAG, "Failed to capture clipboard: ${e.localizedMessage}")
            } finally {
                finish()
            }
        }
    }

    private suspend fun saveToDatabase(text: String) {
        try {
            val db = AppDatabase.getDatabase(applicationContext)
            val dao = db.clipboardDao()

            // Avoid duplicate entries — just bump timestamp if content already exists
            val existing = dao.getAllItems().find { it.content == text }
            if (existing != null) {
                dao.insertItem(existing.copy(timestamp = System.currentTimeMillis()))
                Log.d(TAG, "Bumped timestamp for existing clip.")
                return
            }

            val type = ContentParser.detectType(text)
            val isSensitive = ContentParser.isSensitive(text)

            val entity = ClipboardItemEntity(
                id = java.util.UUID.randomUUID().toString(),
                content = text,
                type = type.value,
                timestamp = System.currentTimeMillis(),
                isSensitive = isSensitive,
                isMasked = isSensitive,
                isJson = text.startsWith("{") || text.startsWith("[")
            )
            dao.insertItem(entity)

            // Keep history within user-configured limit
            val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
            val maxItems = prefs.getInt("max_history_items", 200)
            dao.trimExcessItems(maxItems)

            Log.d(TAG, "Saved clip of type ${type.value}: ${text.take(40)}")
        } catch (e: Exception) {
            Log.e(TAG, "Database error: ${e.localizedMessage}")
        }
    }

    companion object {
        /**
         * Launch the transparent capture activity from any context.
         * Safe to call from background (AccessibilityService).
         */
        fun launch(context: Context) {
            val intent = Intent(context, ClipboardCaptureActivity::class.java).apply {
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            }
            context.startActivity(intent)
        }
    }
}
