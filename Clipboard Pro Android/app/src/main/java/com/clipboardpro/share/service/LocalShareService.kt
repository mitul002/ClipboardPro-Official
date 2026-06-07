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
import android.net.Uri
import android.net.wifi.WifiManager
import android.os.Binder
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.clipboardpro.share.MainActivity
import com.clipboardpro.share.R
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.model.ClipboardItemType
import com.clipboardpro.share.model.PeerDevice
import com.clipboardpro.share.model.TransferItem
import com.clipboardpro.share.network.DiscoveryManager
import com.clipboardpro.share.network.TransferReceiver
import com.clipboardpro.share.network.TransferSender
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.File
import java.io.FileOutputStream
import java.security.MessageDigest

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

    private val job = SupervisorJob()
    private val scope = CoroutineScope(Dispatchers.Main + job)
    private lateinit var database: AppDatabase

    private val _peers = MutableStateFlow<List<PeerDevice>>(emptyList())
    val peers: StateFlow<List<PeerDevice>> = _peers

    private val _transfers = MutableStateFlow<List<TransferItem>>(emptyList())
    val transfers: StateFlow<List<TransferItem>> = _transfers

    private var multicastLock: WifiManager.MulticastLock? = null
    private var discoveryManager: DiscoveryManager? = null
    private var transferReceiver: TransferReceiver? = null
    private lateinit var transferSender: TransferSender
    private var tcpPort: Int = -1

    override fun onBind(intent: Intent?): IBinder = binder

    fun addClipboardItem(text: String, category: String? = null, title: String? = null) {
        val clean = text.trim()
        if (clean.isBlank()) return
        
        scope.launch(Dispatchers.IO) {
            try {
                val dao = database.clipboardDao()
                val existing = dao.getAllItems().find { it.content == clean }
                val type = ContentParser.detectType(clean)
                val isJsonStr = clean.startsWith("{") || clean.startsWith("[")
                val isSensitive = ContentParser.isSensitive(clean)
                
                val entity = if (existing != null) {
                    existing.copy(
                        timestamp = System.currentTimeMillis(),
                        category = category ?: existing.category,
                        title = title ?: existing.title,
                        isSensitive = isSensitive
                    )
                } else {
                    ClipboardItemEntity(
                        id = java.util.UUID.randomUUID().toString(),
                        content = clean,
                        type = type.value,
                        timestamp = System.currentTimeMillis(),
                        category = category,
                        title = title,
                        isJson = isJsonStr,
                        isSensitive = isSensitive,
                        isMasked = isSensitive
                    )
                }
                dao.insertItem(entity)
                
                // Trim history according to user settings
                val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                val maxItems = prefs.getInt("max_history_items", 200)
                dao.trimExcessItems(maxItems)
            } catch (e: Throwable) {
                Log.e(TAG, "Failed to add clipboard item: ${e.localizedMessage}", e)
            }
        }
    }

    fun removeClipboardItem(id: String) {
        scope.launch(Dispatchers.IO) {
            try {
                val dao = database.clipboardDao()
                val allItems = dao.getAllItems()
                val item = allItems.find { it.id == id }
                if (item != null) {
                    if (!item.imagePath.isNullOrBlank()) {
                        try {
                            val file = File(item.imagePath)
                            if (file.exists()) file.delete()
                        } catch (e: Exception) {
                            Log.e(TAG, "Failed to delete image: ${e.localizedMessage}")
                        }
                    }
                    dao.deleteItem(item)
                }
            } catch (e: Throwable) {
                Log.e(TAG, "Failed to remove clipboard item: ${e.localizedMessage}", e)
            }
        }
    }

    fun clearClipboardHistory() {
        scope.launch(Dispatchers.IO) {
            try {
                val dao = database.clipboardDao()
                val allItems = dao.getAllItems()
                allItems.forEach { item ->
                    if (!item.imagePath.isNullOrBlank()) {
                        try {
                            val file = File(item.imagePath)
                            if (file.exists()) file.delete()
                        } catch (e: Exception) { }
                    }
                }
                dao.clearAll()
            } catch (e: Throwable) {
                Log.e(TAG, "Failed to clear clipboard history: ${e.localizedMessage}", e)
            }
        }
    }

    // Tracks the last clip label we set ourselves so we can ignore our own events
    @Volatile private var lastSelfSetClipLabel: String? = null

    private fun handleClipboardChange(clipboardManager: ClipboardManager) {
        scope.launch(Dispatchers.IO) {
            try {
                val clip = try { clipboardManager.primaryClip } catch (e: Exception) { null }
                if (clip == null || clip.itemCount == 0) return@launch

                val clipLabel = clip.description?.label?.toString() ?: ""

                // Ignore clipboard events that we set ourselves (from received texts or user copy)
                if (clipLabel == "ClipboardPro Sync" || clipLabel == lastSelfSetClipLabel) return@launch

                val item = clip.getItemAt(0)
                val text = item?.text?.toString()
                val uri = item?.uri

                if (!text.isNullOrBlank()) {
                    addClipboardItem(text)
                } else if (uri != null) {
                    val typeStr = try { contentResolver.getType(uri) } catch (e: Exception) { null } ?: ""
                    if (typeStr.startsWith("image/") || uri.path?.endsWith(".png") == true || uri.path?.endsWith(".jpg") == true) {
                        processClipboardImage(uri)
                    }
                }
            } catch (e: Throwable) {
                Log.e(TAG, "Error handling clipboard change: ${e.localizedMessage}")
            }
        }
    }

    private fun processClipboardImage(uri: Uri) {
        scope.launch(Dispatchers.IO) {
            try {
                val inputStream = contentResolver.openInputStream(uri) ?: return@launch
                val bytes = inputStream.readBytes()
                if (bytes.isEmpty()) return@launch

                val md = MessageDigest.getInstance("SHA-256")
                val digest = md.digest(bytes)
                val hash = digest.joinToString("") { "%02x".format(it) }

                val dao = database.clipboardDao()
                val existing = dao.getItemByHash(hash)
                if (existing != null) {
                    dao.insertItem(existing.copy(timestamp = System.currentTimeMillis()))
                    return@launch
                }

                val imagesDir = File(filesDir, "images").apply { mkdirs() }
                val imageFile = File(imagesDir, "clip_$hash.png")
                FileOutputStream(imageFile).use { fos -> fos.write(bytes) }

                val entity = ClipboardItemEntity(
                    id = java.util.UUID.randomUUID().toString(),
                    content = "Copied Image",
                    imagePath = imageFile.absolutePath,
                    imageHash = hash,
                    type = ClipboardItemType.IMAGE.value,
                    timestamp = System.currentTimeMillis()
                )
                dao.insertItem(entity)

                val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                val maxItems = prefs.getInt("max_history_items", 200)
                dao.trimExcessItems(maxItems)
                Log.i(TAG, "Processed new clipboard image: $hash")
            } catch (e: Exception) {
                Log.e(TAG, "Error processing clipboard image: ${e.localizedMessage}")
            }
        }
    }

    fun vacuumDatabase() {
        scope.launch(Dispatchers.IO) {
            try {
                database.openHelper.writableDatabase.execSQL("VACUUM")
                Log.i(TAG, "Database vacuum completed.")
            } catch (e: Exception) {
                Log.e(TAG, "Failed to vacuum DB: ${e.localizedMessage}")
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        database = AppDatabase.getDatabase(this)
        
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification("Scanning for nearby devices..."))

        val clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        clipboardManager.addPrimaryClipChangedListener {
            handleClipboardChange(clipboardManager)
        }

        acquireMulticastLock()
        initNetworking()
        Log.i(TAG, "LocalShareService started with Room database.")
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
                scope.launch(Dispatchers.IO) {
                    val resolvedName = _peers.value.find { it.ip == from }?.name ?: from

                    val prefs = getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)
                    val autoClip = prefs.getBoolean("auto_clipboard", true)
                    if (autoClip) {
                        try {
                            // Mark that we are about to set the clipboard ourselves so the listener ignores it
                            lastSelfSetClipLabel = "ClipboardPro Sync"
                            val clipboardManager = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            clipboardManager.setPrimaryClip(ClipData.newPlainText("ClipboardPro Sync", text))
                        } catch (e: Exception) {
                            Log.e(TAG, "Failed to set clipboard: ${e.localizedMessage}")
                        }
                    }

                    // Add received text directly to Room Database — do NOT go through clipboard listener to avoid duplication
                    addClipboardItem(
                        text = text,
                        category = "Received",
                        title = "From $resolvedName"
                    )

                    updateNotification("Text received from $resolvedName")
                    Log.i(TAG, "Text from $resolvedName: ${text.take(50)}")
                }
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

    fun removeTransfer(id: String) {
        _transfers.value = _transfers.value.filter { it.id != id }
    }

    fun clearTransfers() {
        _transfers.value = emptyList()
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
        scope.cancel() // Cancel all database tasks safely
        Log.i(TAG, "LocalShareService destroyed.")
        super.onDestroy()
    }
}
