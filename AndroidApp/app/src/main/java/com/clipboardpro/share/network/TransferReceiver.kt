package com.clipboardpro.share.network

import android.util.Log
import com.clipboardpro.share.model.ClipboardItemPayload
import com.clipboardpro.share.model.ClipboardItemType
import com.clipboardpro.share.model.TransferDirection
import com.clipboardpro.share.model.TransferItem
import com.clipboardpro.share.model.TransferStatus
import com.google.gson.Gson
import java.io.BufferedInputStream
import java.io.DataInputStream
import java.io.File
import java.io.FileOutputStream
import java.net.ServerSocket
import java.nio.ByteBuffer
import java.nio.ByteOrder

class TransferReceiver(
    private val context: android.content.Context,
    private val onTransferUpdate: (TransferItem) -> Unit,
    private val onTextReceived: (String, String) -> Unit
) {
    private val TAG = "TransferReceiver"
    private val gson = Gson()
    private var serverSocket: ServerSocket? = null
    @Volatile private var isRunning = false
    private var boundPort: Int = -1

    fun start(): Int {
        isRunning = true

        for (port in 50506..50515) {
            try {
                serverSocket = ServerSocket(port)
                boundPort = port
                break
            } catch (e: Exception) {
                Log.w(TAG, "Port $port busy, trying next.")
            }
        }

        if (serverSocket == null) {
            Log.e(TAG, "Could not bind to any TCP port in range 50506-50515")
            isRunning = false
            return -1
        }

        Log.i(TAG, "TCP listener bound on port $boundPort")
        Thread(::acceptLoop, "ClipPro-TCP-Accept").apply { isDaemon = true; start() }
        return boundPort
    }

    private fun acceptLoop() {
        while (isRunning) {
            try {
                val clientSocket = serverSocket?.accept() ?: break
                val peerIp = clientSocket.inetAddress.hostAddress ?: "unknown"
                Log.i(TAG, "Incoming connection from $peerIp")
                Thread({ handleClient(clientSocket, peerIp) }, "ClipPro-TCP-Handle").apply { isDaemon = true; start() }
            } catch (e: Exception) {
                if (!isRunning) break
                Log.w(TAG, "Accept error: ${e.message}")
            }
        }
    }

    private fun handleClient(socket: java.net.Socket, peerIp: String) {
        socket.use { s ->
            try {
                val dis = DataInputStream(BufferedInputStream(s.getInputStream()))

                // 1. Read JSON metadata length (Little Endian Int32)
                val lenBytes = ByteArray(4)
                dis.readFully(lenBytes)
                val jsonLen = ByteBuffer.wrap(lenBytes).order(ByteOrder.LITTLE_ENDIAN).int

                if (jsonLen <= 0 || jsonLen > 50 * 1024 * 1024) {
                    Log.e(TAG, "Invalid JSON length: $jsonLen")
                    return
                }

                // 2. Read JSON metadata
                val jsonBytes = ByteArray(jsonLen)
                dis.readFully(jsonBytes)
                val jsonStr = String(jsonBytes, Charsets.UTF_8)
                Log.d(TAG, "Received JSON: $jsonStr")

                val item = gson.fromJson(jsonStr, ClipboardItemPayload::class.java) ?: return

                // 3. Handle TEXT items
                if (item.Type == ClipboardItemType.TEXT.value || item.Type == ClipboardItemType.URL.value) {
                    Log.i(TAG, "Text received from $peerIp: ${item.Content.take(100)}")
                    onTextReceived(item.Content, peerIp)
                    return
                }

                // 4. Handle BINARY items (Image=1, Path=7)
                if (item.Type == ClipboardItemType.IMAGE.value || item.Type == ClipboardItemType.PATH.value) {
                    val lenBytesLong = ByteArray(8)
                    dis.readFully(lenBytesLong)
                    val payloadLen = ByteBuffer.wrap(lenBytesLong).order(ByteOrder.LITTLE_ENDIAN).long

                    if (payloadLen < 0 || payloadLen > 2L * 1024 * 1024 * 1024) {
                        Log.e(TAG, "Invalid payload length: $payloadLen")
                        return
                    }

                    val rawName = if (item.Type == ClipboardItemType.IMAGE.value) {
                        "img_${System.currentTimeMillis()}.png"
                    } else {
                        item.Content.substringAfterLast('\\').substringAfterLast('/')
                    }

                    // Security sanitization
                    val safeName = sanitizeFileName(rawName)

                    val transfer = TransferItem(
                        fileName = safeName,
                        direction = TransferDirection.RECEIVE,
                        totalBytes = payloadLen,
                        status = TransferStatus.ACTIVE,
                        peerName = peerIp
                    )
                    onTransferUpdate(transfer)

                    var success = false
                    var savedUri: String? = null
                    try {
                        val prefs = context.getSharedPreferences("localshare_prefs", android.content.Context.MODE_PRIVATE)
                        val subFolder = prefs.getString("save_folder", "Received") ?: "Received"

                        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                            val resolver = context.contentResolver
                            val contentValues = android.content.ContentValues().apply {
                                put(android.provider.MediaStore.Downloads.DISPLAY_NAME, safeName)
                                put(android.provider.MediaStore.Downloads.RELATIVE_PATH, "Download/$subFolder")
                            }
                            val uri = resolver.insert(android.provider.MediaStore.Downloads.EXTERNAL_CONTENT_URI, contentValues)
                            if (uri != null) {
                                savedUri = uri.toString()
                                resolver.openOutputStream(uri).use { fos ->
                                    if (fos != null) {
                                        val buffer = ByteArray(81920)
                                        var totalRead = 0L
                                        while (totalRead < payloadLen) {
                                            val toRead = minOf(buffer.size.toLong(), payloadLen - totalRead).toInt()
                                            val read = dis.read(buffer, 0, toRead)
                                            if (read == -1) break
                                            fos.write(buffer, 0, read)
                                            totalRead += read
                                            val pct = ((totalRead * 100) / payloadLen).toInt()
                                            onTransferUpdate(transfer.copy(
                                                progress = pct,
                                                bytesTransferred = totalRead,
                                                status = TransferStatus.ACTIVE
                                            ))
                                        }
                                        success = true
                                    }
                                }
                            }
                        } else {
                            val downloadsDir = android.os.Environment.getExternalStoragePublicDirectory(android.os.Environment.DIRECTORY_DOWNLOADS)
                            val receivedDir = File(downloadsDir, subFolder)
                            if (!receivedDir.exists()) receivedDir.mkdirs()
                            val targetFile = File(receivedDir, safeName)
                            savedUri = android.net.Uri.fromFile(targetFile).toString()
                            java.io.FileOutputStream(targetFile).use { fos ->
                                val buffer = ByteArray(81920)
                                var totalRead = 0L
                                while (totalRead < payloadLen) {
                                    val toRead = minOf(buffer.size.toLong(), payloadLen - totalRead).toInt()
                                    val read = dis.read(buffer, 0, toRead)
                                    if (read == -1) break
                                    fos.write(buffer, 0, read)
                                    totalRead += read
                                    val pct = ((totalRead * 100) / payloadLen).toInt()
                                    onTransferUpdate(transfer.copy(
                                        progress = pct,
                                        bytesTransferred = totalRead,
                                        status = TransferStatus.ACTIVE
                                    ))
                                }
                                success = true
                            }
                        }
                    } catch (e: Exception) {
                        Log.e(TAG, "Error writing file: ${e.message}")
                    }

                    if (success) {
                        onTransferUpdate(transfer.copy(
                            progress = 100,
                            bytesTransferred = payloadLen,
                            status = TransferStatus.COMPLETED,
                            fileUri = savedUri
                        ))
                        Log.i(TAG, "File received: $safeName, uri: $savedUri")
                    } else {
                        onTransferUpdate(transfer.copy(
                            status = TransferStatus.FAILED
                        ))
                    }
                }
                Unit
            } catch (e: Exception) {
                Log.e(TAG, "Error handling client: ${e.message}")
            }
        }
    }

    private fun sanitizeFileName(name: String): String {
        var safe = File(name).name  // Strip any directory separators
        safe = safe.replace("..", "").replace("/", "").replace("\\", "")
        val illegalChars = Regex("[<>:\"/\\\\|?*\\x00-\\x1F]")
        safe = illegalChars.replace(safe, "_")
        return if (safe.isBlank()) "file_${System.currentTimeMillis()}.dat" else safe
    }

    fun getBoundPort(): Int = boundPort

    fun stop() {
        isRunning = false
        try { serverSocket?.close() } catch (e: Exception) { }
        serverSocket = null
        Log.i(TAG, "TCP Receiver stopped.")
    }
}
