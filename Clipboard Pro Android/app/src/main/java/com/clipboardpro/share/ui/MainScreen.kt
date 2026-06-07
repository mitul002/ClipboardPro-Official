package com.clipboardpro.share.ui

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.*
import androidx.compose.animation.core.*
import androidx.compose.foundation.*
import androidx.compose.foundation.gestures.detectTapGestures
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
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.FileProvider
import com.clipboardpro.share.R
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.data.SnippetItemEntity
import com.clipboardpro.share.model.ClipboardItemType
import com.clipboardpro.share.model.PeerDevice
import com.clipboardpro.share.model.TransferDirection
import com.clipboardpro.share.model.TransferItem
import com.clipboardpro.share.model.TransferStatus
import com.clipboardpro.share.service.LocalShareService
import com.clipboardpro.share.ui.theme.*
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.launch
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    serviceProvider: () -> LocalShareService?,
    isServiceBound: Boolean,
    themeMode: String,
    onThemeModeChanged: (String) -> Unit
) {
    val service = serviceProvider()
    val peers by (service?.peers ?: emptyFlow<List<PeerDevice>>()).collectAsState(initial = emptyList())
    val transfers by (service?.transfers ?: emptyFlow<List<TransferItem>>()).collectAsState(initial = emptyList())

    val context = LocalContext.current
    val database = remember { AppDatabase.getDatabase(context) }
    val scope = rememberCoroutineScope()

    // Room DB flow states
    val dbClips by database.clipboardDao().getAllItemsFlow().collectAsState(initial = emptyList())
    val dbSnippets by database.snippetDao().getAllSnippetsFlow().collectAsState(initial = emptyList())

    var selectedTab by remember { mutableIntStateOf(0) } // 0: Vault, 1: Snippets, 2: Devices, 3: Transfers
    var showSettings by remember { mutableStateOf(false) }
    var searchQuery by remember { mutableStateOf("") }
    
    // Bottom Sheet for direct P2P sharing
    var showBottomSheet by remember { mutableStateOf(false) }
    var itemToSend by remember { mutableStateOf<String?>(null) }
    val sheetState = rememberModalBottomSheetState()

    // Dialog for adding snippet
    var showAddSnippetDialog by remember { mutableStateOf(false) }

    // Edit clip dialog
    var editingClip by remember { mutableStateOf<ClipboardItemEntity?>(null) }

    // Edit snippet dialog
    var editingSnippet by remember { mutableStateOf<SnippetItemEntity?>(null) }

    AnimatedContent(
        targetState = showSettings,
        transitionSpec = {
            slideInHorizontally { it } + fadeIn() togetherWith slideOutHorizontally { -it } + fadeOut()
        },
        label = "settings_nav"
    ) { inSettings ->
        if (inSettings) {
            SettingsScreen(
                themeMode = themeMode,
                onThemeModeChanged = onThemeModeChanged,
                onBack = { showSettings = false }
            )
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
                },
                floatingActionButton = {
                    if (selectedTab == 1) {
                        FloatingActionButton(
                            onClick = { showAddSnippetDialog = true },
                            containerColor = Teal400,
                            contentColor = Color.White,
                            shape = CircleShape
                        ) {
                            Icon(Icons.Rounded.Add, "Add Snippet")
                        }
                    }
                }
            ) { padding ->
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .background(DarkBg)
                ) {
                    when (selectedTab) {
                        0 -> VaultTab(
                            clips = dbClips,
                            searchQuery = searchQuery,
                            onQueryChanged = { searchQuery = it },
                            onCopyText = { text ->
                                val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                cb.setPrimaryClip(ClipData.newPlainText("text", text))
                                Toast.makeText(context, "Copied to clipboard", Toast.LENGTH_SHORT).show()
                            },
                            onDeleteText = { id -> service?.removeClipboardItem(id) },
                            onClearAll = { service?.clearClipboardHistory() },
                            onSendClick = { text ->
                                itemToSend = text
                                showBottomSheet = true
                            },
                            onPinToggle = { item ->
                                scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                    database.clipboardDao().insertItem(item.copy(isPinned = !item.isPinned))
                                }
                            },
                            onFavToggle = { item ->
                                scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                    database.clipboardDao().insertItem(item.copy(isFavorite = !item.isFavorite))
                                }
                            },
                            onMaskToggle = { item ->
                                scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                    database.clipboardDao().insertItem(item.copy(isMasked = !item.isMasked))
                                }
                            },
                            onEditClip = { item -> editingClip = item }
                        )
                        1 -> SnippetsTab(
                            snippets = dbSnippets,
                            searchQuery = searchQuery,
                            onQueryChanged = { searchQuery = it },
                            onDeleteSnippet = { snippet ->
                                scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                    database.snippetDao().deleteSnippet(snippet)
                                }
                            },
                            onEditSnippet = { snippet -> editingSnippet = snippet }
                        )
                        2 -> DevicesTab(
                            peers = peers,
                            onSendFileSelected = { file, peer ->
                                service?.sendFile(file, peer)
                            },
                            onSendText = { text, peer ->
                                service?.sendText(text, peer)
                            },
                            onSendClipboard = { peer ->
                                try {
                                    val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                    if (cb.hasPrimaryClip()) {
                                        val text = cb.primaryClip?.getItemAt(0)?.text?.toString()
                                        if (!text.isNullOrBlank()) {
                                            service?.sendText(text, peer)
                                            Toast.makeText(context, "Sending clipboard to ${peer.name}...", Toast.LENGTH_SHORT).show()
                                        } else {
                                            Toast.makeText(context, "Clipboard is empty", Toast.LENGTH_SHORT).show()
                                        }
                                    } else {
                                        Toast.makeText(context, "Clipboard is empty", Toast.LENGTH_SHORT).show()
                                    }
                                } catch (e: Exception) {
                                    Toast.makeText(context, "Cannot access clipboard", Toast.LENGTH_SHORT).show()
                                }
                            }
                        )
                        3 -> TransfersTab(
                            transfers = transfers,
                            onDeleteTransfer = { service?.removeTransfer(it) },
                            onClearAllTransfers = { service?.clearTransfers() }
                        )
                    }
                }

                // Send Device Selector Bottom Sheet
                if (showBottomSheet) {
                    ModalBottomSheet(
                        onDismissRequest = { showBottomSheet = false },
                        sheetState = sheetState,
                        containerColor = CardBg
                    ) {
                        DeviceSelectorContent(
                            peers = peers,
                            onDeviceSelected = { peer ->
                                itemToSend?.let { text ->
                                    service?.sendText(text, peer)
                                    Toast.makeText(context, "Sending to ${peer.name}...", Toast.LENGTH_SHORT).show()
                                }
                                showBottomSheet = false
                                itemToSend = null
                            }
                        )
                    }
                }

                // Add Snippet Dialog
                if (showAddSnippetDialog) {
                    AddSnippetDialog(
                        onDismiss = { showAddSnippetDialog = false },
                        onSave = { trigger, content, desc ->
                            scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                val entity = SnippetItemEntity(
                                    id = java.util.UUID.randomUUID().toString(),
                                    trigger = trigger,
                                    content = content,
                                    description = desc,
                                    createdAt = System.currentTimeMillis()
                                )
                                database.snippetDao().insertSnippet(entity)
                            }
                            showAddSnippetDialog = false
                        }
                    )
                }

                // Edit Clip Dialog
                editingClip?.let { clip ->
                    EditClipDialog(
                        item = clip,
                        onDismiss = { editingClip = null },
                        onSave = { updatedContent, isPinned, isFavorite, category ->
                            scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                database.clipboardDao().insertItem(
                                    clip.copy(
                                        content = updatedContent,
                                        isPinned = isPinned,
                                        isFavorite = isFavorite,
                                        category = category
                                    )
                                )
                            }
                            editingClip = null
                        }
                    )
                }

                // Edit Snippet Dialog
                editingSnippet?.let { snip ->
                    EditSnippetDialog(
                        snippet = snip,
                        onDismiss = { editingSnippet = null },
                        onSave = { trigger, content, desc ->
                            scope.launch(kotlinx.coroutines.Dispatchers.IO) {
                                database.snippetDao().insertSnippet(
                                    snip.copy(trigger = trigger, content = content, description = desc)
                                )
                            }
                            editingSnippet = null
                        }
                    )
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
            Image(
                painter = painterResource(id = R.drawable.logo),
                contentDescription = "App Logo",
                modifier = Modifier
                    .size(36.dp)
                    .clip(RoundedCornerShape(10.dp))
            )
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(
                    "ClipboardPro",
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
                        Text("Scanning...", color = TextMuted, fontSize = 11.sp)
                    } else {
                        Box(
                            Modifier.size(7.dp).clip(CircleShape)
                                .background(SuccessGreen)
                        )
                        Spacer(Modifier.width(5.dp))
                        Text("${peers.size} device(s) online", color = SuccessGreen, fontSize = 11.sp)
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
            icon = { Icon(Icons.Rounded.ContentPaste, null) },
            label = { Text("Vault", fontSize = 11.sp) },
            colors = navColors()
        )
        NavigationBarItem(
            selected = selectedTab == 1,
            onClick = { onTabSelected(1) },
            icon = { Icon(Icons.Rounded.Bolt, null) },
            label = { Text("Expander", fontSize = 11.sp) },
            colors = navColors()
        )
        NavigationBarItem(
            selected = selectedTab == 2,
            onClick = { onTabSelected(2) },
            icon = { Icon(Icons.Rounded.Devices, null) },
            label = { Text("Devices", fontSize = 11.sp) },
            colors = navColors()
        )
        NavigationBarItem(
            selected = selectedTab == 3,
            onClick = { onTabSelected(3) },
            icon = {
                BadgedBox(badge = {
                    if (transferCount > 0)
                        Badge(containerColor = Teal400) {
                            Text("$transferCount", fontSize = 9.sp, color = Color.White)
                        }
                }) { Icon(Icons.Rounded.SwapVert, null) }
            },
            label = { Text("Transfers", fontSize = 11.sp) },
            colors = navColors()
        )
    }
}

