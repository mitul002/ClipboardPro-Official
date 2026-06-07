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

        clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent) {
        if (event.eventType != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) return
        if (isExpanding) return

        val sourceNode = event.source ?: return

        val className = sourceNode.className?.toString() ?: ""
        val isEditableNode = sourceNode.isEditable ||
                sourceNode.isFocused ||
                className.contains("Edit", ignoreCase = true) ||
                className.contains("WebView", ignoreCase = true) ||
                className.contains("webview", ignoreCase = true)

        if (!isEditableNode) {
            sourceNode.recycle()
            return
        }

        val rawText = sourceNode.text?.toString() ?: ""
        val text = if (rawText.isBlank() && event.text.isNotEmpty()) event.text.joinToString("") else rawText
        
        val cursorPosition = sourceNode.textSelectionEnd
        val textBeforeCursor = if (cursorPosition in 0..text.length) {
            text.substring(0, cursorPosition)
        } else {
            text
        }

        // ── Undo / Backspace detection ────────────────────────────────────────
        if (lastExpandedText.isNotEmpty() && textBeforeCursor == lastExpandedText.dropLast(1)) {
            isExpanding = true
            val cleanTrigger = getCleanTrigger(lastTrigger)
            val prefixLen = preExpansionText.length - lastTrigger.length
            val prefix = if (prefixLen >= 0) preExpansionText.substring(0, prefixLen) else ""
            val textAfterCursor = if (cursorPosition in 0..text.length) text.substring(cursorPosition) else ""
            val restoredText = prefix + cleanTrigger + textAfterCursor

            setTextViaClipboard(sourceNode, restoredText)

            lastExpandedText = ""
            lastTrigger = ""
            preExpansionText = ""
            isExpanding = false
            sourceNode.recycle()
            return
        }

        // Cancel undo window if user typed something else
        if (lastExpandedText.isNotEmpty() && textBeforeCursor != lastExpandedText) {
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
            if (textBeforeCursor.endsWith(trigger)) {
                isExpanding = true

                val prefix = textBeforeCursor.substring(0, textBeforeCursor.length - trigger.length)
                val textAfterCursor = if (cursorPosition in 0..text.length) text.substring(cursorPosition) else ""
                val expanded = prefix + snippet.content + textAfterCursor

                // First try ACTION_SET_TEXT (works in some apps e.g. Keep Notes)
                val setTextArgs = Bundle().apply {
                    putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, expanded)
                }
                val setTextSuccess = sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, setTextArgs)

                if (setTextSuccess) {
                    // Move cursor to end of expanded text
                    val newCursorPos = prefix.length + snippet.content.length
                    val selArgs = Bundle().apply {
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, newCursorPos)
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, newCursorPos)
                    }
                    sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selArgs)
                } else {
                    // Fallback: clipboard-based injection (works in Gmail, YouTube, Xiaomi Notes, etc.)
                    // 1. Select all existing text that is the trigger
                    //    Send backspaces to delete the trigger chars
                    val delArgs = Bundle().apply {
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, textBeforeCursor.length - trigger.length)
                        putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, textBeforeCursor.length)
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
                preExpansionText = textBeforeCursor
                lastTrigger = trigger
                lastExpandedText = prefix + snippet.content

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
            // Fallback: select-all (using set selection from 0 to length) then paste
            val selAllArgs = Bundle().apply {
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, 0)
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, node.text?.length ?: 0)
            }
            node.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selAllArgs)
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
