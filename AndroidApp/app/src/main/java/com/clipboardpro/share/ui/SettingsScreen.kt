package com.clipboardpro.share.ui

import android.content.Context
import android.content.SharedPreferences
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.clipboardpro.share.ui.theme.*

fun getPrefs(ctx: Context): SharedPreferences =
    ctx.getSharedPreferences("localshare_prefs", Context.MODE_PRIVATE)

fun getSaveFolder(ctx: Context): String =
    getPrefs(ctx).getString("save_folder", "Download/Received") ?: "Download/Received"

fun setSaveFolder(ctx: Context, path: String) =
    getPrefs(ctx).edit().putString("save_folder", path).apply()

fun getAutoClipboard(ctx: Context): Boolean =
    getPrefs(ctx).getBoolean("auto_clipboard", true)

fun setAutoClipboard(ctx: Context, v: Boolean) =
    getPrefs(ctx).edit().putBoolean("auto_clipboard", v).apply()

fun setThemeMode(ctx: Context, mode: String) =
    getPrefs(ctx).edit().putString("theme_mode", mode).apply()

@Composable
fun SettingsScreen(
    themeMode: String,
    onThemeModeChanged: (String) -> Unit,
    onBack: () -> Unit
) {
    val context = LocalContext.current
    var saveFolder by remember { mutableStateOf(getSaveFolder(context)) }
    var autoClipboard by remember { mutableStateOf(getAutoClipboard(context)) }
    var showFolderDialog by remember { mutableStateOf(false) }
    var customFolderInput by remember { mutableStateOf(saveFolder) }
    var showThemeDialog by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(DarkBg)
    ) {
        // Header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(CardBg)
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.Rounded.ArrowBack, null, tint = TextPrimary)
            }
            Spacer(Modifier.width(8.dp))
            Text("Settings", color = TextPrimary, fontWeight = FontWeight.Bold, fontSize = 18.sp)
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            SettingSectionLabel("FILE STORAGE")

            // Received Folder
            SettingCard(
                icon = Icons.Rounded.Folder,
                title = "Received Files Folder",
                subtitle = "Download/$saveFolder",
                onClick = { customFolderInput = saveFolder; showFolderDialog = true }
            )

            SettingSectionLabel("CLIPBOARD")

            // Auto-clipboard on receive
            SettingCardToggle(
                icon = Icons.Rounded.ContentPaste,
                title = "Auto-copy Received Text",
                subtitle = "Automatically copy incoming text to clipboard",
                checked = autoClipboard,
                onCheckedChange = {
                    autoClipboard = it
                    setAutoClipboard(context, it)
                }
            )

            SettingSectionLabel("APPEARANCE")

            // Theme Mode
            val themeLabel = when (themeMode) {
                "light" -> "Light Theme"
                "dark" -> "Dark Theme"
                else -> "System Default"
            }
            SettingCard(
                icon = Icons.Rounded.Palette,
                title = "Theme Mode",
                subtitle = themeLabel,
                onClick = { showThemeDialog = true }
            )

            SettingSectionLabel("ABOUT")

            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(14.dp),
                colors = CardDefaults.cardColors(containerColor = CardBg),
                border = androidx.compose.foundation.BorderStroke(1.dp, BorderColor)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Box(
                        modifier = Modifier
                            .size(64.dp)
                            .clip(RoundedCornerShape(16.dp))
                            .background(Teal400.copy(alpha = 0.12f)),
                        contentAlignment = Alignment.Center
                    ) {
                        androidx.compose.foundation.Image(
                            painter = androidx.compose.ui.res.painterResource(id = com.clipboardpro.share.R.drawable.logo),
                            contentDescription = "App Logo",
                            modifier = Modifier.size(44.dp)
                        )
                    }
                    Spacer(Modifier.height(12.dp))
                    Text(
                        text = "ClipboardPro Local Share",
                        color = TextPrimary,
                        fontWeight = FontWeight.Bold,
                        fontSize = 16.sp
                    )
                    Spacer(Modifier.height(4.dp))
                    Text(
                        text = "Version 1.0.0",
                        color = Teal400,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 12.sp
                    )
                    Spacer(Modifier.height(16.dp))
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(1.dp)
                            .background(BorderColor)
                    )
                    Spacer(Modifier.height(16.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceEvenly
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(text = "Developer", color = TextMuted, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                            Spacer(Modifier.height(2.dp))
                            Text(text = "Magnetieght EU", color = TextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                        }
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(text = "Developed By", color = TextMuted, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                            Spacer(Modifier.height(2.dp))
                            Text(text = "Cross Tech", color = TextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                        }
                    }
                    Spacer(Modifier.height(24.dp))
                    Text(
                        text = "© 2026 Magnetieght EU. All rights reserved.",
                        color = TextMuted,
                        fontSize = 10.sp
                    )
                }
            }
        }
    }

    if (showFolderDialog) {
        AlertDialog(
            onDismissRequest = { showFolderDialog = false },
            containerColor = CardBg,
            title = {
                Text("Received Folder", color = TextPrimary, fontWeight = FontWeight.Bold)
            },
            text = {
                Column {
                    Text(
                        "Files are saved to Downloads/<folder>",
                        color = TextMuted, fontSize = 12.sp,
                        modifier = Modifier.padding(bottom = 12.dp)
                    )
                    OutlinedTextField(
                        value = customFolderInput,
                        onValueChange = { customFolderInput = it },
                        label = { Text("Subfolder name", color = TextMuted) },
                        singleLine = true,
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedTextColor = TextPrimary,
                            unfocusedTextColor = TextPrimary,
                            focusedBorderColor = Teal400,
                            unfocusedBorderColor = BorderColor,
                            cursorColor = Teal400,
                            focusedLabelColor = Teal400,
                            unfocusedLabelColor = TextMuted
                        ),
                        shape = RoundedCornerShape(10.dp),
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        val clean = customFolderInput.trim().ifBlank { "Received" }
                        saveFolder = clean
                        setSaveFolder(context, clean)
                        showFolderDialog = false
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                ) { Text("Save", color = DarkBg, fontWeight = FontWeight.Bold) }
            },
            dismissButton = {
                TextButton(onClick = { showFolderDialog = false }) {
                    Text("Cancel", color = TextMuted)
                }
            }
        )
    }

    if (showThemeDialog) {
        AlertDialog(
            onDismissRequest = { showThemeDialog = false },
            title = { Text("Select Theme Mode", color = TextPrimary, fontWeight = FontWeight.Bold) },
            containerColor = CardBg,
            textContentColor = TextPrimary,
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    listOf("system" to "System Default", "light" to "Light Theme", "dark" to "Dark Theme").forEach { (value, label) ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    onThemeModeChanged(value)
                                    setThemeMode(context, value)
                                    showThemeDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = (themeMode == value),
                                onClick = {
                                    onThemeModeChanged(value)
                                    setThemeMode(context, value)
                                    showThemeDialog = false
                                },
                                colors = RadioButtonDefaults.colors(
                                    selectedColor = Teal400,
                                    unselectedColor = TextMuted
                                )
                            )
                            Spacer(Modifier.width(12.dp))
                            Text(label, color = TextPrimary, fontSize = 14.sp)
                        }
                    }
                }
            },
            confirmButton = {}
        )
    }
}