@Composable
fun VaultTab(
    clips: List<ClipboardItemEntity>,
    searchQuery: String,
    onQueryChanged: (String) -> Unit,
    onCopyText: (String) -> Unit,
    onDeleteText: (String) -> Unit,
    onClearAll: () -> Unit,
    onSendClick: (String) -> Unit,
    onPinToggle: (ClipboardItemEntity) -> Unit,
    onFavToggle: (ClipboardItemEntity) -> Unit,
    onMaskToggle: (ClipboardItemEntity) -> Unit,
    onEditClip: (ClipboardItemEntity) -> Unit = {}
) {
    var selectedFilterIndex by remember { mutableIntStateOf(0) }
    val filters = listOf("All", "Pinned", "Favorites", "Texts", "Images", "Colors", "URLs", "Code", "Email", "Phone", "Received")

    val filteredClips = remember(clips, searchQuery, selectedFilterIndex) {
        var base = clips.filter {
            it.content.contains(searchQuery, ignoreCase = true) ||
            it.title?.contains(searchQuery, ignoreCase = true) == true ||
            it.category?.contains(searchQuery, ignoreCase = true) == true
        }
        base = when (selectedFilterIndex) {
            1 -> base.filter { it.isPinned }
            2 -> base.filter { it.isFavorite }
            3 -> base.filter { it.type == ClipboardItemType.TEXT.value }
            4 -> base.filter { it.type == ClipboardItemType.IMAGE.value }
            5 -> base.filter { it.type == ClipboardItemType.COLOR.value }
            6 -> base.filter { it.type == ClipboardItemType.URL.value }
            7 -> base.filter { it.type == ClipboardItemType.CODE.value }
            8 -> base.filter { it.type == ClipboardItemType.EMAIL.value }
            9 -> base.filter { it.type == ClipboardItemType.PHONE.value }
            10 -> base.filter { it.category == "Received" }
            else -> base
        }
        base
    }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        SearchBar(
            query = searchQuery,
            onQueryChange = onQueryChanged,
            placeholder = "Search clipboard..."
        )
        
        Spacer(Modifier.height(10.dp))
        
        // Filter Chips Row
        LazyRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            contentPadding = PaddingValues(vertical = 4.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            items(filters.size) { index ->
                FilterChip(
                    selected = selectedFilterIndex == index,
                    onClick = { selectedFilterIndex = index },
                    label = { Text(filters[index], fontSize = 12.sp) },
                    colors = FilterChipDefaults.filterChipColors(
                        selectedContainerColor = Teal400.copy(0.15f),
                        selectedLabelColor = Teal400,
                        containerColor = CardBg,
                        labelColor = TextMuted
                    ),
                    border = BorderStroke(1.dp, if (selectedFilterIndex == index) Teal400 else BorderColor)
                )
            }
        }

        Spacer(Modifier.height(10.dp))

        Row(
            modifier = Modifier.fillMaxWidth().padding(bottom = 6.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                "VAULT HISTORY", color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp
            )
            if (filteredClips.isNotEmpty()) {
                Text(
                    "Clear All", color = DangerRed, fontSize = 11.sp,
                    fontWeight = FontWeight.SemiBold, modifier = Modifier.clickable { onClearAll() }
                )
            }
        }

        if (filteredClips.isEmpty()) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("No matching items in vault", color = TextMuted, fontSize = 13.sp)
            }
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(filteredClips, key = { it.id }) { item ->
                    VaultCard(
                        item = item,
                        onCopy = { onCopyText(item.content) },
                        onDelete = { onDeleteText(item.id) },
                        onSend = { onSendClick(item.content) },
                        onPin = { onPinToggle(item) },
                        onFav = { onFavToggle(item) },
                        onMaskToggle = { onMaskToggle(item) },
                        onEdit = { onEditClip(item) }
                    )
                }
            }
        }
    }
}

