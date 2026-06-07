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
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.*
import androidx.core.content.ContextCompat
import com.clipboardpro.share.service.LocalShareService
import com.clipboardpro.share.ui.theme.ClipboardProTheme
import com.clipboardpro.share.ui.MainScreen
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.service.ContentParser
import com.clipboardpro.share.model.ClipboardItemType

class MainActivity : ComponentActivity() {

    private val clipboardListener = android.content.ClipboardManager.OnPrimaryClipChangedListener {
        checkClipboardAndSave()
    }

    private fun checkClipboardAndSave() {
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager ?: return
        val clip = cm.primaryClip ?: return
        if (clip.itemCount == 0) return
        val text = clip.getItemAt(0)?.text?.toString() ?: return
        val clean = text.trim()
        if (clean.isBlank()) return

        val database = AppDatabase.getDatabase(this)
        lifecycleScope.launch(Dispatchers.IO) {
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
                
                // Trim history
                val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                val maxItems = prefs.getInt("max_history_items", 200)
                dao.trimExcessItems(maxItems)
            } catch (e: Exception) {
                android.util.Log.e("MainActivity", "Failed to auto-save clipboard: ${e.localizedMessage}")
            }
        }
    }

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

    override fun onResume() {
        super.onResume()
        isAppAllowedState.value = com.clipboardpro.share.service.LicenseService(this).isAppAllowed()
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager
        cm?.addPrimaryClipChangedListener(clipboardListener)
        checkClipboardAndSave()
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) {
            checkClipboardAndSave()
        }
    }

    override fun onPause() {
        super.onPause()
        val cm = getSystemService(Context.CLIPBOARD_SERVICE) as? android.content.ClipboardManager
        cm?.removePrimaryClipChangedListener(clipboardListener)
    }

    override fun onDestroy() {
        super.onDestroy()
        if (isServiceBound) {
            unbindService(serviceConnection)
            isServiceBound = false
        }
    }
}
