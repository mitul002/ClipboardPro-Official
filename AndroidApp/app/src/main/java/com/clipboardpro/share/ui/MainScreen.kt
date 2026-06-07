package com.clipboardpro.share.ui

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.rounded.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.clipboardpro.share.model.PeerDevice
import com.clipboardpro.share.model.TransferItem
import com.clipboardpro.share.model.TransferStatus
import com.clipboardpro.share.service.LocalShareService
import com.clipboardpro.share.ui.theme.*
import kotlinx.coroutines.flow.emptyFlow
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    serviceProvider: () -> LocalShareService?,
    isServiceBound: Boolean
) {
    val service = serviceProvider()
    val peers by (service?.peers ?: emptyFlow<List<PeerDevice>>()).collectAsState(initial = emptyList())
    val transfers by (service?.transfers ?: emptyFlow<List<TransferItem>>()).collectAsState(initial = emptyList())

    var selectedPeer by remember { mutableStateOf<PeerDevice?>(null) }
    var selectedTab by remember { mutableIntStateOf(0) }
    val context = LocalContext.current

    val filePicker = rememberLauncherForActivityResult(
        ActivityResultContracts.GetMultipleContents()
    ) { uris ->
        val peer = selectedPeer ?: return@rememberLauncherForActivityResult
        uris.forEach { uri ->
            val file = uriToFile(context, uri) ?: return@forEach
            service?.sendFile(file, peer)
        }
    }

    Scaffold(
        containerColor = DarkBg,
        bottomBar = {
            NavigationBar(
                containerColor = CardBg,
                contentColor = Teal400
            ) {
                NavigationBarItem(
                    selected = selectedTab == 0,
                    onClick = { selectedTab = 0 },
                    icon = { Icon(Icons.Rounded.Devices, null) },
                    label = { Text("Devices", fontSize = 11.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = Teal400,
                        selectedTextColor = Teal400,
                        unselectedIconColor = TextMuted,
                        unselectedTextColor = TextMuted,
                        indicatorColor = Teal400.copy(alpha = 0.15f)
                    )
                )
                NavigationBarItem(
                    selected = selectedTab == 1,
                    onClick = { selectedTab = 1 },
                    icon = { Icon(Icons.Rounded.SwapVert, null) },
                    label = { Text("Transfers", fontSize = 11.sp) },
                    colors = NavigationBarItemDefaults.colors(
                        selectedIconColor = Teal400,
                        selectedTextColor = Teal400,
                        unselectedIconColor = TextMuted,
                        unselectedTextColor = TextMuted,
                        indicatorColor = Teal400.copy(alpha = 0.15f)
                    )
                )
            }
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(DarkBg)
        ) {
            // Header
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(
                        Brush.verticalGradient(
                            colors = listOf(CardBg, DarkBg)
                        )
                    )
                    .padding(horizontal = 20.dp, vertical = 16.dp)
            ) {
                Column {
                    Text(
                        text = "ClipboardPro",
                        color = Teal400,
                        fontWeight = FontWeight.Bold,
                        fontSize = 22.sp,
                        letterSpacing = 0.5.sp
                    )
                    Text(
                        text = if (!isServiceBound) "Starting..."
                        else if (peers.isEmpty()) "Scanning for nearby devices..."
                        else "${peers.size} device(s) found on network",
                        color = TextMuted,
                        fontSize = 12.sp
                    )
                }
                if (!isServiceBound) {
                    CircularProgressIndicator(
                        modifier = Modifier
                            .size(20.dp)
                            .align(Alignment.CenterEnd),
                        color = Teal400,
                        strokeWidth = 2.dp
                    )
                }
            }

            when (selectedTab) {
                0 -> DevicesTab(
                    peers = peers,
                    selectedPeer = selectedPeer,
                    onPeerSelected = { selectedPeer = it },
                    onSendFile = { filePicker.launch("*/*") },
                    onSendClipboard = {
                        val peer = selectedPeer ?: return@DevicesTab
                        val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                        val text = clipboard.primaryClip?.getItemAt(0)?.text?.toString() ?: return@DevicesTab
                        service?.sendText(text, peer)
                    },
                    onSendText = { text ->
                        val peer = selectedPeer ?: return@DevicesTab
                        service?.sendText(text, peer)
                    }
                )
                1 -> TransfersTab(transfers = transfers)
            }
        }
    }
}