@Composable
fun VaultCard(
    item: ClipboardItemEntity,
    onCopy: () -> Unit,
    onDelete: () -> Unit,
    onSend: () -> Unit,
    onPin: () -> Unit,
    onFav: () -> Unit,
    onMaskToggle: () -> Unit,
    onEdit: () -> Unit = {}
) {
    val context = LocalContext.current
    var showPrettifyDialog by remember { mutableStateOf(false) }

    val typeBadge = when (item.type) {
        ClipboardItemType.IMAGE.value -> "image"
        ClipboardItemType.COLOR.value -> "color"
        ClipboardItemType.URL.value -> "url"
        ClipboardItemType.CODE.value -> if (item.isJson) "json" else "code"
        ClipboardItemType.EMAIL.value -> "email"
        ClipboardItemType.PHONE.value -> "phone"
        ClipboardItemType.PATH.value -> "path"
        ClipboardItemType.DIRECTORY.value -> "folder"
        else -> "text"
    }
    val badgeColor = when (item.type) {
        ClipboardItemType.URL.value -> Blue400
        ClipboardItemType.COLOR.value -> SuccessGreen
        ClipboardItemType.CODE.value -> WarningAmber
        ClipboardItemType.EMAIL.value -> Teal400
        ClipboardItemType.PHONE.value -> Teal400
        else -> Teal400
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg),
        border = BorderStroke(1.dp, if (item.isPinned) Teal400 else BorderColor)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            // Header Row
            Row(verticalAlignment = Alignment.CenterVertically) {
                // Type Badge
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(6.dp))
                        .background(badgeColor.copy(0.12f))
                        .padding(horizontal = 8.dp, vertical = 3.dp)
                ) {
                    Text(typeBadge, color = badgeColor, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                }

                if (item.isJson) {
                    Spacer(Modifier.width(6.dp))
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(6.dp))
                            .background(WarningAmber.copy(0.12f))
                            .padding(horizontal = 8.dp, vertical = 3.dp)
                            .clickable { showPrettifyDialog = true }
                    ) {
                        Text("Prettify", color = WarningAmber, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                    }
                }

                if (item.category != null) {
                    Spacer(Modifier.width(6.dp))
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(6.dp))
                            .background(SuccessGreen.copy(0.12f))
                            .padding(horizontal = 8.dp, vertical = 3.dp)
                    ) {
                        Text(item.category, color = SuccessGreen, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                    }
                }

                Spacer(Modifier.weight(1f))

                // Actions Row
                IconButton(onClick = onEdit, modifier = Modifier.size(28.dp)) {
                    Icon(
                        imageVector = Icons.Rounded.Edit,
                        contentDescription = "Edit",
                        tint = TextMuted,
                        modifier = Modifier.size(16.dp)
                    )
                }
                IconButton(onClick = onPin, modifier = Modifier.size(28.dp)) {
                    Icon(
                        imageVector = Icons.Rounded.PushPin,
                        contentDescription = "Pin",
                        tint = if (item.isPinned) Teal400 else TextMuted,
                        modifier = Modifier.size(16.dp)
                    )
                }
                IconButton(onClick = onFav, modifier = Modifier.size(28.dp)) {
                    Icon(
                        imageVector = if (item.isFavorite) Icons.Rounded.Favorite else Icons.Rounded.FavoriteBorder,
                        contentDescription = "Favorite",
                        tint = if (item.isFavorite) DangerRed else TextMuted,
                        modifier = Modifier.size(16.dp)
                    )
                }
                IconButton(onClick = onDelete, modifier = Modifier.size(28.dp)) {
                    Icon(Icons.Rounded.Delete, "Delete", tint = DangerRed.copy(0.8f), modifier = Modifier.size(16.dp))
                }
            }

            Spacer(Modifier.height(10.dp))

            // Body Display based on Content Types
            when (item.type) {
                ClipboardItemType.COLOR.value -> {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        val parsedColor = remember(item.content) {
                            try {
                                Color(android.graphics.Color.parseColor(item.content))
                            } catch (e: Exception) {
                                Color.Gray
                            }
                        }
                        Box(
                            modifier = Modifier
                                .size(36.dp)
                                .clip(CircleShape)
                                .background(parsedColor)
                                .border(1.dp, Color.White, CircleShape)
                        )
                        Spacer(Modifier.width(10.dp))
                        Text(
                            text = item.content,
                            color = TextPrimary,
                            fontWeight = FontWeight.Bold,
                            fontFamily = FontFamily.Monospace,
                            fontSize = 14.sp
                        )
                    }
                }
                ClipboardItemType.IMAGE.value -> {
                    if (!item.imagePath.isNullOrBlank()) {
                        val file = remember(item.imagePath) { java.io.File(item.imagePath) }
                        if (file.exists()) {
                            val bitmap = remember(item.imagePath) {
                                try {
                                    val options = android.graphics.BitmapFactory.Options().apply {
                                        inJustDecodeBounds = true
                                    }
                                    android.graphics.BitmapFactory.decodeFile(file.absolutePath, options)
                                    var scale = 1
                                    while (options.outWidth / scale / 2 >= 512 && options.outHeight / scale / 2 >= 512) {
                                        scale *= 2
                                    }
                                    val decodeOptions = android.graphics.BitmapFactory.Options().apply {
                                        inSampleSize = scale
                                    }
                                    android.graphics.BitmapFactory.decodeFile(file.absolutePath, decodeOptions)
                                } catch (e: Throwable) {
                                    null
                                }
                            }
                            if (bitmap != null) {
                                Column(modifier = Modifier.fillMaxWidth().height(160.dp)) {
                                    androidx.compose.foundation.Image(
                                        bitmap = bitmap.asImageBitmap(),
                                        contentDescription = "Clipboard Image",
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .fillMaxHeight()
                                            .clip(RoundedCornerShape(8.dp)),
                                        contentScale = androidx.compose.ui.layout.ContentScale.Fit
                                    )
                                }
                            } else {
                                Text("Failed to parse image bitmap", color = DangerRed, fontSize = 12.sp)
                            }
                        } else {
                            Text("Image file missing on disk", color = DangerRed, fontSize = 12.sp)
                        }
                    }
                }
                ClipboardItemType.URL.value -> {
                    Column {
                        if (!item.title.isNullOrBlank()) {
                            Text(item.title, color = TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Spacer(Modifier.height(2.dp))
                        }
                        Text(
                            text = item.content,
                            color = Blue400,
                            fontSize = 13.sp,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.clickable {
                                try {
                                    val intent = Intent(Intent.ACTION_VIEW, Uri.parse(item.content))
                                    context.startActivity(intent)
                                } catch (e: Exception) { }
                            }
                        )
                    }
                }
                ClipboardItemType.CODE.value -> {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(8.dp))
                            .background(DarkBg)
                            .padding(10.dp)
                    ) {
                        Text(
                            text = item.content,
                            color = SuccessGreen,
                            fontFamily = FontFamily.Monospace,
                            fontSize = 12.sp,
                            maxLines = 6,
                            overflow = TextOverflow.Ellipsis
                        )
                    }
                }
                else -> {
                    // Standard text with Sensitive Masking support
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = if (item.isSensitive && item.isMasked) "••••••••••••" else item.content,
                            color = TextPrimary,
                            fontSize = 14.sp,
                            maxLines = 4,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.weight(1f)
                        )
                        if (item.isSensitive) {
                            IconButton(onClick = onMaskToggle, modifier = Modifier.size(28.dp)) {
                                Icon(
                                    imageVector = if (item.isMasked) Icons.Rounded.VisibilityOff else Icons.Rounded.Visibility,
                                    contentDescription = "Mask Toggle",
                                    tint = TextMuted,
                                    modifier = Modifier.size(16.dp)
                                )
                            }
                        }
                    }
                }
            }

            Spacer(Modifier.height(12.dp))

            // Footer / Bottom Actions
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = getRelativeTime(item.timestamp),
                    color = TextMuted,
                    fontSize = 10.sp
                )
                
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(
                        onClick = onCopy,
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 2.dp),
                        shape = RoundedCornerShape(8.dp),
                        border = BorderStroke(1.dp, BorderColor),
                        modifier = Modifier.height(30.dp)
                    ) {
                        Icon(Icons.Rounded.ContentCopy, null, tint = TextSecondary, modifier = Modifier.size(14.dp))
                        Spacer(Modifier.width(4.dp))
                        Text("Copy", color = TextSecondary, fontSize = 11.sp)
                    }
                    Button(
                        onClick = onSend,
                        contentPadding = PaddingValues(horizontal = 12.dp, vertical = 2.dp),
                        shape = RoundedCornerShape(8.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = Teal400),
                        modifier = Modifier.height(30.dp)
                    ) {
                        Icon(Icons.Rounded.Send, null, tint = Color.White, modifier = Modifier.size(14.dp))
                        Spacer(Modifier.width(4.dp))
                        Text("Send", color = Color.White, fontSize = 11.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
        }
    }

    // JSON Prettifier Dialog
    if (showPrettifyDialog) {
        val prettyJson = remember(item.content) {
            try {
                val obj = org.json.JSONObject(item.content)
                obj.toString(2)
            } catch (e: Exception) {
                try {
                    val arr = org.json.JSONArray(item.content)
                    arr.toString(2)
                } catch (e2: Exception) {
                    item.content
                }
            }
        }
        AlertDialog(
            onDismissRequest = { showPrettifyDialog = false },
            containerColor = CardBg,
            title = { Text("Prettified JSON", color = TextPrimary, fontWeight = androidx.compose.ui.text.font.FontWeight.Bold) },
            text = {
                androidx.compose.foundation.rememberScrollState().let { scrollState ->
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .heightIn(max = 340.dp)
                            .verticalScroll(scrollState)
                    ) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .background(DarkBg)
                                .padding(12.dp)
                        ) {
                            Text(
                                text = prettyJson,
                                color = SuccessGreen,
                                fontFamily = FontFamily.Monospace,
                                fontSize = 12.sp
                            )
                        }
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                        cb.setPrimaryClip(ClipData.newPlainText("json", prettyJson))
                        Toast.makeText(context, "Pretty JSON copied", Toast.LENGTH_SHORT).show()
                        showPrettifyDialog = false
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                ) { Text("Copy", color = Color.White, fontWeight = FontWeight.Bold) }
            },
            dismissButton = {
                TextButton(onClick = { showPrettifyDialog = false }) { Text("Close", color = TextMuted) }
            }
        )
    }
}

