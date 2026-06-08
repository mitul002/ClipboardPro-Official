package com.clipboardpro.vault.network

import android.util.Log
import com.clipboardpro.vault.model.ClipboardItemPayload
import com.clipboardpro.vault.model.ClipboardItemType
import com.clipboardpro.vault.model.PeerDevice
import com.clipboardpro.vault.model.TransferDirection
import com.clipboardpro.vault.model.TransferItem
import com.clipboardpro.vault.model.TransferStatus
import com.google.gson.Gson
import java.io.BufferedOutputStream
import java.io.DataOutputStream
import java.io.File
import java.io.FileInputStream
import java.net.Socket
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.time.Instant
import java.util.UUID

class TransferSender(
    private val onTransferUpdate: (TransferItem) -> Unit
) {
    private val TAG = "TransferSender"
    private val gson = Gson()

    fun sendFile(file: File, peer: PeerDevice): TransferItem {
        val transfer = TransferItem(
            fileName = file.name,
            direction = TransferDirection.SEND,
            totalBytes = file.length(),
            status = TransferStatus.PENDING,
            peerName = peer.name
        )
        onTransferUpdate(transfer)

        Thread({
            performSend(file, peer, transfer)
        }, "ClipPro-Send-${file.name}").apply { isDaemon = true; start() }

        return transfer
    }

    fun sendText(text: String, peer: PeerDevice) {
        Thread({
            try {
                Socket(peer.ip, peer.port).use { socket ->
                    socket.soTimeout = 10000
                    val dos = DataOutputStream(BufferedOutputStream(socket.getOutputStream()))

                    val metadata = ClipboardItemPayload(
                        Id = UUID.randomUUID().toString(),
                        Content = text,
                        Type = ClipboardItemType.TEXT.value,
                        Timestamp = Instant.now().toString()
                    )
                    writeJsonFrame(dos, metadata)
                    dos.flush()
                    Log.i(TAG, "Text sent to ${peer.name}")
                }
            } catch (e: Exception) {
                Log.e(TAG, "Failed to send text: ${e.message}")
            }
        }, "ClipPro-SendText").apply { isDaemon = true; start() }
    }

    private fun performSend(file: File, peer: PeerDevice, transfer: TransferItem) {
        try {
            Socket().use { socket ->
                socket.connect(java.net.InetSocketAddress(peer.ip, peer.port), 5000)
                socket.soTimeout = 30000

                onTransferUpdate(transfer.copy(status = TransferStatus.ACTIVE, progress = 5))
                val dos = DataOutputStream(BufferedOutputStream(socket.getOutputStream()))

                // Determine item type
                val ext = file.extension.lowercase()
                val isImage = ext in listOf("png", "jpg", "jpeg", "bmp", "gif", "webp")
                val itemType = if (isImage) ClipboardItemType.IMAGE.value else ClipboardItemType.PATH.value

                val metadata = ClipboardItemPayload(
                    Id = UUID.randomUUID().toString(),
                    Content = file.name,
                    Type = itemType,
                    Timestamp = Instant.now().toString(),
                    ImagePath = if (isImage) file.name else null
                )

                // 1. Write JSON frame
                writeJsonFrame(dos, metadata)
                onTransferUpdate(transfer.copy(status = TransferStatus.ACTIVE, progress = 15))

                // 2. Write file length (Little Endian Int64)
                val fileLen = file.length()
                val lenBuf = ByteBuffer.allocate(8).order(ByteOrder.LITTLE_ENDIAN).putLong(fileLen).array()
                dos.write(lenBuf)

                // 3. Stream file in 80KB chunks
                FileInputStream(file).use { fis ->
                    val buffer = ByteArray(81920)
                    var totalSent = 0L
                    var read: Int
                    while (fis.read(buffer).also { read = it } != -1) {
                        dos.write(buffer, 0, read)
                        totalSent += read
                        val pct = 15 + ((totalSent * 80) / fileLen).toInt()
                        onTransferUpdate(transfer.copy(
                            status = TransferStatus.ACTIVE,
                            progress = pct,
                            bytesTransferred = totalSent
                        ))
                    }
                }

                dos.flush()
                onTransferUpdate(transfer.copy(progress = 100, status = TransferStatus.COMPLETED, bytesTransferred = fileLen))
                Log.i(TAG, "File sent successfully: ${file.name} → ${peer.name}")
            }
        } catch (e: Exception) {
            Log.e(TAG, "Send failed: ${e.message}")
            onTransferUpdate(transfer.copy(status = TransferStatus.FAILED))
        }
    }

    private fun writeJsonFrame(dos: DataOutputStream, metadata: ClipboardItemPayload) {
        val json = gson.toJson(metadata)
        val jsonBytes = json.toByteArray(Charsets.UTF_8)

        // Write JSON length as Little Endian Int32 (to match C# BinaryWriter)
        val lenBuf = ByteBuffer.allocate(4).order(ByteOrder.LITTLE_ENDIAN).putInt(jsonBytes.size).array()
        dos.write(lenBuf)
        dos.write(jsonBytes)
    }
}
