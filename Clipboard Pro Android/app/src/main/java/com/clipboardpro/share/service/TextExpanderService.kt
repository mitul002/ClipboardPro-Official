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
    }

    private val job = SupervisorJob()
    private val scope = CoroutineScope(Dispatchers.Main + job)
    
    private var snippetsList = listOf<SnippetItemEntity>()
    private var lastExpandedText = ""
    private var lastTrigger = ""
    private var isExpanding = false

    override fun onCreate() {
        super.onCreate()
        
        // Listen to database snippets dynamically
        val db = AppDatabase.getDatabase(this)
        scope.launch {
            db.snippetDao().getAllSnippetsFlow().collectLatest { list ->
                snippetsList = list
                Log.d(TAG, "Loaded ${list.size} snippets for expansion.")
            }
        }
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent) {
        if (event.eventType != AccessibilityEvent.TYPE_VIEW_TEXT_CHANGED) return
        if (isExpanding) return // Prevent recursive events during substitution

        val sourceNode = event.source ?: return
        
        // Ensure it is an editable field
        if (!sourceNode.isEditable && !sourceNode.className.toString().contains("Edit", ignoreCase = true)) {
            sourceNode.recycle()
            return
        }

        val text = sourceNode.text?.toString() ?: ""
        if (text.isBlank()) {
            sourceNode.recycle()
            return
        }

        // Check for Undo window (if user deletes backspace right after expansion)
        if (lastExpandedText.isNotEmpty() && text == lastExpandedText.dropLast(1)) {
            // Revert expansion back to clean trigger
            isExpanding = true
            val restoredText = text.substring(0, text.length - (lastExpandedText.length - 1 - lastTrigger.length)) + lastTrigger
            val arguments = Bundle().apply {
                putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, restoredText)
            }
            sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, arguments)
            
            val selectionArgs = Bundle().apply {
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, restoredText.length)
                putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, restoredText.length)
            }
            sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selectionArgs)
            
            lastExpandedText = ""
            lastTrigger = ""
            isExpanding = false
            sourceNode.recycle()
            return
        }

        // Normal Expansion Scan
        for (snippet in snippetsList) {
            val trigger = snippet.trigger
            if (text.endsWith(trigger)) {
                isExpanding = true
                
                val newText = text.substring(0, text.length - trigger.length) + snippet.content
                
                val arguments = Bundle().apply {
                    putCharSequence(AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE, newText)
                }
                sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, arguments)

                // Place cursor at the end
                val selectionArgs = Bundle().apply {
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_START_INT, newText.length)
                    putInt(AccessibilityNodeInfo.ACTION_ARGUMENT_SELECTION_END_INT, newText.length)
                }
                sourceNode.performAction(AccessibilityNodeInfo.ACTION_SET_SELECTION, selectionArgs)

                // Arm undo state
                lastTrigger = trigger
                lastExpandedText = newText

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