@Composable
fun SnippetsTab(
    snippets: List<SnippetItemEntity>,
    searchQuery: String,
    onQueryChanged: (String) -> Unit,
    onDeleteSnippet: (SnippetItemEntity) -> Unit,
    onEditSnippet: (SnippetItemEntity) -> Unit = {}
) {
    val filtered = remember(snippets, searchQuery) {
        if (searchQuery.isBlank()) snippets
        else snippets.filter {
            it.trigger.contains(searchQuery, ignoreCase = true) || it.content.contains(searchQuery, ignoreCase = true)
        }
    }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        SearchBar(
            query = searchQuery,
            onQueryChange = onQueryChanged,
            placeholder = "Search shortcuts..."
        )
        
        Spacer(Modifier.height(14.dp))

        Row(
            modifier = Modifier.fillMaxWidth().padding(bottom = 6.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                "TEXT EXPANDER SNIPPETS", color = TextMuted, fontSize = 10.sp,
                fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp
            )
        }

        if (filtered.isEmpty()) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Icon(Icons.Rounded.Bolt, null, tint = TextMuted, modifier = Modifier.size(48.dp))
                    Spacer(Modifier.height(8.dp))
                    Text("No snippets added yet", color = TextMuted, fontSize = 13.sp)
                    Text("Accessibility Service must be active to expand snippets", color = TextMuted.copy(0.6f), fontSize = 11.sp)
                }
            }
        } else {
            LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                items(filtered, key = { it.id }) { snippet ->
                    SnippetCard(
                        snippet = snippet,
                        onDelete = { onDeleteSnippet(snippet) },
                        onEdit = { onEditSnippet(snippet) }
                    )
                }
            }
        }
    }
}

