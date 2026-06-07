package com.clipboardpro.share.ui

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
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
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.clipboardpro.share.model.PeerDevice
import com.clipboardpro.share.model.TransferDirection
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
    val receivedTexts by (service?.receivedTexts ?: emptyFlow<List<Pair<String,String>>>()).collectAsState(initial = emptyList())

    var selectedPeer by remember { mutableStateOf<PeerDevice?>(null) }
    var selectedTab by remember { mutableIntStateOf(0) }
    var showSettings by remember { mutableStateOf(false) }
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

    AnimatedContent(
        targetState = showSettings,
        transitionSpec = {
            slideInHorizontally { it } + fadeIn() togetherWith slideOutHorizontally { -it } + fadeOut()
        },
        label = "nav"
    ) { inSettings ->
        if (inSettings) {
            SettingsScreen(onBack = { showSettings = false })
        } else {
            Scaffold(
                containerColor = DarkBg,
                topBar = {
                    TopBar(
                        peers = peers,
                        isServiceBound = isServiceBound,
                        onSettingsClick = { showSettings = true }
                    )
                },
                bottomBar = {
                    AppBottomBar(
                        selectedTab = selectedTab,
                        transferCount = transfers.count { it.status == TransferStatus.ACTIVE },
                        onTabSelected = { selectedTab = it }
                    )
                }
            ) { padding ->
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .background(DarkBg)
                ) {
                    when (selectedTab) {
                        0 -> DevicesTab(
                            peers = peers,
                            selectedPeer = selectedPeer,
                            onPeerSelected = { selectedPeer = if (selectedPeer?.ip == it.ip) null else it },
                            onSendFile = { filePicker.launch("*/*") },
                            onSendClipboard = {
                                val peer = selectedPeer ?: return@DevicesTab
                                val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                val text = cb.primaryClip?.getItemAt(0)?.text?.toString() ?: return@DevicesTab
                                service?.sendText(text, peer)
                            },
                            onSendText = { text ->
                                val peer = selectedPeer ?: return@DevicesTab
                                service?.sendText(text, peer)
                            }
                        )
                        1 -> TransfersTab(transfers = transfers)
                        2 -> ReceivedTextsTab(receivedTexts = receivedTexts)
                    }
                }
            }
        }
    }
}

@Composable
fun TopBar(peers: List<PeerDevice>, isServiceBound: Boolean, onSettingsClick: () -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                Brush.verticalGradient(colors = listOf(CardBg, DarkBg))
            )
            .padding(horizontal = 20.dp, vertical = 14.dp)
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(36.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .background(Teal400.copy(0.15f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Rounded.Share, null, tint = Teal400, modifier = Modifier.size(20.dp))
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    "Local Share",
                    color = TextPrimary, fontWeight = FontWeight.Bold, fontSize = 18.sp
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    if (!isServiceBound) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(8.dp),
                            color = WarningAmber, strokeWidth = 1.5.dp
                        )
                        Spacer(Modifier.width(5.dp))
                        Text("Starting...", color = WarningAmber, fontSize = 11.sp)
                    } else if (peers.isEmpty()) {
                        Box(
                            Modifier.size(7.dp).clip(CircleShape)
                                .background(TextMuted.copy(0.5f))
                        )
                        Spacer(Modifier.width(5.dp))
                        Text("Scanning for devices...", color = TextMuted, fontSize = 11.sp)
                    } else {
                        Box(
                            Modifier.size(7.dp).clip(CircleShape)
                                .background(SuccessGreen)
                        )
                        Spacer(Modifier.width(5.dp))
                        Text("${peers.size} device(s) nearby", color = SuccessGreen, fontSize = 11.sp)
                    }
                }
            }
            IconButton(onClick = onSettingsClick) {
                Icon(Icons.Rounded.Settings, null, tint = TextMuted)
            }
        }
    }
}

@Composable
fun AppBottomBar(selectedTab: Int, transferCount: Int, onTabSelected: (Int) -> Unit) {
    NavigationBar(
        containerColor = CardBg,
        tonalElevation = 0.dp
    ) {
        NavigationBarItem(
            selected = selectedTab == 0,
            onClick = { onTabSelected(0) },
            icon = { Icon(Icons.Rounded.Devices, null) },
            label = { Text("Devices", fontSize = 11.sp) },
            colors = navColors()
        )
        NavigationBarItem(
            selected = selectedTab == 1,
            onClick = { onTabSelected(1) },
            icon = {
                BadgedBox(badge = {
                    if (transferCount > 0)
                        Badge(containerColor = Teal400) {
                            Text("$transferCount", fontSize = 9.sp, color = DarkBg)
                        }
                }) { Icon(Icons.Rounded.SwapVert, null) }
            },
            label = { Text("Transfers", fontSize = 11.sp) },
            colors = navColors()
        )
        NavigationBarItem(
            selected = selectedTab == 2,
            onClick = { onTabSelected(2) },
            icon = { Icon(Icons.Rounded.TextFields, null) },
            label = { Text("Received", fontSize = 11.sp) },
            colors = navColors()
        )
    }
}

