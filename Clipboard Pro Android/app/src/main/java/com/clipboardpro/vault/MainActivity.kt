package com.clipboardpro.vault

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
import com.clipboardpro.vault.service.ClipboardCaptureActivity
import com.clipboardpro.vault.service.LocalShareService
import com.clipboardpro.vault.ui.theme.ClipboardProTheme
import com.clipboardpro.vault.ui.MainScreen

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
        isAppAllowedState.value = com.clipboardpro.vault.service.LicenseService(this).isAppAllowed()
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
                    com.clipboardpro.vault.ui.LicenseGateScreen(
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
        isAppAllowedState.value = com.clipboardpro.vault.service.LicenseService(this).isAppAllowed()
        // Capture whatever is currently on the clipboard when user opens the app.
        // ClipboardCaptureActivity handles deduplication and DB insertion safely.
        ClipboardCaptureActivity.launch(this)
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
            startService(Intent(this, com.clipboardpro.vault.service.FloatingYoinkService::class.java))
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