@Composable
fun SnippetCard(snippet: SnippetItemEntity, onDelete: () -> Unit, onEdit: () -> Unit = {}) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg),
        border = BorderStroke(1.dp, BorderColor)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(6.dp))
                        .background(Teal400.copy(0.12f))
                        .padding(horizontal = 8.dp, vertical = 3.dp)
                ) {
                    Text(snippet.trigger, color = Teal400, fontSize = 12.sp, fontWeight = FontWeight.Bold, fontFamily = FontFamily.Monospace)
                }
                Spacer(Modifier.weight(1f))
                IconButton(onClick = onEdit, modifier = Modifier.size(28.dp)) {
                    Icon(Icons.Rounded.Edit, "Edit", tint = TextMuted, modifier = Modifier.size(16.dp))
                }
                IconButton(onClick = onDelete, modifier = Modifier.size(28.dp)) {
                    Icon(Icons.Rounded.Delete, "Delete", tint = DangerRed.copy(0.8f), modifier = Modifier.size(16.dp))
                }
            }
            Spacer(Modifier.height(8.dp))
            Text(snippet.content, color = TextPrimary, fontSize = 14.sp)
            if (!snippet.description.isNullOrBlank()) {
                Spacer(Modifier.height(4.dp))
                Text(snippet.description, color = TextMuted, fontSize = 11.sp)
            }
        }
    }
}

