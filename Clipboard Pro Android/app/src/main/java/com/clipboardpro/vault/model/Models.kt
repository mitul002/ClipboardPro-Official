package com.clipboardpro.vault.model

data class PeerDevice(
    val name: String,
    val ip: String,
    val port: Int,
    var lastSeen: Long = System.currentTimeMillis()
)

data class TransferItem(
    val id: String = java.util.UUID.randomUUID().toString(),
    val fileName: String,
    val direction: TransferDirection,
    var progress: Int = 0,
    var status: TransferStatus = TransferStatus.PENDING,
    var bytesTransferred: Long = 0L,
    var totalBytes: Long = 0L,
    var peerName: String = "",
    val fileUri: String? = null
) {
    val sizeDisplay: String get() {
        if (totalBytes <= 0L) return ""
        return "${formatSize(bytesTransferred)} / ${formatSize(totalBytes)}"
    }

    private fun formatSize(bytes: Long): String {
        val units = listOf("B", "KB", "MB", "GB")
        var size = bytes.toDouble()
        var unitIdx = 0
        while (size >= 1024 && unitIdx < units.size - 1) {
            size /= 1024.0
            unitIdx++
        }
        return "%.1f %s".format(size, units[unitIdx])
    }
}

enum class TransferDirection { SEND, RECEIVE }

enum class TransferStatus { PENDING, ACTIVE, COMPLETED, FAILED, CANCELLED }

// Matches C# ClipboardItem.Type enum order exactly
enum class ClipboardItemType(val value: Int) {
    TEXT(0), IMAGE(1), URL(2), EMAIL(3), PHONE(4), CODE(5), COLOR(6), PATH(7), DIRECTORY(8)
}

data class ClipboardItemPayload(
    val Id: String = java.util.UUID.randomUUID().toString(),
    val Content: String = "",
    val Type: Int = 0,
    val Timestamp: String = "",
    val ImagePath: String? = null
)