@Composable
fun SettingSectionLabel(text: String) {
    Text(
        text = text,
        color = TextMuted,
        fontSize = 10.sp,
        fontWeight = FontWeight.Bold,
        letterSpacing = 1.5.sp,
        modifier = Modifier.padding(start = 4.dp, top = 8.dp, bottom = 4.dp)
    )
}

@Composable
fun SettingCard(
    icon: ImageVector,
    title: String,
    subtitle: String,
    onClick: (() -> Unit)? = null
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg),
        border = androidx.compose.foundation.BorderStroke(1.dp, BorderColor)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .background(Teal400.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(icon, null, tint = Teal400, modifier = Modifier.size(22.dp))
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                Text(subtitle, color = TextMuted, fontSize = 12.sp)
            }
            if (onClick != null) {
                Icon(Icons.Rounded.ChevronRight, null, tint = TextMuted, modifier = Modifier.size(20.dp))
            }
        }
    }
}

@Composable
fun SettingCardToggle(
    icon: ImageVector,
    title: String,
    subtitle: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = CardBg),
        border = androidx.compose.foundation.BorderStroke(1.dp, BorderColor)
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .background(Teal400.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(icon, null, tint = Teal400, modifier = Modifier.size(22.dp))
            }
            Spacer(Modifier.width(14.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = TextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                Text(subtitle, color = TextMuted, fontSize = 12.sp)
            }
            Switch(
                checked = checked,
                onCheckedChange = onCheckedChange,
                colors = SwitchDefaults.colors(
                    checkedThumbColor = DarkBg,
                    checkedTrackColor = Teal400,
                    uncheckedTrackColor = BorderColor
                )
            )
        }
    }
}