@Composable
fun DeviceSelectorContent(
    peers: List<PeerDevice>,
    onDeviceSelected: (PeerDevice) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(18.dp)
    ) {
        Text(
            text = "SELECT RECIPIENT DEVICE",
            color = TextMuted,
            fontSize = 11.sp,
            fontWeight = FontWeight.Bold,
            letterSpacing = 1.2.sp
        )
        Spacer(Modifier.height(14.dp))

        if (peers.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 24.dp),
                contentAlignment = Alignment.Center
            ) {
                Text("No devices online nearby.", color = TextMuted, fontSize = 13.sp)
            }
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                items(peers) { peer ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(10.dp))
                            .background(DarkBg)
                            .clickable { onDeviceSelected(peer) }
                            .padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(38.dp)
                                .clip(CircleShape)
                                .background(Teal400.copy(0.15f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Rounded.Computer, null, tint = Teal400, modifier = Modifier.size(20.dp))
                        }
                        Spacer(Modifier.width(12.dp))
                        Column {
                            Text(peer.name, color = TextPrimary, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                            Text(peer.ip, color = TextMuted, fontSize = 11.sp)
                        }
                    }
                }
            }
        }
        Spacer(Modifier.height(12.dp))
    }
}

@Composable
fun AddSnippetDialog(
    onDismiss: () -> Unit,
    onSave: (trigger: String, content: String, desc: String) -> Unit
) {
    var trigger by remember { mutableStateOf("") }
    var content by remember { mutableStateOf("") }
    var desc by remember { mutableStateOf("") }
    var triggerError by remember { mutableStateOf<String?>(null) }

    val delimiterHint = "; . / ! @ # : , ? * - _ + = ~"

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("New Snippet", color = TextPrimary, fontWeight = FontWeight.Bold) },
        containerColor = CardBg,
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                // Hint box
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(Teal400.copy(0.08f))
                        .padding(10.dp)
                ) {
                    Text(
                        text = "Trigger must start or end with a special symbol.\nAllowed: $delimiterHint\nExamples: :ph  em;  /addr  hello#",
                        color = TextMuted,
                        fontSize = 11.sp,
                        lineHeight = 16.sp
                    )
                }
                OutlinedTextField(
                    value = trigger,
                    onValueChange = {
                        trigger = it
                        triggerError = null
                    },
                    label = { Text("Shortcut (Trigger)") },
                    placeholder = { Text("e.g. :ph  or  em;") },
                    isError = triggerError != null,
                    supportingText = triggerError?.let { { Text(it, color = DangerRed, fontSize = 11.sp) } },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = if (triggerError != null) DangerRed else Teal400,
                        unfocusedBorderColor = if (triggerError != null) DangerRed else BorderColor
                    ),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = content,
                    onValueChange = { content = it },
                    label = { Text("Expanded Text") },
                    placeholder = { Text("e.g. +8801700000000") },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400,
                        unfocusedBorderColor = BorderColor
                    ),
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = desc,
                    onValueChange = { desc = it },
                    label = { Text("Description (Optional)") },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400,
                        unfocusedBorderColor = BorderColor
                    ),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val t = trigger.trim()
                    when {
                        t.isBlank() -> triggerError = "Please enter a trigger."
                        !com.clipboardpro.share.service.TextExpanderService.hasValidDelimiter(t) ->
                            triggerError = "Must start or end with a symbol: $delimiterHint"
                        content.isBlank() -> { /* handled by disabled state */ }
                        else -> onSave(t, content, desc)
                    }
                },
                enabled = content.isNotBlank(),
                colors = ButtonDefaults.buttonColors(containerColor = Teal400)
            ) {
                Text("Save", color = Color.White, fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel", color = TextMuted)
            }
        }
    )
}

fun getRelativeTime(timestamp: Long): String {
    val diff = System.currentTimeMillis() - timestamp
    return when {
        diff < 60_000 -> "Just now"
        diff < 3600_000 -> "${diff / 60_000}m ago"
        diff < 86400_000 -> "${diff / 3600_000}h ago"
        else -> "${diff / 86400_000}d ago"
    }
}

@Composable
fun navColors() = NavigationBarItemDefaults.colors(
    selectedIconColor = Teal400,
    selectedTextColor = Teal400,
    indicatorColor = Teal400.copy(alpha = 0.12f),
    unselectedIconColor = TextMuted,
    unselectedTextColor = TextMuted
)

@Composable
fun EditSnippetDialog(
    snippet: SnippetItemEntity,
    onDismiss: () -> Unit,
    onSave: (trigger: String, content: String, desc: String) -> Unit
) {
    var trigger by remember { mutableStateOf(snippet.trigger) }
    var content by remember { mutableStateOf(snippet.content) }
    var desc by remember { mutableStateOf(snippet.description ?: "") }
    var triggerError by remember { mutableStateOf<String?>(null) }

    val delimiterHint = "; . / ! @ # : , ? * - _ + = ~"

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Edit Snippet", color = TextPrimary, fontWeight = FontWeight.Bold) },
        containerColor = CardBg,
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                OutlinedTextField(
                    value = trigger,
                    onValueChange = {
                        trigger = it
                        triggerError = null
                    },
                    label = { Text("Shortcut (Trigger)") },
                    isError = triggerError != null,
                    supportingText = triggerError?.let { { Text(it, color = DangerRed, fontSize = 11.sp) } },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary, unfocusedTextColor = TextPrimary,
                        focusedBorderColor = if (triggerError != null) DangerRed else Teal400,
                        unfocusedBorderColor = if (triggerError != null) DangerRed else BorderColor
                    ),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = content,
                    onValueChange = { content = it },
                    label = { Text("Expanded Text") },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary, unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400, unfocusedBorderColor = BorderColor
                    ),
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = desc,
                    onValueChange = { desc = it },
                    label = { Text("Description (Optional)") },
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary, unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400, unfocusedBorderColor = BorderColor
                    ),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    val t = trigger.trim()
                    when {
                        t.isBlank() -> triggerError = "Please enter a trigger."
                        !com.clipboardpro.share.service.TextExpanderService.hasValidDelimiter(t) ->
                            triggerError = "Must start or end with a symbol: $delimiterHint"
                        content.isBlank() -> { /* handled by disabled state */ }
                        else -> onSave(t, content, desc)
                    }
                },
                enabled = content.isNotBlank(),
                colors = ButtonDefaults.buttonColors(containerColor = Teal400)
            ) { Text("Save", color = Color.White, fontWeight = FontWeight.Bold) }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel", color = TextMuted) }
        }
    )
}

