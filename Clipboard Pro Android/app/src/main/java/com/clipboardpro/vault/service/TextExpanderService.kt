package com.clipboardpro.vault.service

import android.accessibilityservice.AccessibilityService
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.os.Bundle
import android.util.Log
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import com.clipboardpro.vault.data.AppDatabase
import com.clipboardpro.vault.data.SnippetItemEntity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

class TextExpanderService : AccessibilityService() {

    companion object {
        private const val TAG = "TextExpanderService"

        /** Broadcast action (kept for legacy compatibility) */
        const val ACTION_CLIP_SAVED = "com.clipboardpro.vault.CLIP_SAVED"

        val ALLOWED_DELIMITERS = setOf(
            ';', '.', '/', '!', '@', '#', ':', ',', '?', '*', '-', '_', '+', '=', '~'
        )

        fun hasValidDelimiter(trigger: String): Boolean {
            if (trigger.isEmpty()) return false
            return trigger.first() in ALLOWED_DELIMITERS || trigger.last() in ALLOWED_DELIMITERS
        }

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

    // ── Clipboard copy-detection state (XClipper technique) ──────────────────
    private var lastSelectionFrom = -1
    private var lastSelectionTo = -1
    private var lastSelectionPackage: String? = null
    private var lastSelectionClass: String? = null
    
    private val copyWords = setOf("copy", "cut", "copied", "copy to clipboard")
    private val copyToastRegex = Regex("(copied|clipboard)", RegexOption.IGNORE_CASE)
    @Volatile private var clipLaunchPending = false

    override fun onCreate() {
        super.onCreate()
        database = AppDatabase.getDatabase(this)
        scope.launch {
            database.snippetDao().getAllSnippetsFlow().collectLatest { list ->
                snippetsList = list.filter { hasValidDelimiter(it.trigger) }
                Log.d(TAG, "Loaded ${snippetsList.size} valid snippets.")
            }
        }
        clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    }

    // ─────────────────────────────────────────────────────────────────────────
    // onAccessibilityEvent — handles BOTH copy detection AND snippet expansion
    // ─────────────────────────────────────────────────────────────────────────
    override fun onAccessibilityEvent(event: AccessibilityEvent) {
        try {
            // ── 1. Copy-event detection (XClipper technique) ──────────────────
            detectAndCaptureCopy(event)

            // ── 2. Text-expander logic ────────────────────────────────────────
            if (event.eventType != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) return
            if (isExpanding) return

            val sourceNode = event.source ?: return

            val className = sourceNode.className?.toString() ?: ""
            val isEditableNode = sourceNode.isEditable ||
                    sourceNode.isFocused ||
                    className.contains("Edit", ignoreCase = true) ||
                    className.contains("WebView", ignoreCase = true)

            if (!isEditableNode) {
                // Do not recycle sourceNode here if you're not sure, but generally good practice
                return
            }

            val rawText = sourceNode.text?.toString() ?: ""
            // Fallback for some views that don't report text in sourceNode.text
            val text = if (rawText.isBlank() && event.text.isNotEmpty()) {
                event.text.filterNotNull().joinToString("")
            } else {
                rawText
            }

            val cursorPosition = sourceNode.textSelectionEnd
            // If cursor position is invalid (-1), we might still want to try expanding if it's the end of text
            val actualCursorPos = if (cursorPosition == -1) text.length else cursorPosition
            val textBeforeCursor = if (actualCursorPos in 0..text.length) text.substring(0, actualCursorPos) else text

            // Undo / Backspace detection
            if (lastExpandedText.isNotEmpty() && textBeforeCursor == lastExpandedText.dropLast(1)) {
                isExpanding = true
                val cleanTrigger = getCleanTrigger(lastTrigger)
                val prefixLen = preExpansionText.length - lastTrigger.length
                val prefix = if (prefixLen >= 0) preExpansionText.substring(0, prefixLen) else ""
                val textAfterCursor = if (actualCursorPos in 0..text.length) text.substring(actualCursorPos) else ""
                setTextViaClipboard(sourceNode, prefix + cleanTrigger + textAfterCursor)
                lastExpandedText = ""; lastTrigger = ""; preExpansionText = ""
                isExpanding = false
                return
            }

            if (lastExpandedText.isNotEmpty() && textBeforeCursor != lastExpandedText) {
                lastExpandedText = ""; lastTrigger = ""; preExpansionText = ""
            }

            if (text.isBlank()) return

            // Snippet expansion scan
            for (snippet in snippetsList) {
                val trigger = snippet.trigger
                if (textBeforeCursor.endsWith(trigger)) {
                    isExpanding = true
                    val prefix = textBeforeCursor.substring(0, textBeforeCursor.length - trigger.length)
                    val textAfterCursor = if (actualCursorPos in 0..text.length) text.substring(actualCursorPos) else ""
                    val expanded = prefix + snippet.content + textAfterCursor

                    val setTextArgs = Bundle().apply {
                        putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, expanded)
                    }
                    
                    // Try to focus first
                    sourceNode.performAction(AccessibilityNodeInfo.ACTION_FOCUS)
                    val setTextSuccess = sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, setTextArgs)

                    if (setTextSuccess) {
                        val newCursorPos = prefix.length + snippet.content.length
                        val selArgs = Bundle().apply {
                            putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, newCursorPos)
                            putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, newCursorPos)
                        }
                        sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selArgs)
                    } else {
                        // FALLBACK: Paste mechanism
                        val delArgs = Bundle().apply {
                            putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, textBeforeCursor.length - trigger.length)
                            putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, textBeforeCursor.length)
                        }
                        sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, delArgs)

                        val prevClip = try { clipboardManager.primaryClip } catch (e: Exception) { null }
                        clipboardManager.setPrimaryClip(ClipData.newPlainText("ClipExpand", snippet.content))
                        
                        // Perform paste synchronously before node is recycled
                        sourceNode.performAction(AccessibilityNodeInfo.ACTION_PASTE)
                        
                        // Restore previous clipboard after a delay
                        scope.launch {
                            kotlinx.coroutines.delay(500)
                            try { if (prevClip != null) clipboardManager.setPrimaryClip(prevClip) } catch (e: Exception) { }
                        }
                    }

                    preExpansionText = textBeforeCursor
                    lastTrigger = trigger
                    lastExpandedText = prefix + snippet.content
                    isExpanding = false
                    break
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "onAccessibilityEvent error: ${e.localizedMessage}")
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // detectAndCaptureCopy — XClipper's 3-technique heuristic copy detection
    // ─────────────────────────────────────────────────────────────────────────
    private fun detectAndCaptureCopy(event: AccessibilityEvent) {
        val eventType = event.eventType

        // Technique 1: "Copy" / "Cut" context-menu button click
        if (eventType == AccessibilityEvent.TYPE_VIEW_CLICKED) {
            val desc = event.contentDescription?.toString()?.lowercase() ?: ""
            val text = event.text.filterNotNull().joinToString(" ").lowercase()
            if (copyWords.any { desc.contains(it) } || copyWords.any { text.contains(it) }) {
                launchCapture()
                return
            }
        }

        // Technique 2: Toast message confirming copy ("Copied", "Copied to clipboard")
        if (eventType == AccessibilityEvent.TYPE_NOTIFICATION_STATE_CHANGED) {
            val className = event.className?.toString() ?: ""
            if (className.contains("Toast", ignoreCase = true)) {
                val text = event.text.filterNotNull().joinToString(" ")
                if (copyToastRegex.containsMatchIn(text)) {
                    launchCapture()
                    return
                }
            }
        }

        // Technique 3: Selection-changed heuristic
        // (selection had range → collapsed = user likely pressed Copy)
        if (eventType == AccessibilityEvent.TYPE_VIEW_TEXT_SELECTION_CHANGED) {
            val prevHadSelection = lastSelectionFrom != lastSelectionTo && lastSelectionFrom >= 0 && lastSelectionTo >= 0
            val curCollapsed = event.fromIndex == event.toIndex
            val sameView = lastSelectionPackage == event.packageName?.toString() && lastSelectionClass == event.className?.toString()

            if (sameView && prevHadSelection && curCollapsed) {
                launchCapture()
                lastSelectionFrom = -1
                lastSelectionTo = -1
                return
            }
            
            lastSelectionFrom = event.fromIndex
            lastSelectionTo = event.toIndex
            lastSelectionPackage = event.packageName?.toString()
            lastSelectionClass = event.className?.toString()
        } else if (eventType != AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED) {
            lastSelectionFrom = -1
            lastSelectionTo = -1
            lastSelectionPackage = null
            lastSelectionClass = null
        }
    }

    private fun launchCapture() {
        if (clipLaunchPending) return
        clipLaunchPending = true
        Log.d(TAG, "Copy detected — launching ClipboardCaptureActivity")
        ClipboardCaptureActivity.launch(applicationContext)
        scope.launch {
            kotlinx.coroutines.delay(1500)
            clipLaunchPending = false
        }
    }

    private fun setTextViaClipboard(node: AccessibilityNodeInfo, text: String) {
        try {
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
            val selAllArgs = Bundle().apply {
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, 0)
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, node.text?.length ?: 0)
            }
            node.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selAllArgs)
            val prevClip = try { clipboardManager.primaryClip } catch (e: Exception) { null }
            clipboardManager.setPrimaryClip(ClipData.newPlainText("ClipExpand", text))
            node.performAction(AccessibilityNodeInfo.ACTION_PASTE)
            scope.launch {
                kotlinx.coroutines.delay(300)
                try { if (prevClip != null) clipboardManager.setPrimaryClip(prevClip) } catch (e: Exception) { }
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