@Composable
fun DevicesTab(
    peers: List<PeerDevice>,
    selectedPeer: PeerDevice?,
    onPeerSelected: (PeerDevice) -> Unit,
    onSendFile: () -> Unit,
    onSendClipboard: () -> Unit,
    onSendText: (String) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        if (peers.isEmpty()) {
            ScanningAnimation()
        } else {
            Text(
                text = "NEARBY DEVICES",
                color = TextMuted,
                fontSize = 10.sp,
                fontWeight = FontWeight.Bold,
                letterSpacing = 1.5.sp,
                modifier = Modifier.padding(bottom = 12.dp, start = 4.dp)
            )

            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.weight(1f)
            ) {
                items(peers) { peer ->
                    PeerCard(
                        peer = peer,
                        isSelected = selectedPeer?.ip == peer.ip,
                        onClick = { onPeerSelected(peer) }
                    )
                }
            }

            AnimatedVisibility(
                visible = selectedPeer != null,
                enter = slideInVertically(initialOffsetY = { it }) + fadeIn(),
                exit = slideOutVertically(targetOffsetY = { it }) + fadeOut()
            ) {
                SendPanel(
                    peer = selectedPeer,
                    onSendFile = onSendFile,
                    onSendClipboard = onSendClipboard,
                    onSendText = onSendText
                )
            }
        }
    }
}

@Composable
fun PeerCard(peer: PeerDevice, isSelected: Boolean, onClick: () -> Unit) {
    val borderColor by animateColorAsState(
        if (isSelected) Teal400 else Color.Transparent,
        label = "border"
    )
    Card(
        onClick = onClick,
        modifier = Modifier
            .fillMaxWidth()
            .border(1.5.dp, borderColor, RoundedCornerShape(14.dp)),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected) Teal400.copy(alpha = 0.08f) else CardBg
        )
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Device icon
            Box(
                modifier = Modifier
                    .size(46.dp)
                    .clip(CircleShape)
                    .background(Teal400.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Rounded.Computer,
                    contentDescription = null,
                    tint = Teal400,
                    modifier = Modifier.size(26.dp)
                )
            }
            Spacer(modifier = Modifier.width(14.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = peer.name,
                    color = TextPrimary,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 15.sp,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = peer.ip,
                    color = TextMuted,
                    fontSize = 11.sp
                )
            }
            // Online indicator
            Box(
                modifier = Modifier
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(SuccessGreen)
            )
        }
    }
}

@Composable
fun SendPanel(
    peer: PeerDevice?,
    onSendFile: () -> Unit,
    onSendClipboard: () -> Unit,
    onSendText: (String) -> Unit
) {
    var textInput by remember { mutableStateOf("") }
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 16.dp),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceBg),
        border = BorderStroke(1.dp, Teal400.copy(alpha = 0.2f))
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = "SEND TO ${peer?.name?.uppercase() ?: ""}",
                color = Teal400,
                fontSize = 10.sp,
                fontWeight = FontWeight.Bold,
                letterSpacing = 1.5.sp
            )
            Spacer(modifier = Modifier.height(10.dp))
            
            OutlinedTextField(
                value = textInput,
                onValueChange = { textInput = it },
                placeholder = { Text("Type text to send...", color = TextMuted, fontSize = 13.sp) },
                modifier = Modifier.fillMaxWidth().height(56.dp),
                maxLines = 2,
                colors = OutlinedTextFieldDefaults.colors(
                    focusedTextColor = TextPrimary,
                    unfocusedTextColor = TextPrimary,
                    focusedBorderColor = Teal400,
                    unfocusedBorderColor = TextMuted.copy(0.3f),
                    cursorColor = Teal400
                ),
                shape = RoundedCornerShape(10.dp),
                trailingIcon = {
                    IconButton(
                        onClick = {
                            if (textInput.isNotBlank()) {
                                onSendText(textInput)
                                textInput = ""
                            }
                        },
                        enabled = textInput.isNotBlank()
                    ) {
                        Icon(
                            imageVector = Icons.Default.Send,
                            contentDescription = "Send",
                            tint = if (textInput.isNotBlank()) Teal400 else TextMuted.copy(0.4f)
                        )
                    }
                }
            )
            
            Spacer(modifier = Modifier.height(12.dp))
            
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(
                    onClick = onSendFile,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                ) {
                    Icon(Icons.Rounded.UploadFile, null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Send File", fontWeight = FontWeight.SemiBold, color = DarkBg)
                }
                OutlinedButton(
                    onClick = onSendClipboard,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    border = BorderStroke(1.dp, Teal400)
                ) {
                    Icon(Icons.Rounded.ContentPaste, null, modifier = Modifier.size(18.dp), tint = Teal400)
                    Spacer(Modifier.width(6.dp))
                    Text("Clipboard", fontWeight = FontWeight.SemiBold, color = Teal400)
                }
            }
        }
    }
}

