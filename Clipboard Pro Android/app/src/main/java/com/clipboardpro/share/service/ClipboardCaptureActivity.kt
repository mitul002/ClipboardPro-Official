package com.clipboardpro.share.service

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Invisible trampoline activity that captures the system clipboard on Android 10+.
 *
 * XClipper pattern (the correct implementation):
 *  1. AccessibilityService (TextExpanderService) detects a copy event.
 *  2. It launches THIS activity with FLAG_ACTIVITY_NEW_TASK.
 *  3. Because the activity is now in the FOREGROUND, Android 10+ allows reading
 *     the clipboard without restriction.
 *  4. We read clipboard in onWindowFocusChanged (called when focus is gained),
 *     save to Room on IO thread, then finish().
 *
 * CRITICAL NOTES:
 *  - Do NOT use Theme.NoDisplay — it requires synchronous finish() before onResume()
 *    and will crash when finish() is deferred to a coroutine.
 *  - onWindowFocusChanged is the reliable trigger because it is always called
 *    after the activity becomes fully visible to the user.
 *  - We use a standalone CoroutineScope (not lifecycleScope) so the DB write
 *    can complete even if the activity finishes before it's done.
 */
class ClipboardCaptureActivity : ComponentActivity() {

    companion object {
        private const val TAG = "ClipboardCaptureActivity"

        /** Labels we set ourselves — never save these back into history. */
        private val IGNORED_LABELS = setOf("ClipExpand", "ClipboardPro Sync")

        /** Launch from any context (including AccessibilityService background). */
        fun launch(context: Context) {
            val intent = Intent(context, ClipboardCaptureActivity::class.java).apply {
                addFlags(
                    Intent.FLAG_ACTIVITY_NEW_TASK or
                    Intent.FLAG_ACTIVITY_SINGLE_TOP or
                    Intent.FLAG_ACTIVITY_CLEAR_TOP
                )
            }
            try {
                context.startActivity(intent)
            } catch (e: Exception) {
                Log.e(TAG, "Failed to launch capture activity: ${e.localizedMessage}")
            }
        }
    }

    // Standalone scope — outlives the activity so the DB write always completes.
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    /** Track whether we've already captured for this launch to avoid duplicates. */
    @Volatile private var captured = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // No setContentView — window is transparent (Theme.Transparent.Capture)
        // Actual capture happens in onWindowFocusChanged once we have focus.
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        // Reset so a re-launch re-captures.
        captured = false
    }

    /**
     * Called when the activity window gains or loses focus.
     * This is the XClipper-recommended entry point: the activity is now in
     * the foreground and Android 10+ permits clipboard access.
     */
    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus && !captured) {
            captured = true
            captureAndFinish()
        }
    }

    private fun captureAndFinish() {
        // Read clipboard synchronously on the Main thread (we're already in focus).
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager
        val clip = cm?.primaryClip

        if (clip == null || clip.itemCount == 0) {
            finish()
            return
        }

        val label = clip.description?.label?.toString() ?: ""
        if (label in IGNORED_LABELS) {
            finish()
            return
        }

        val text = clip.getItemAt(0)?.text?.toString()?.trim() ?: run {
            finish()
            return
        }

        if (text.isBlank()) {
            finish()
            return
        }

        // Finish the activity immediately — the DB write runs in background.
        finish()

        // Persist to Room on IO thread.
        scope.launch {
            try {
                val db = AppDatabase.getDatabase(applicationContext)
                val dao = db.clipboardDao()

                // Deduplicate — bump timestamp if already stored.
                val existing = dao.getAllItems().find { it.content == text }
                if (existing != null) {
                    dao.insertItem(existing.copy(timestamp = System.currentTimeMillis()))
                    Log.d(TAG, "Bumped timestamp for existing clip.")
                    return@launch
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

                // Trim history to user-configured limit.
                val prefs = applicationContext.getSharedPreferences(
                    "localshare_prefs", Context.MODE_PRIVATE
                )
                val maxItems = prefs.getInt("max_history_items", 200)
                dao.trimExcessItems(maxItems)

                Log.d(TAG, "Saved clip [${type.name}]: ${text.take(40)}")
            } catch (e: Exception) {
                Log.e(TAG, "DB error saving clip: ${e.localizedMessage}")
            }
        }
    }
}
