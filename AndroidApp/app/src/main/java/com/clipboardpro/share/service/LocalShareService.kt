package com.clipboardpro.share.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.wifi.WifiManager
import android.os.Binder
import android.os.Environment
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.clipboardpro.share.MainActivity
import com.clipboardpro.share.R
import com.clipboardpro.share.model.PeerDevice
import com.clipboardpro.share.model.TransferItem
import com.clipboardpro.share.network.DiscoveryManager
import com.clipboardpro.share.network.TransferReceiver
import com.clipboardpro.share.network.TransferSender
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import java.io.File

class LocalShareService : Service() {

    companion object {
        private const val TAG = "LocalShareService"
        private const val CHANNEL_ID = "ClipboardPro_Share"
        private const val NOTIFICATION_ID = 1001
    }

    inner class LocalBinder : Binder() {
        fun getService(): LocalShareService = this@LocalShareService
    }
    private val binder = LocalBinder()

    private val _peers = MutableStateFlow<List<PeerDevice>>(emptyList())
    val peers: StateFlow<List<PeerDevice>> = _peers

    private val _transfers = MutableStateFlow<List<TransferItem>>(emptyList())
    val transfers: StateFlow<List<TransferItem>> = _transfers

    private val _receivedTexts = MutableStateFlow<List<Pair<String, String>>>(emptyList())
    val receivedTexts: StateFlow<List<Pair<String, String>>> = _receivedTexts

    private var multicastLock: WifiManager.MulticastLock? = null
    private var discoveryManager: DiscoveryManager? = null
    private var transferReceiver: TransferReceiver? = null
    private lateinit var transferSender: TransferSender
    private var tcpPort: Int = -1

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification("Scanning for nearby devices..."))

        acquireMulticastLock()
        initNetworking()
        Log.i(TAG, "LocalShareService started.")
    }

    private fun acquireMulticastLock() {
        val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        multicastLock = wifiManager.createMulticastLock("ClipboardProDiscovery").apply {
            setReferenceCounted(true)
            acquire()
        }
        Log.i(TAG, "MulticastLock acquired.")
    }

    private fun initNetworking() {
        val instanceId = generateInstanceId()
        val deviceName = android.os.Build.MODEL

        // Start TCP receiver first to get port
        transferReceiver = TransferReceiver(
            context = this,
            onTransferUpdate = { item -> updateTransfer(item) },
            onTextReceived = { text, from ->
                val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                val autoClip = prefs.getBoolean("auto_clipboard", true)
                if (autoClip) {
                    val clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                    clipboardManager.setPrimaryClip(ClipData.newPlainText("ClipboardPro Sync", text))
                }
                val resolvedName = _peers.value.find { it.ip == from }?.name ?: from
                _receivedTexts.value = _receivedTexts.value + Pair(text, resolvedName)
                updateNotification("Text received from $resolvedName")
                Log.i(TAG, "Text from $resolvedName: ${text.take(50)}")
            }
        )
        tcpPort = transferReceiver!!.start()

        if (tcpPort < 0) {
            Log.e(TAG, "TCP Receiver failed to bind! Networking unavailable.")
            updateNotification("Network error — TCP bind failed")
            return
        }

        // Start UDP discovery
        discoveryManager = DiscoveryManager(
            deviceName = deviceName,
            tcpPort = tcpPort,
            instanceId = instanceId,
            onPeersUpdated = { peerList ->
                _peers.value = peerList.sortedBy { it.name }
                updateNotification(
                    if (peerList.isEmpty()) "Searching for devices..."
                    else "${peerList.size} device(s) found"
                )
            }
        )
        discoveryManager!!.start()

        // Init sender
        transferSender = TransferSender(
            onTransferUpdate = { item -> updateTransfer(item) }
        )

        Log.i(TAG, "Networking initialized. TCP:$tcpPort UDP:50505 ID:$instanceId")
    }

    private fun generateInstanceId(): String {
        val prefs = getSharedPreferences("clipboardpro", Context.MODE_PRIVATE)
        var id = prefs.getString("instance_id", null)
        if (id == null) {
            id = java.util.UUID.randomUUID().toString().substring(0, 8)
            prefs.edit().putString("instance_id", id).apply()
        }
        return id
    }

    fun sendFile(file: File, peer: PeerDevice) {
        transferSender.sendFile(file, peer)
    }

    fun sendText(text: String, peer: PeerDevice) {
        transferSender.sendText(text, peer)
    }

    private fun updateTransfer(item: TransferItem) {
        val resolvedPeerName = _peers.value.find { it.ip == item.peerName }?.name ?: item.peerName
        val resolvedItem = if (resolvedPeerName != item.peerName) {
            item.copy(peerName = resolvedPeerName)
        } else {
            item
        }

        val current = _transfers.value.toMutableList()
        val existingIdx = current.indexOfFirst { it.id == resolvedItem.id }
        if (existingIdx >= 0) {
            current[existingIdx] = resolvedItem
        } else {
            current.add(0, resolvedItem)
        }
        _transfers.value = current
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "ClipboardPro Local Share",
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = "Local file sharing service"
        }
        (getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager)
            .createNotificationChannel(channel)
    }

    private fun buildNotification(text: String): Notification {
        val intent = Intent(this, MainActivity::class.java)
        val pi = PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_IMMUTABLE)
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("ClipboardPro Share")
            .setContentText(text)
            .setSmallIcon(R.drawable.logo)
            .setContentIntent(pi)
            .setOngoing(true)
            .build()
    }

    private fun updateNotification(text: String) {
        val nm = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        nm.notify(NOTIFICATION_ID, buildNotification(text))
    }

    override fun onDestroy() {
        discoveryManager?.stop()
        transferReceiver?.stop()
        if (multicastLock?.isHeld == true) multicastLock?.release()
        Log.i(TAG, "LocalShareService destroyed.")
        super.onDestroy()
    }
}
