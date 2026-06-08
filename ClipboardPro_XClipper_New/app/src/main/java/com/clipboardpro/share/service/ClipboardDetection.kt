package com.clipboardpro.share.service

import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import android.widget.Toast
import android.util.Log

typealias Predicate = (ClipboardDetection.AEvent) -> Boolean

class StripArrayList<T>(private val maxSize: Int) : ArrayList<T>() {
    override fun add(element: T): Boolean {
        if (size == maxSize) {
            removeAt(0)
        }
        return super.add(element)
    }
}

class ClipboardDetection(
    private val copyWord: String = "Copy"
) {
    private val typeViewSelectionChangeEvent = StripArrayList<AEvent>(2)
    private val eventList = StripArrayList<Int>(4)
    private var lastEvent: AEvent? = null

    companion object {
        private const val TAG = "ClipboardDetection"
        private const val MAX_COPY_WORD_DETECTION_LENGTH = 30
    }

    fun addEvent(eventType: Int) {
        eventList.add(eventType)
    }

    fun getSupportedEventTypes(event: AccessibilityEvent?, predicate: Predicate? = null): Boolean {
        if (event == null) return false

        val clipEvent = AEvent.from(event)
        if (predicate?.invoke(clipEvent) == true) return false
        return detectAppropriateEvents(event = clipEvent)
    }

    private fun detectAppropriateEvents(event: AEvent): Boolean {
        if (event.EventType == AccessibilityEvent.TYPE_VIEW_TEXT_SELECTION_CHANGED) {
            typeViewSelectionChangeEvent.add(event)
        }

        // Technique 1: Context-menu copy/cut button click detection
        if (event.EventType == AccessibilityEvent.TYPE_VIEW_CLICKED && event.Text != null &&
            ((event.ContentDescription?.length ?: 0) < MAX_COPY_WORD_DETECTION_LENGTH && event.ContentDescription?.contains(copyWord, ignoreCase = true) == true ||
             (event.Text.toString().length) < MAX_COPY_WORD_DETECTION_LENGTH && event.Text.toString().contains(copyWord, ignoreCase = true) ||
             event.ContentDescription == "Cut" || event.ContentDescription == copyWord)
        ) {
            Log.d(TAG, "Copy captured - Context Menu Click heuristic triggered")
            return true
        }

        // Technique 2: Text selection collapsing changed heuristic (User selected text then clicked Copy)
        if (typeViewSelectionChangeEvent.size == 2) {
            val firstEvent = typeViewSelectionChangeEvent[0]
            val secondEvent = typeViewSelectionChangeEvent[1]
            if (secondEvent.FromIndex == secondEvent.ToIndex) {
                val success = (firstEvent.PackageName == secondEvent.PackageName &&
                               firstEvent.FromIndex != firstEvent.ToIndex &&
                               secondEvent.ClassName == firstEvent.ClassName) &&
                              secondEvent.Text.toString() == firstEvent.Text.toString()
                typeViewSelectionChangeEvent.clear()
                if (success) {
                    Log.d(TAG, "Copy captured - Text Selection Collapse heuristic triggered")
                    return true
                }
            }
        }

        // Technique 3: Subtree change window content change heuristic
        if ((event.ContentChangeTypes ?: 0) and AccessibilityEvent.CONTENT_CHANGE_TYPE_SUBTREE == 1 &&
            event.EventType == AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED && lastEvent != null
        ) {
            val previousEvent = lastEvent!!
            if (previousEvent.EventType == AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED &&
                previousEvent.Text?.size == 1 &&
                (previousEvent.Text.toString().contains(copyWord, ignoreCase = true) ||
                 previousEvent.ContentDescription?.contains(copyWord, ignoreCase = true) == true)
            ) {
                Log.d(TAG, "Copy captured - Subtree window change heuristic triggered")
                return true
            }
        }

        // Technique 4: Toast notification state change heuristic
        if (event.EventType == AccessibilityEvent.TYPE_NOTIFICATION_STATE_CHANGED &&
            event.ClassName?.toString()?.contains("Toast", ignoreCase = true) == true &&
            event.Text != null && event.Text.toString().contains(AEvent.copyKeyWords)
        ) {
            Log.d(TAG, "Copy captured - Toast detection heuristic triggered")
            return true
        }

        lastEvent = event.clone()
        return false
    }

    data class AEvent(
        var EventType: Int? = null,
        var EventTime: Long? = null,
        var PackageName: CharSequence? = null,
        var MovementGranularity: Int? = null,
        var Action: Int? = null,
        var ClassName: CharSequence? = null,
        var Text: List<CharSequence?>? = null,
        var ContentDescription: CharSequence? = null,
        var ContentChangeTypes: Int? = null,
        var CurrentItemIndex: Int? = null,
        var FromIndex: Int? = null,
        var ToIndex: Int? = null,
        var ScrollX: Int? = null,
        var ScrollY: Int? = null
    ) {
        companion object {
            internal val copyKeyWords = "(copied)|(Copied)|(clipboard)".toRegex()

            fun from(event: AccessibilityEvent): AEvent {
                // Return a copy with stringified representations to avoid recycled object mutations.
                return AEvent(
                    EventType = event.eventType,
                    EventTime = event.eventTime,
                    PackageName = event.packageName?.toString(),
                    MovementGranularity = event.movementGranularity,
                    Action = event.action,
                    ClassName = event.className?.toString(),
                    Text = event.text?.map { it?.toString() } ?: emptyList(),
                    ContentChangeTypes = event.contentChangeTypes,
                    ContentDescription = event.contentDescription?.toString(),
                    CurrentItemIndex = event.currentItemIndex,
                    FromIndex = event.fromIndex,
                    ToIndex = event.toIndex,
                    ScrollX = event.scrollX,
                    ScrollY = event.scrollY
                )
            }
        }
    }

    private fun AEvent.clone(): AEvent = this.copy(Text = ArrayList(this.Text ?: listOf()))
}
