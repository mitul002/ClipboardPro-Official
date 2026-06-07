package com.clipboardpro.share.service

import android.accessibilityservice.AccessibilityService
import android.os.Bundle
import android.util.Log
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.SnippetItemEntity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch

class TextExpanderService : AccessibilityService() {

    companion object {
        private const val TAG = "TextExpanderService"

        /**
         * Allowed delimiter symbols — exactly matches the Windows client's HasValidDelimiter logic.
         * A trigger MUST start or end with one of these characters.
         */
        val ALLOWED_DELIMITERS = setOf(
            ';', '.', '/', '!', '@', '#', ':', ',', '?', '*', '-', '_', '+', '=', '~'
        )

        /**
         * Returns true when the trigger starts or ends with a permitted delimiter symbol.
         * Mirrors Windows: HasValidDelimiter()
         */
        fun hasValidDelimiter(trigger: String): Boolean {
            if (trigger.isEmpty()) return false
            return trigger.first() in ALLOWED_DELIMITERS || trigger.last() in ALLOWED_DELIMITERS
        }

        /**
         * Strips leading and trailing delimiter symbols from the trigger, returning only
         * the alphanumeric "core". Mirrors Windows: GetCleanTrigger()
         * Example: ":ad" → "ad", "em;" → "em", "#hello#" → "hello"
         */
        fun getCleanTrigger(trigger: String): String {
            if (trigger.isEmpty()) return ""
            var start = 0
            while (start < trigger.length && !trigger[start].isLetterOrDigit()) start++
            var end = trigger.length - 1
            while (end >= start && !trigger[end].isLetterOrDigit()) end--
            // If all chars are non-alphanumeric (e.g. pure symbol trigger), return as-is
            if (start > end) return trigger
            return trigger.substring(start, end + 1)
        }
    }

    private val job = SupervisorJob()
    private val scope = CoroutineScope(Dispatchers.Main + job)

    private var snippetsList = listOf<SnippetItemEntity>()

    // Expansion undo state
    private var lastExpandedText = ""  // Full text AFTER expansion (e.g., "Hello mirpur")
    private var lastTrigger = ""       // The original trigger string (e.g., ":ad")
    private var preExpansionText = ""  // Full field text BEFORE expansion (e.g., "Hello :ad")

    private var isExpanding = false

    override fun onCreate() {
        super.onCreate()

        val db = AppDatabase.getDatabase(this)
        scope.launch {
            db.snippetDao().getAllSnippetsFlow().collectLatest { list ->
                // Only load snippets that have a valid delimiter — skip any legacy ones without
                snippetsList = list.filter { hasValidDelimiter(it.trigger) }
                Log.d(TAG, "Loaded ${snippetsList.size} valid snippets for expansion.")
            }
        }
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent) {
        if (event.eventType != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) return
        if (isExpanding) return

        val sourceNode = event.source ?: return

        if (!sourceNode.isEditable && !sourceNode.className.toString().contains("Edit", ignoreCase = true)) {
            sourceNode.recycle()
            return
        }

        val text = sourceNode.text?.toString() ?: ""

        // ── Undo / Backspace detection ────────────────────────────────────────
        // When expansion is armed and the user presses backspace once, the current text
        // becomes lastExpandedText minus its last character.
        // On undo: restore the clean trigger (strip delimiter prefix/suffix), matching Windows behaviour.
        if (lastExpandedText.isNotEmpty() && text == lastExpandedText.dropLast(1)) {
            isExpanding = true

            // Windows: GetCleanTrigger strips the delimiter, so ":ad" → "ad", "em;" → "em"
            val cleanTrigger = getCleanTrigger(lastTrigger)

            // Rebuild restored text: everything before the expansion + clean trigger
            val prefixLen = preExpansionText.length - lastTrigger.length
            val prefix = if (prefixLen >= 0) preExpansionText.substring(0, prefixLen) else ""
            val restoredText = prefix + cleanTrigger

            val arguments = Bundle().apply {
                putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, restoredText)
            }
            sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, arguments)

            val selectionArgs = Bundle().apply {
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, restoredText.length)
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, restoredText.length)
            }
            sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selectionArgs)

            // Clear undo state
            lastExpandedText = ""
            lastTrigger = ""
            preExpansionText = ""
            isExpanding = false
            sourceNode.recycle()
            return
        }

        // If user typed something else after expansion, cancel the undo window
        if (lastExpandedText.isNotEmpty() && text != lastExpandedText) {
            lastExpandedText = ""
            lastTrigger = ""
            preExpansionText = ""
        }

        if (text.isBlank()) {
            sourceNode.recycle()
            return
        }

        // ── Normal Expansion Scan ─────────────────────────────────────────────
        for (snippet in snippetsList) {
            val trigger = snippet.trigger
            if (text.endsWith(trigger)) {
                isExpanding = true

                val textBeforeTrigger = text.substring(0, text.length - trigger.length)
                val newText = textBeforeTrigger + snippet.content

                val arguments = Bundle().apply {
                    putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, newText)
                }
                sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, arguments)

                val selectionArgs = Bundle().apply {
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, newText.length)
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, newText.length)
                }
                sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selectionArgs)

                // Arm undo state
                preExpansionText = text       // e.g., "Hello :ad"
                lastTrigger = trigger         // e.g., ":ad"
                lastExpandedText = newText    // e.g., "Hello mirpur"

                isExpanding = false
                break
            }
        }
        sourceNode.recycle()
    }

    override fun onInterrupt() {
        Log.w(TAG, "Accessibility Service Interrupted.")
    }

    override fun onDestroy() {
        job.cancel()
        super.onDestroy()
    }
}
