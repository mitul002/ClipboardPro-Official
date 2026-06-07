package com.clipboardpro.share.network

import android.util.Log
import com.clipboardpro.share.model.PeerDevice
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.NetworkInterface
import java.net.SocketTimeoutException
import java.util.concurrent.ConcurrentHashMap

class DiscoveryManager(
    private val deviceName: String,
    private val tcpPort: Int,
    private val instanceId: String,
    private val onPeersUpdated: (List<PeerDevice>) -> Unit
) {
    private val TAG = "DiscoveryManager"
    private var socket: DatagramSocket? = null
    private val discoveredPeers = ConcurrentHashMap<String, PeerDevice>()
    @Volatile private var isRunning = false

    fun start() {
        if (isRunning) return
        isRunning = true
        try {
            socket = DatagramSocket(50505).apply {
                broadcast = true
                soTimeout = 2000
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to open UDP socket: ${e.message}")
            // Port may be in use; try alternate
            try {
                socket = DatagramSocket(50505).apply { reuseAddress = true; broadcast = true; soTimeout = 2000 }
            } catch (e2: Exception) {
                Log.e(TAG, "Cannot open UDP socket at all: ${e2.message}")
                isRunning = false
                return
            }
        }

        Thread(::listenLoop, "ClipPro-UDP-Listen").apply { isDaemon = true; start() }
        Thread(::broadcastLoop, "ClipPro-UDP-Broadcast").apply { isDaemon = true; start() }
        Thread(::cleanupLoop, "ClipPro-UDP-Cleanup").apply { isDaemon = true; start() }
        Log.i(TAG, "Discovery started. Instance: $instanceId")
    }

    private fun listenLoop() {
        val buffer = ByteArray(1024)
        while (isRunning) {
            try {
                val packet = DatagramPacket(buffer, buffer.size)
                socket?.receive(packet)
                val message = String(packet.data, 0, packet.length, Charsets.UTF_8)

                if (message.startsWith("CLIPPRO_DISCOVER:")) {
                    val payload = message.substring("CLIPPRO_DISCOVER:".length)
                    val parts = payload.split("|")
                    if (parts.size < 3) continue

                    val peerName = parts[0].trim()
                    val peerPort = parts[1].trim().toIntOrNull() ?: continue
                    val peerId = parts[2].trim()
                    val peerIp = packet.address?.hostAddress ?: continue

                    if (peerId != instanceId && peerName.isNotBlank()) {
                        val key = "$peerIp:$peerPort"
                        val existing = discoveredPeers[key]
                        discoveredPeers[key] = PeerDevice(
                            name = peerName,
                            ip = peerIp,
                            port = peerPort,
                            lastSeen = System.currentTimeMillis()
                        )
                        if (existing == null) {
                            onPeersUpdated(discoveredPeers.values.toList())
                        }
                    }
                }
            } catch (e: SocketTimeoutException) {
                // Normal timeout, loop continues
            } catch (e: Exception) {
                if (!isRunning) break
                Log.w(TAG, "Listen error: ${e.message}")
            }
        }
    }

    private fun broadcastLoop() {
        val broadcastPayload = "CLIPPRO_DISCOVER:$deviceName|$tcpPort|$instanceId"
        val messageBytes = broadcastPayload.toByteArray(Charsets.UTF_8)

        while (isRunning) {
            try {
                // Try subnet-specific broadcast addresses first (better than 255.255.255.255)
                val broadcasts = getSubnetBroadcasts()
                for (bcast in broadcasts) {
                    try {
                        val packet = DatagramPacket(messageBytes, messageBytes.size, bcast, 50505)
                        socket?.send(packet)
                    } catch (e: Exception) { /* ignore per-interface failures */ }
                }
                // Also send global broadcast as fallback
                try {
                    val fallback = InetAddress.getByName("255.255.255.255")
                    socket?.send(DatagramPacket(messageBytes, messageBytes.size, fallback, 50505))
                } catch (e: Exception) { /* ignore */ }
            } catch (e: Exception) {
                if (!isRunning) break
            }
            Thread.sleep(3000)
        }
    }

    private fun cleanupLoop() {
        while (isRunning) {
            Thread.sleep(5000)
            val now = System.currentTimeMillis()
            val removed = discoveredPeers.entries.removeIf { now - it.value.lastSeen > 15000 }
            if (removed) {
                onPeersUpdated(discoveredPeers.values.toList())
            }
        }
    }

    private fun getSubnetBroadcasts(): List<InetAddress> {
        val result = mutableListOf<InetAddress>()
        try {
            val interfaces = NetworkInterface.getNetworkInterfaces() ?: return result
            for (ni in interfaces.asSequence()) {
                if (!ni.isUp || ni.isLoopback || ni.isVirtual) continue
                for (addr in ni.interfaceAddresses) {
                    val ip = addr.address
                    if (!ip.isSiteLocalAddress) continue
                    val broadcast = addr.broadcast ?: continue
                    result.add(broadcast)
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Could not enumerate network interfaces: ${e.message}")
        }
        return result
    }

    fun stop() {
        isRunning = false
        try { socket?.close() } catch (e: Exception) { }
        socket = null
        discoveredPeers.clear()
        Log.i(TAG, "Discovery stopped.")
    }

    fun getPeers(): List<PeerDevice> = discoveredPeers.values.toList()
}
