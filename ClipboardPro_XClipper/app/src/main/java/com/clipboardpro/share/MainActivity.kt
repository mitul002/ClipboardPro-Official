package com.clipboardpro.share

import android.Manifest
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.ServiceConnection
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.os.IBinder
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.*
import androidx.core.content.ContextCompat
import com.clipboardpro.share.service.ClipboardCaptureActivity
import com.clipboardpro.share.service.LocalShareService
import com.clipboardpro.share.ui.theme.ClipboardProTheme
import com.clipboardpro.share.ui.MainScreen

class MainActivity : ComponentActivity() {

    private var shareService: LocalShareService? = null
    private var isServiceBound by mutableStateOf(false)
    private val isAppAllowedState = mutableStateOf(true)

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName?, binder: IBinder?) {
            val localBinder = binder as? LocalShareService.LocalBinder
            shareService = localBinder?.getService()
            isServiceBound = true
        }
        override fun onServiceDisconnected(name: ComponentName?) {
            shareService = null
            isServiceBound = false
        }
    }

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { granted ->
        if (granted.values.any { it }) {
            startAndBindService()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        isAppAllowedState.value = com.clipboardpro.share.service.LicenseService(this).isAppAllowed()
        requestRequiredPermissions()
        setContent {
            val context = androidx.compose.ui.platform.LocalContext.current
            var themeMode by remember {
                mutableStateOf(
                    context.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                        .getString("theme_mode", "system") ?: "system"
                )
            }

            val isAllowed by isAppAllowedState

            ClipboardProTheme(themeMode = themeMode) {
                if (isAllowed) {
                    MainScreen(
                        serviceProvider = { shareService },
                        isServiceBound = isServiceBound,
                        themeMode = themeMode,
                        onThemeModeChanged = { newMode -> themeMode = newMode }
                    )
                } else {
                    com.clipboardpro.share.ui.LicenseGateScreen(
                        onActivationSuccess = {
                            isAppAllowedState.value = true
                        }
                    )
                }
            }
        }
    }

    /**
     * When the app is opened by the user (comes to foreground), also capture
     * the current clipboard. This is the fallback path for when the user
     * copied something while the app was closed and then opens the app manually.
     *
     * The actual "real-time" copy detection is handled by the AccessibilityService
     * (TextExpanderService) → ClipboardCaptureActivity pipeline.
     */
    override fun onResume() {
        super.onResume()
        isAppAllowedState.value = com.clipboardpro.share.service.LicenseService(this).isAppAllowed()
        
        // Capture whatever is currently on the clipboard directly when user opens the app.
        // This avoids launching ClipboardCaptureActivity which steals focus and causes an infinite loop.
        try {
            val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager
            if (cm != null && cm.hasPrimaryClip()) {
                val clip = cm.primaryClip
                if (clip != null && clip.itemCount > 0) {
                    val text = clip.getItemAt(0)?.text?.toString()?.trim()
                    if (!text.isNullOrBlank()) {
                        val label = clip.description?.label?.toString() ?: ""
                        if (label != "ClipExpand" && label != "ClipboardPro Sync") {
                            kotlinx.coroutines.CoroutineScope(kotlinx.coroutines.Dispatchers.IO).launch {
                                try {
                                    val db = com.clipboardpro.share.data.AppDatabase.getDatabase(applicationContext)
                                    val dao = db.clipboardDao()
                                    val existing = dao.getAllItems().find { it.content == text }
                                    if (existing != null) {
                                        dao.insertItem(existing.copy(timestamp = System.currentTimeMillis()))
                                        Log.d("MainActivity", "Bumped timestamp for existing clip.")
                                    } else {
                                        val type = com.clipboardpro.share.service.ContentParser.detectType(text)
                                        val isSensitive = com.clipboardpro.share.service.ContentParser.isSensitive(text)
                                        val entity = com.clipboardpro.share.data.ClipboardItemEntity(
                                            id = java.util.UUID.randomUUID().toString(),
                                            content = text,
                                            type = type.value,
                                            timestamp = System.currentTimeMillis(),
                                            isSensitive = isSensitive,
                                            isMasked = isSensitive,
                                            isJson = text.startsWith("{") || text.startsWith("[")
                                        )
                                        dao.insertItem(entity)
                                        
                                        val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                                        val maxItems = prefs.getInt("max_history_items", 200)
                                        dao.trimExcessItems(maxItems)
                                        Log.d("MainActivity", "Directly saved clipboard item.")
                                    }
                                } catch (e: Exception) {
                                    Log.e("MainActivity", "Error saving clipboard: ${e.localizedMessage}")
                                }
                            }
                        }
                    }
                }
            }
        } catch (e: Exception) {
            Log.e("MainActivity", "Failed to read clipboard onResume: ${e.localizedMessage}")
        }
    }

    private fun requestRequiredPermissions() {
        val needed = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (!hasPermission(Manifest.permission.POST_NOTIFICATIONS))
                needed.add(Manifest.permission.POST_NOTIFICATIONS)
            if (!hasPermission(Manifest.permission.READ_MEDIA_IMAGES))
                needed.add(Manifest.permission.READ_MEDIA_IMAGES)
        } else {
            if (!hasPermission(Manifest.permission.READ_EXTERNAL_STORAGE))
                needed.add(Manifest.permission.READ_EXTERNAL_STORAGE)
        }

        if (needed.isNotEmpty()) {
            permissionLauncher.launch(needed.toTypedArray())
        } else {
            startAndBindService()
        }
    }

    private fun hasPermission(perm: String) =
        ContextCompat.checkSelfPermission(this, perm) == PackageManager.PERMISSION_GRANTED

    private fun startAndBindService() {
        val intent = Intent(this, LocalShareService::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(intent)
        } else {
            startService(intent)
        }
        bindService(intent, serviceConnection, Context.BIND_AUTO_CREATE)

        // Launch Yoink overlay bubble if configured and permitted
        val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
        if (prefs.getBoolean("floating_yoink_enabled", false) && android.provider.Settings.canDrawOverlays(this)) {
            startService(Intent(this, com.clipboardpro.share.service.FloatingYoinkService::class.java))
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        if (isServiceBound) {
            unbindService(serviceConnection)
            isServiceBound = false
        }
    }
}
