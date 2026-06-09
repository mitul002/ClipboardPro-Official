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
import android.util.Log
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
        Log.d("MainActivity", "onCreate started")

        try {
            // Check license/trial status safely
            val licenseService = com.clipboardpro.vault.service.LicenseService(this)
            isAppAllowedState.value = licenseService.isAppAllowed()
            Log.d("MainActivity", "App allowed: ${isAppAllowedState.value}")
        } catch (e: Exception) {
            Log.e("MainActivity", "License check failed: ${e.message}")
            isAppAllowedState.value = true // Safe fallback to allow app to open
        }

        requestRequiredPermissions()

        setContent {
            val context = androidx.compose.ui.platform.LocalContext.current
            var themeMode by remember {
                mutableStateOf(
                    try {
                        context.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                            .getString("theme_mode", "system") ?: "system"
                    } catch (e: Exception) { "system" }
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

    override fun onResume() {
        super.onResume()
        try {
            isAppAllowedState.value = com.clipboardpro.vault.service.LicenseService(this).isAppAllowed()
            // Capture current clipboard via transparent trampoline
            ClipboardCaptureActivity.launch(this)
        } catch (e: Exception) {
            Log.e("MainActivity", "onResume error: ${e.message}")
        }
    }

    private fun startAndBindService() {
        try {
            val intent = Intent(this, LocalShareService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(intent)
            } else {
                startService(intent)
            }
            bindService(intent, serviceConnection, Context.BIND_AUTO_CREATE)
            Log.d("MainActivity", "LocalShareService started and binding...")

            // Launch Yoink overlay bubble if configured and permitted
            val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
            if (prefs.getBoolean("floating_yoink_enabled", false) && android.provider.Settings.canDrawOverlays(this)) {
                startService(Intent(this, com.clipboardpro.vault.service.FloatingYoinkService::class.java))
            }
        } catch (e: Exception) {
            Log.e("MainActivity", "Service startup failed: ${e.message}")
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
