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

class MainActivity : ComponentActivity() {

    private var shareService: LocalShareService? = null
    private var isServiceBound by mutableStateOf(false)

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
        requestRequiredPermissions()
        setContent {
            val context = androidx.compose.ui.platform.LocalContext.current
            var themeMode by remember {
                mutableStateOf(
                    context.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                        .getString("theme_mode", "system") ?: "system"
                )
            }

            ClipboardProTheme(themeMode = themeMode) {
                MainScreen(
                    serviceProvider = { shareService },
                    isServiceBound = isServiceBound,
                    themeMode = themeMode,
                    onThemeModeChanged = { newMode -> themeMode = newMode }
                )
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
    }

    override fun onResume() {
        super.onResume()
        try {
            val cb = getSystemService(Context.CLIPBOARD_SERVICE) as android.content.ClipboardManager
            if (cb.hasPrimaryClip()) {
                val clip = cb.primaryClip
                if (clip != null && clip.itemCount > 0) {
                    val text = clip.getItemAt(0).text?.toString()
                    if (!text.isNullOrBlank()) {
                        shareService?.addClipboardItem(text)
                    }
                }
            }
        } catch (e: Exception) {
            // Ignore security exception if clipboard access is denied
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
