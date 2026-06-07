package com.clipboardpro.share.service

import android.accessibilityservice.AccessibilityService
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import androidx.core.view.accessibility.AccessibilityNodeInfoCompat
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.data.SnippetItemEntity
import com.clipboardpro.share.model.ClipboardItemType
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

class TextExpanderService : AccessibilityService() {

    companion object {
        private const val TAG = "TextExpanderService"

        /** Broadcast action to notify LocalShareService that a new clip was saved by the accessibility service */
        const val ACTION_CLIP_SAVED = "com.clipboardpro.share.CLIP_SAVED"

        /**
         * Allowed delimiter symbols — exactly matches the Windows client's HasValidDelimiter logic.
         * A trigger MUST start or end with one of these characters.
         */
        val ALLOWED_DELIMITERS = setOf(
            ';', '.', '/', '!', '@', '#', ':', ',', '?', '*', '-', '_', '+', '=', '~'
        )

        fun hasValidDelimiter(trigger: String): Boolean {
            if (trigger.isEmpty()) return false
            return trigger.first() in ALLOWED_DELIMITERS || trigger.last() in ALLOWED_DELIMITERS
        }

        /**
         * Strips leading and trailing delimiter symbols, returning the alphanumeric core.
         * Mirrors Windows: GetCleanTrigger()
         * Example: ":ad" → "ad", "em;" → "em"
         */
        fun getCleanTrigger(trigger: String): String {
            if (trigger.isEmpty()) return ""
            var start = 0
            while (start < trigger.length && !trigger[start].isLetterOrDigit()) start++
            var end = trigger.length - 1
            while (end >= start && !trigger[end].isLetterOrDigit()) end--
            if (start > end) return trigger
            return trigger.substring(start, end + 1)
        }
    }

    private val job = SupervisorJob()
    private val scope = CoroutineScope(Dispatchers.IO + job)

    private var snippetsList = listOf<SnippetItemEntity>()
    private lateinit var database: AppDatabase
    private lateinit var clipboardManager: ClipboardManager

    // ── Expansion undo state ─────────────────────────────────────────────────
    private var lastExpandedText = ""
    private var lastTrigger = ""
    private var preExpansionText = ""
    private var isExpanding = false

    // ── Clipboard monitoring state ────────────────────────────────────────────
    // Track last clip label we injected ourselves to avoid self-reads
    @Volatile private var lastSelfSetLabel: String? = null
    @Volatile private var lastSavedContent: String? = null

    override fun onCreate() {
        super.onCreate()
        database = AppDatabase.getDatabase(this)

        // Load snippets reactively
        scope.launch {
            database.snippetDao().getAllSnippetsFlow().collectLatest { list ->
                snippetsList = list.filter { hasValidDelimiter(it.trigger) }
                Log.d(TAG, "Loaded ${snippetsList.size} valid snippets.")
            }
        }

        // ── Clipboard monitoring from Accessibility Service ───────────────────
        // Accessibility Services are treated as foreground-equivalent by Android
        // and can read the clipboard even in background (unlike regular background services).
        clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        clipboardManager.addPrimaryClipChangedListener {
            scope.launch {
                try {
                    val clip = clipboardManager.primaryClip ?: return@launch
                    if (clip.itemCount == 0) return@launch

                    val label = clip.description?.label?.toString() ?: ""

                    // Ignore clips we set ourselves during expansion or sync
                    if (label == "ClipboardPro Sync" ||
                        label == "ClipExpand" ||
                        label == lastSelfSetLabel) return@launch

                    val text = clip.getItemAt(0)?.text?.toString() ?: return@launch
                    if (text.isBlank()) return@launch
                    if (text == lastSavedContent) return@launch // deduplicate

                    lastSavedContent = text
                    saveClipboardItem(text)
                } catch (e: Throwable) {
                    Log.e(TAG, "Clipboard listener error: ${e.localizedMessage}")
                }
            }
        }
    }