@Composable
fun navColors() = NavigationBarItemDefaults.colors(
    selectedIconColor = Teal400,
    selectedTextColor = Teal400,
    unselectedIconColor = TextMuted,
    unselectedTextColor = TextMuted,
    indicatorColor = Teal400.copy(alpha = 0.15f)
)

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
        modifier = Modifier.fillMaxSize().padding(16.dp)
    ) {
        if (peers.isEmpty()) {
            ScanningAnimation()
        } else {
            Text(
                "NEARBY DEVICES",
                color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp,
                modifier = Modifier.padding(bottom = 12.dp, start = 4.dp)
            )
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(10.dp),
                modifier = Modifier.weight(1f)
            ) {
                items(peers, key = { it.ip }) { peer ->
                    PeerCard(
                        peer = peer,
                        isSelected = selectedPeer?.ip == peer.ip,
                        onClick = { onPeerSelected(peer) }
                    )
                }
            }
            AnimatedVisibility(
                visible = selectedPeer != null,
                enter = slideInVertically { it } + fadeIn(),
                exit = slideOutVertically { it } + fadeOut()
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
        if (isSelected) Teal400 else BorderColor, label = "border"
    )
    val bgColor by animateColorAsState(
        if (isSelected) Teal400.copy(alpha = 0.08f) else CardBg, label = "bg"
    )
    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth().border(1.5.dp, borderColor, RoundedCornerShape(14.dp)),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = bgColor),
        elevation = CardDefaults.cardElevation(if (isSelected) 4.dp else 0.dp)
    ) {
        Row(
            modifier = Modifier.padding(14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier.size(46.dp).clip(CircleShape)
                    .background(if (isSelected) Teal400.copy(0.2f) else ElevatedBg),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    Icons.Rounded.Computer, null,
                    tint = if (isSelected) Teal400 else TextMuted,
                    modifier = Modifier.size(26.dp)
                )
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    peer.name, color = TextPrimary,
                    fontWeight = FontWeight.SemiBold, fontSize = 15.sp,
                    maxLines = 1, overflow = TextOverflow.Ellipsis
                )
                Text(peer.ip, color = TextMuted, fontSize = 11.sp)
            }
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Box(
                    Modifier.size(8.dp).clip(CircleShape).background(SuccessGreen)
                )
                Spacer(Modifier.height(3.dp))
                Text("Online", color = SuccessGreen, fontSize = 9.sp)
            }
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
        modifier = Modifier.fillMaxWidth().padding(top = 14.dp),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceBg),
        border = BorderStroke(1.dp, Teal400.copy(alpha = 0.25f))
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(6.dp).clip(CircleShape).background(SuccessGreen)
                )
                Spacer(Modifier.width(7.dp))
                Text(
                    "SEND TO ${peer?.name?.uppercase() ?: ""}",
                    color = Teal400, fontSize = 10.sp,
                    fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp
                )
            }

            Spacer(Modifier.height(12.dp))

            // Text input row
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedTextField(
                    value = textInput,
                    onValueChange = { textInput = it },
                    placeholder = { Text("Type a message...", color = TextMuted, fontSize = 13.sp) },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Send),
                    keyboardActions = KeyboardActions(onSend = {
                        if (textInput.isNotBlank()) { onSendText(textInput); textInput = "" }
                    }),
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400,
                        unfocusedBorderColor = BorderColor,
                        cursorColor = Teal400
                    ),
                    shape = RoundedCornerShape(12.dp)
                )
                Button(
                    onClick = {
                        if (textInput.isNotBlank()) { onSendText(textInput); textInput = "" }
                    },
                    enabled = textInput.isNotBlank(),
                    shape = RoundedCornerShape(12.dp),
                    contentPadding = PaddingValues(horizontal = 14.dp, vertical = 14.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Teal400,
                        disabledContainerColor = ElevatedBg
                    )
                ) {
                    Icon(
                        Icons.Rounded.Send, null,
                        modifier = Modifier.size(18.dp),
                        tint = if (textInput.isNotBlank()) DarkBg else TextMuted
                    )
                }
            }

            Spacer(Modifier.height(12.dp))
            HorizontalDivider(color = BorderColor, thickness = 1.dp)
            Spacer(Modifier.height(12.dp))

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(
                    onClick = onSendFile,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                ) {
                    Icon(Icons.Rounded.UploadFile, null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Send File", fontWeight = FontWeight.SemiBold, color = DarkBg, fontSize = 13.sp)
                }
                OutlinedButton(
                    onClick = onSendClipboard,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    border = BorderStroke(1.dp, Teal400)
                ) {
                    Icon(Icons.Rounded.ContentPaste, null, modifier = Modifier.size(18.dp), tint = Teal400)
                    Spacer(Modifier.width(6.dp))
                    Text("Clipboard", fontWeight = FontWeight.SemiBold, color = Teal400, fontSize = 13.sp)
                }
            }
        }
    }
}