@Composable
fun ScanningAnimation() {
    val infiniteTransition = rememberInfiniteTransition(label = "scan")
    val scale1 by infiniteTransition.animateFloat(
        initialValue = 1f, targetValue = 2.4f, label = "s1",
        animationSpec = infiniteRepeatable(tween(2400, easing = LinearEasing))
    )
    val alpha1 by infiniteTransition.animateFloat(
        initialValue = 0.45f, targetValue = 0f, label = "a1",
        animationSpec = infiniteRepeatable(tween(2400, easing = LinearEasing))
    )
    val scale2 by infiniteTransition.animateFloat(
        initialValue = 1f, targetValue = 2.4f, label = "s2",
        animationSpec = infiniteRepeatable(tween(2400, delayMillis = 800, easing = LinearEasing))
    )
    val alpha2 by infiniteTransition.animateFloat(
        initialValue = 0.45f, targetValue = 0f, label = "a2",
        animationSpec = infiniteRepeatable(tween(2400, delayMillis = 800, easing = LinearEasing))
    )

    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(contentAlignment = Alignment.Center) {
                Box(
                    modifier = Modifier
                        .size(100.dp)
                        .scale(scale1)
                        .clip(CircleShape)
                        .background(Teal400.copy(alpha = alpha1))
                )
                Box(
                    modifier = Modifier
                        .size(100.dp)
                        .scale(scale2)
                        .clip(CircleShape)
                        .background(Teal400.copy(alpha = alpha2))
                )
                Box(
                    modifier = Modifier
                        .size(100.dp)
                        .clip(CircleShape)
                        .background(
                            Brush.radialGradient(
                                colors = listOf(Teal400.copy(0.2f), CardBg)
                            )
                        )
                        .border(2.dp, Teal400.copy(0.6f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        Icons.Rounded.Wifi,
                        contentDescription = null,
                        tint = Teal400,
                        modifier = Modifier.size(42.dp)
                    )
                }
            }
            Spacer(Modifier.height(24.dp))
            Text("Searching for nearby devices...", color = TextMuted, fontSize = 14.sp)
            Text("Make sure ClipboardPro is running on your PC", color = TextMuted.copy(0.6f), fontSize = 11.sp, textAlign = TextAlign.Center, modifier = Modifier.padding(top = 4.dp, start = 32.dp, end = 32.dp))
        }
    }
}

@Composable
fun TransfersTab(transfers: List<TransferItem>) {
    if (transfers.isEmpty()) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Rounded.SwapVert, null, tint = TextMuted, modifier = Modifier.size(48.dp))
                Spacer(Modifier.height(12.dp))
                Text("No transfers yet", color = TextMuted, fontSize = 14.sp)
            }
        }
        return
    }
    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        item {
            Text("TRANSFER HISTORY", color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp,
                modifier = Modifier.padding(bottom = 4.dp, start = 4.dp))
        }
        items(transfers) { transfer ->
            TransferCard(transfer)
        }
    }
}

@Composable
fun TransferCard(transfer: TransferItem) {
    val statusColor = when (transfer.status) {
        TransferStatus.COMPLETED -> SuccessGreen
        TransferStatus.FAILED, TransferStatus.CANCELLED -> DangerRed
        TransferStatus.ACTIVE -> Teal400
        else -> TextMuted
    }
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    if (transfer.status == TransferStatus.COMPLETED) Icons.Rounded.CheckCircle else Icons.Rounded.SwapVert,
                    null, tint = statusColor, modifier = Modifier.size(20.dp)
                )
                Spacer(Modifier.width(10.dp))
                Column(Modifier.weight(1f)) {
                    Text(transfer.fileName, color = TextPrimary, fontWeight = FontWeight.SemiBold,
                        fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text("${if (transfer.direction.name == "SEND") "→" else "←"} ${transfer.peerName}",
                        color = TextMuted, fontSize = 11.sp)
                }
                Text(transfer.status.name, color = statusColor, fontSize = 11.sp, fontWeight = FontWeight.Bold)
            }
            if (transfer.status == TransferStatus.ACTIVE && transfer.totalBytes > 0) {
                Spacer(Modifier.height(8.dp))
                LinearProgressIndicator(
                    progress = { transfer.progress / 100f },
                    modifier = Modifier.fillMaxWidth().height(3.dp).clip(RoundedCornerShape(2.dp)),
                    color = Teal400,
                    trackColor = Teal400.copy(0.1f)
                )
                Spacer(Modifier.height(4.dp))
                Text(transfer.sizeDisplay, color = TextMuted, fontSize = 10.sp)
            }
        }
    }
}

private fun uriToFile(context: Context, uri: android.net.Uri): File? {
    return try {
        val fileName = getFileName(context, uri) ?: "file_${System.currentTimeMillis()}"
        val inputStream = context.contentResolver.openInputStream(uri) ?: return null
        val tempFile = File(context.cacheDir, fileName)
        tempFile.outputStream().use { inputStream.copyTo(it) }
        tempFile
    } catch (e: Exception) { null }
}

private fun getFileName(context: Context, uri: android.net.Uri): String? {
    var name: String? = null
    context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
        val idx = cursor.getColumnIndex(android.provider.OpenableColumns.DISPLAY_NAME)
        if (cursor.moveToFirst() && idx >= 0) name = cursor.getString(idx)
    }
    return name ?: uri.lastPathSegment
}