    /** Save a text item directly to Room database from the Accessibility Service */
    private suspend fun saveClipboardItem(text: String) {
        val clean = text.trim()
        if (clean.isBlank()) return
        try {
            val dao = database.clipboardDao()
            val existing = dao.getAllItems().find { it.content == clean }
            val type = ContentParser.detectType(clean)
            val isSensitive = ContentParser.isSensitive(clean)

            val entity = if (existing != null) {
                existing.copy(timestamp = System.currentTimeMillis())
            } else {
                ClipboardItemEntity(
                    id = java.util.UUID.randomUUID().toString(),
                    content = clean,
                    type = type.value,
                    timestamp = System.currentTimeMillis(),
                    isSensitive = isSensitive,
                    isMasked = isSensitive,
                    isJson = clean.startsWith("{") || clean.startsWith("[")
                )
            }
            dao.insertItem(entity)
            Log.i(TAG, "Clipboard item saved: ${clean.take(50)}")

            // Trim history
            val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
            val maxItems = prefs.getInt("max_history_items", 200)
            dao.trimExcessItems(maxItems)
        } catch (e: Throwable) {
            Log.e(TAG, "Failed to save clipboard item: ${e.localizedMessage}")
        }
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent) {
        if (event.eventType != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) return
        if (isExpanding) return

        val sourceNode = event.source ?: return

        if (!sourceNode.isEditable &&
            !sourceNode.className.toString().contains("Edit", ignoreCase = true)) {
            sourceNode.recycle()
            return
        }

        val text = sourceNode.text?.toString() ?: ""

        // ── Undo / Backspace detection ────────────────────────────────────────
        if (lastExpandedText.isNotEmpty() && text == lastExpandedText.dropLast(1)) {
            isExpanding = true
            val cleanTrigger = getCleanTrigger(lastTrigger)
            val prefixLen = preExpansionText.length - lastTrigger.length
            val prefix = if (prefixLen >= 0) preExpansionText.substring(0, prefixLen) else ""
            val restoredText = prefix + cleanTrigger

            setTextViaClipboard(sourceNode, restoredText)

            lastExpandedText = ""
            lastTrigger = ""
            preExpansionText = ""
            isExpanding = false
            sourceNode.recycle()
            return
        }

        // Cancel undo window if user typed something else
        if (lastExpandedText.isNotEmpty() && text != lastExpandedText) {
            lastExpandedText = ""
            lastTrigger = ""
            preExpansionText = ""
        }

        if (text.isBlank()) {
            sourceNode.recycle()
            return
        }

        // ── Snippet expansion scan ─────────────────────────────────────────────
        for (snippet in snippetsList) {
            val trigger = snippet.trigger
            if (text.endsWith(trigger)) {
                isExpanding = true

                val prefix = text.substring(0, text.length - trigger.length)
                val expanded = prefix + snippet.content

                // First try ACTION_SET_TEXT (works in some apps e.g. Keep Notes)
                val setTextArgs = Bundle().apply {
                    putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, expanded)
                }
                val setTextSuccess = sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, setTextArgs)

                if (setTextSuccess) {
                    // Move cursor to end
                    val selArgs = Bundle().apply {
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, expanded.length)
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, expanded.length)
                    }
                    sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selArgs)
                } else {
                    // Fallback: clipboard-based injection (works in Gmail, YouTube, Xiaomi Notes, etc.)
                    // 1. Select all existing text that is the trigger
                    //    Send backspaces to delete the trigger chars
                    val delArgs = Bundle().apply {
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, prefix.length)
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, text.length)
                    }
                    sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, delArgs)

                    // 2. Inject snippet content via clipboard paste
                    lastSelfSetLabel = "ClipExpand"
                    val prevClip = try { clipboardManager.primaryClip } catch (e: Exception) { null }

                    clipboardManager.setPrimaryClip(
                        ClipData.newPlainText("ClipExpand", snippet.content)
                    )
                    sourceNode.performAction(AccessibilityNodeInfo.ACTION_PASTE)

                    // Restore original clipboard after a short delay
                    scope.launch {
                        kotlinx.coroutines.delay(300)
                        try {
                            if (prevClip != null) clipboardManager.setPrimaryClip(prevClip)
                        } catch (e: Exception) { /* ignore */ }
                        lastSelfSetLabel = null
                    }
                }

                // Arm undo state
                preExpansionText = text
                lastTrigger = trigger
                lastExpandedText = expanded

                isExpanding = false
                break
            }
        }
        sourceNode.recycle()
    }

    /**
     * Sets text in a field using clipboard paste — works in apps that block ACTION_SET_TEXT.
     * Used for undo restoration.
     */
    private fun setTextViaClipboard(node: AccessibilityNodeInfo, text: String) {
        try {
            // Try ACTION_SET_TEXT first
            val args = Bundle().apply {
                putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, text)
            }
            val success = node.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, args)
            if (success) {
                val selArgs = Bundle().apply {
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, text.length)
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, text.length)
                }
                node.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selArgs)
                return
            }
            // Fallback: select-all then paste
            val compatNode = AccessibilityNodeInfoCompat.wrap(node)
            compatNode.performAction(AccessibilityNodeInfoCompat.ACTION_SELECT_ALL)
            lastSelfSetLabel = "ClipExpand"
            val prevClip = try { clipboardManager.primaryClip } catch (e: Exception) { null }
            clipboardManager.setPrimaryClip(ClipData.newPlainText("ClipExpand", text))
            node.performAction(AccessibilityNodeInfo.ACTION_PASTE)
            scope.launch {
                kotlinx.coroutines.delay(300)
                try { if (prevClip != null) clipboardManager.setPrimaryClip(prevClip) } catch (e: Exception) { }
                lastSelfSetLabel = null
            }
        } catch (e: Exception) {
            Log.e(TAG, "setTextViaClipboard failed: ${e.localizedMessage}")
        }
    }

    override fun onInterrupt() {
        Log.w(TAG, "Accessibility Service Interrupted.")
    }

    override fun onDestroy() {
        job.cancel()
        super.onDestroy()
    }
}