@Composable
fun ReceivedTextsTab(receivedTexts: List<Pair<String, String>>) {
    val context = LocalContext.current
    if (receivedTexts.isEmpty()) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Rounded.TextFields, null, tint = TextMuted, modifier = Modifier.size(48.dp))
                Spacer(Modifier.height(12.dp))
                Text("No texts received yet", color = TextMuted, fontSize = 14.sp)
                Text("Texts sent from desktop appear here", color = TextMuted.copy(0.6f), fontSize = 12.sp,
                    modifier = Modifier.padding(top = 4.dp))
            }
        }
        return
    }
    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        item {
            Text(
                "RECEIVED TEXTS", color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp,
                modifier = Modifier.padding(bottom = 4.dp, start = 4.dp)
            )
        }
        items(receivedTexts.reversed()) { (text, from) ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = CardBg),
                border = BorderStroke(1.dp, BorderColor)
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Rounded.Computer, null, tint = Teal400, modifier = Modifier.size(16.dp))
                        Spacer(Modifier.width(6.dp))
                        Text(from, color = Teal400, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                        Spacer(Modifier.weight(1f))
                        IconButton(
                            onClick = {
                                val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                cb.setPrimaryClip(ClipData.newPlainText("text", text))
                            },
                            modifier = Modifier.size(28.dp)
                        ) {
                            Icon(Icons.Rounded.ContentCopy, null, tint = TextMuted, modifier = Modifier.size(16.dp))
                        }
                    }
                    Spacer(Modifier.height(8.dp))
                    Text(text, color = TextPrimary, fontSize = 14.sp)
                }
            }
        }
    }
}

@Composable
fun ScanningAnimation() {
    val infiniteTransition = rememberInfiniteTransition(label = "scan")
    val scale1 by infiniteTransition.animateFloat(
        1f, 2.4f, label = "s1",
        animationSpec = infiniteRepeatable(tween(2400, easing = LinearEasing))
    )
    val alpha1 by infiniteTransition.animateFloat(
        0.4f, 0f, label = "a1",
        animationSpec = infiniteRepeatable(tween(2400, easing = LinearEasing))
    )
    val scale2 by infiniteTransition.animateFloat(
        1f, 2.4f, label = "s2",
        animationSpec = infiniteRepeatable(tween(2400, delayMillis = 800, easing = LinearEasing))
    )
    val alpha2 by infiniteTransition.animateFloat(
        0.4f, 0f, label = "a2",
        animationSpec = infiniteRepeatable(tween(2400, delayMillis = 800, easing = LinearEasing))
    )

    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(contentAlignment = Alignment.Center) {
                Box(
                    Modifier.size(100.dp).scale(scale1).clip(CircleShape)
                        .background(Teal400.copy(alpha = alpha1))
                )
                Box(
                    Modifier.size(100.dp).scale(scale2).clip(CircleShape)
                        .background(Teal400.copy(alpha = alpha2))
                )
                Box(
                    Modifier.size(100.dp).clip(CircleShape)
                        .background(Brush.radialGradient(listOf(Teal400.copy(0.15f), CardBg)))
                        .border(1.5.dp, Teal400.copy(0.5f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(Icons.Rounded.Wifi, null, tint = Teal400, modifier = Modifier.size(42.dp))
                }
            }
            Spacer(Modifier.height(28.dp))
            Text("Searching for devices...", color = TextPrimary, fontSize = 15.sp, fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(6.dp))
            Text(
                "Make sure ClipboardPro is running\non your Windows PC",
                color = TextMuted, fontSize = 12.sp,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(horizontal = 40.dp)
            )
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
            Text(
                "TRANSFER HISTORY", color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp,
                modifier = Modifier.padding(bottom = 4.dp, start = 4.dp)
            )
        }
        items(transfers, key = { it.id }) { t -> TransferCard(t) }
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
    val dirIcon = if (transfer.direction == TransferDirection.SEND)
        Icons.Rounded.Upload else Icons.Rounded.Download

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg),
        border = BorderStroke(1.dp, BorderColor)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    Modifier.size(38.dp).clip(RoundedCornerShape(10.dp))
                        .background(statusColor.copy(0.12f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(dirIcon, null, tint = statusColor, modifier = Modifier.size(20.dp))
                }
                Spacer(Modifier.width(12.dp))
                Column(Modifier.weight(1f)) {
                    Text(
                        transfer.fileName, color = TextPrimary,
                        fontWeight = FontWeight.SemiBold, fontSize = 13.sp,
                        maxLines = 1, overflow = TextOverflow.Ellipsis
                    )
                    Text(
                        "${if (transfer.direction == TransferDirection.SEND) "→" else "←"} ${transfer.peerName}",
                        color = TextMuted, fontSize = 11.sp
                    )
                }
                Text(
                    transfer.status.name.lowercase().replaceFirstChar { it.uppercase() },
                    color = statusColor, fontSize = 11.sp, fontWeight = FontWeight.Bold
                )
            }
            if (transfer.status == TransferStatus.ACTIVE && transfer.totalBytes > 0) {
                Spacer(Modifier.height(8.dp))
                LinearProgressIndicator(
                    progress = { transfer.progress / 100f },
                    modifier = Modifier.fillMaxWidth().height(3.dp).clip(RoundedCornerShape(2.dp)),
                    color = Teal400, trackColor = Teal400.copy(0.1f)
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