@Composable
fun DevicesTab(
    peers: List<PeerDevice>,
    onSendFileSelected: (File, PeerDevice) -> Unit,
    onSendText: (String, PeerDevice) -> Unit,
    onSendClipboard: (PeerDevice) -> Unit
) {
    val context = LocalContext.current
    var selectedPeer by remember { mutableStateOf<PeerDevice?>(null) }
    var textInput by remember { mutableStateOf("") }
    
    val filePicker = rememberLauncherForActivityResult(
        ActivityResultContracts.GetMultipleContents()
    ) { uris ->
        val peer = selectedPeer ?: return@rememberLauncherForActivityResult
        uris.forEach { uri ->
            val file = uriToFile(context, uri) ?: return@forEach
            onSendFileSelected(file, peer)
        }
    }

    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp)
    ) {
        if (peers.isEmpty()) {
            ScanningAnimation()
        } else {
            Text(
                "ONLINE RECIPIENTS",
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
                        onClick = {
                            selectedPeer = if (selectedPeer?.ip == peer.ip) null else peer
                        }
                    )
                }
            }
            
            AnimatedVisibility(
                visible = selectedPeer != null,
                enter = slideInVertically { it } + fadeIn(),
                exit = slideOutVertically { it } + fadeOut()
            ) {
                val peer = selectedPeer
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
                                    if (textInput.isNotBlank() && peer != null) {
                                        onSendText(textInput, peer)
                                        textInput = ""
                                    }
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
                                    if (textInput.isNotBlank() && peer != null) {
                                        onSendText(textInput, peer)
                                        textInput = ""
                                    }
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
                                    tint = if (textInput.isNotBlank()) Color.White else TextMuted
                                )
                            }
                        }

                        Spacer(Modifier.height(12.dp))
                        HorizontalDivider(color = BorderColor, thickness = 1.dp)
                        Spacer(Modifier.height(12.dp))

                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            Button(
                                onClick = { filePicker.launch("*/*") },
                                modifier = Modifier.weight(1f),
                                shape = RoundedCornerShape(12.dp),
                                colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                            ) {
                                Icon(Icons.Rounded.UploadFile, null, modifier = Modifier.size(18.dp), tint = Color.White)
                                Spacer(Modifier.width(6.dp))
                                Text("Send File", fontWeight = FontWeight.SemiBold, color = Color.White, fontSize = 13.sp)
                            }
                            OutlinedButton(
                                onClick = { peer?.let { onSendClipboard(it) } },
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
        }
    }
}

@Composable
fun PeerCard(peer: PeerDevice, isSelected: Boolean, onClick: () -> Unit) {
    val scale by animateFloatAsState(
        targetValue = if (isSelected) 1.02f else 1.0f,
        animationSpec = spring(
            dampingRatio = Spring.DampingRatioMediumBouncy,
            stiffness = Spring.StiffnessLow
        ),
        label = "peer_scale"
    )
    val borderColor by animateColorAsState(
        if (isSelected) Teal400 else BorderColor, label = "peer_border"
    )
    val bgColor by animateColorAsState(
        if (isSelected) Teal400.copy(alpha = 0.08f) else CardBg, label = "peer_bg"
    )
    Card(
        onClick = onClick,
        modifier = Modifier
            .fillMaxWidth()
            .graphicsLayer {
                scaleX = scale
                scaleY = scale
            }
            .border(1.5.dp, borderColor, RoundedCornerShape(14.dp)),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = bgColor)
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
                    modifier = Modifier.size(24.dp)
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
fun SearchBar(
    query: String,
    onQueryChange: (String) -> Unit,
    placeholder: String,
    modifier: Modifier = Modifier
) {
    OutlinedTextField(
        value = query,
        onValueChange = onQueryChange,
        placeholder = { Text(placeholder, color = TextMuted, fontSize = 13.sp) },
        modifier = modifier.fillMaxWidth(),
        leadingIcon = { Icon(Icons.Rounded.Search, null, tint = TextMuted, modifier = Modifier.size(18.dp)) },
        trailingIcon = {
            if (query.isNotEmpty()) {
                IconButton(onClick = { onQueryChange("") }) {
                    Icon(Icons.Rounded.Clear, null, tint = TextMuted, modifier = Modifier.size(18.dp))
                }
            }
        },
        singleLine = true,
        colors = OutlinedTextFieldDefaults.colors(
            focusedTextColor = TextPrimary,
            unfocusedTextColor = TextPrimary,
            focusedBorderColor = Teal400,
            unfocusedBorderColor = BorderColor,
            cursorColor = Teal400,
            focusedContainerColor = CardBg,
            unfocusedContainerColor = CardBg
        ),
        shape = RoundedCornerShape(12.dp)
    )
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
                "Make sure ClipboardPro is active\non your Windows PC",
                color = TextMuted, fontSize = 12.sp,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(horizontal = 40.dp)
            )
        }
    }
}

@Composable
fun TransfersTab(
    transfers: List<TransferItem>,
    onDeleteTransfer: (String) -> Unit,
    onClearAllTransfers: () -> Unit
) {
    var searchQuery by remember { mutableStateOf("") }
    val filteredTransfers = remember(transfers, searchQuery) {
        if (searchQuery.isBlank()) transfers
        else transfers.filter {
            it.fileName.contains(searchQuery, ignoreCase = true) || it.peerName.contains(searchQuery, ignoreCase = true)
        }
    }

    if (transfers.isEmpty()) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Rounded.SwapVert, null, tint = TextMuted, modifier = Modifier.size(48.dp))
                Spacer(Modifier.height(12.dp))
                Text("No transfer history", color = TextMuted, fontSize = 14.sp)
            }
        }
        return
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        item {
            SearchBar(
                query = searchQuery,
                onQueryChange = { searchQuery = it },
                placeholder = "Search transfers..."
            )
            Spacer(Modifier.height(8.dp))
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth().padding(bottom = 4.dp, start = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    "TRANSFER LOGS", color = TextMuted, fontSize = 10.sp,
                    fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp
                )
                if (filteredTransfers.isNotEmpty()) {
                    Text(
                        "Clear All",
                        color = DangerRed,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier.clickable { onClearAllTransfers() }
                    )
                }
            }
        }

        items(filteredTransfers, key = { it.id }) { t ->
            TransferCard(
                transfer = t,
                onDelete = { onDeleteTransfer(t.id) }
            )
        }
    }
}

@Composable
fun TransferCard(transfer: TransferItem, onDelete: () -> Unit) {
    val context = LocalContext.current

    val statusColor = when (transfer.status) {
        TransferStatus.COMPLETED -> SuccessGreen
        TransferStatus.FAILED, TransferStatus.CANCELLED -> DangerRed
        TransferStatus.ACTIVE -> Teal400
        else -> TextMuted
    }
    val dirIcon = if (transfer.direction == TransferDirection.SEND)
        Icons.Rounded.Upload else Icons.Rounded.Download

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(enabled = transfer.status == TransferStatus.COMPLETED && transfer.fileUri != null) {
                openReceivedFile(context, transfer.fileUri!!)
            },
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
                Spacer(Modifier.width(8.dp))
                // Copy button — only shown for completed transfers with a URI
                if (transfer.status == TransferStatus.COMPLETED && transfer.fileUri != null) {
                    IconButton(
                        onClick = {
                            val cb = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cb.setPrimaryClip(ClipData.newPlainText("File Path", transfer.fileUri))
                            Toast.makeText(context, "Path copied", Toast.LENGTH_SHORT).show()
                        },
                        modifier = Modifier.size(28.dp)
                    ) {
                        Icon(
                            Icons.Rounded.ContentCopy,
                            contentDescription = "Copy Path",
                            tint = Teal400.copy(alpha = 0.8f),
                            modifier = Modifier.size(16.dp)
                        )
                    }
                }
                IconButton(
                    onClick = onDelete,
                    modifier = Modifier.size(28.dp)
                ) {
                    Icon(
                        Icons.Rounded.Delete,
                        contentDescription = "Delete",
                        tint = DangerRed.copy(alpha = 0.8f),
                        modifier = Modifier.size(16.dp)
                    )
                }
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
            // Tap hint for completed files
            if (transfer.status == TransferStatus.COMPLETED && transfer.fileUri != null) {
                Spacer(Modifier.height(6.dp))
                Text(
                    "Tap to open file",
                    color = TextMuted.copy(0.6f),
                    fontSize = 10.sp
                )
            }
        }
    }
}

private fun openReceivedFile(context: Context, fileUriStr: String) {
    try {
        val parsedUri = Uri.parse(fileUriStr)
        val uri = if (parsedUri.scheme == "file") {
            val file = parsedUri.path?.let { File(it) }
            if (file != null && file.exists()) {
                FileProvider.getUriForFile(context, "${context.packageName}.provider", file)
            } else {
                parsedUri
            }
        } else {
            parsedUri
        }
        val type = context.contentResolver.getType(uri) ?: "*/*"
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, type)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(intent)
    } catch (e: Exception) {
        Toast.makeText(context, "Cannot open file: ${e.localizedMessage}", Toast.LENGTH_SHORT).show()
    }
}

private fun uriToFile(context: Context, uri: Uri): File? {
    return try {
        val fileName = getFileName(context, uri) ?: "file_${System.currentTimeMillis()}"
        val inputStream = context.contentResolver.openInputStream(uri) ?: return null
        val tempFile = File(context.cacheDir, fileName)
        tempFile.outputStream().use { inputStream.copyTo(it) }
        tempFile
    } catch (e: Exception) { null }
}

private fun getFileName(context: Context, uri: Uri): String? {
    var name: String? = null
    context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
        val idx = cursor.getColumnIndex(android.provider.OpenableColumns.DISPLAY_NAME)
        if (cursor.moveToFirst() && idx >= 0) name = cursor.getString(idx)
    }
    return name ?: uri.lastPathSegment
}
