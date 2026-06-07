package com.clipboardpro.share.ui

import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.net.Uri
import android.widget.Toast
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
import com.clipboardpro.share.data.AppDatabase
import com.clipboardpro.share.service.LicenseService
import com.clipboardpro.share.service.TrialService
import com.clipboardpro.share.ui.theme.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream

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

            SettingSectionLabel("INTEGRATIONS")

            var yoinkEnabled by remember {
                mutableStateOf(
                    getPrefs(context).getBoolean("floating_yoink_enabled", false)
                )
            }
            SettingCardToggle(
                icon = Icons.Rounded.History,
                title = "Floating Yoink Shelf",
                subtitle = "Show drag-and-drop floating overlay bubble",
                checked = yoinkEnabled,
                onCheckedChange = { checked ->
                    if (checked && !android.provider.Settings.canDrawOverlays(context)) {
                        val intent = Intent(
                            android.provider.Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                            Uri.parse("package:${context.packageName}")
                        )
                        context.startActivity(intent)
                        Toast.makeText(context, "Please allow overlay permission", Toast.LENGTH_LONG).show()
                    } else {
                        yoinkEnabled = checked
                        getPrefs(context).edit().putBoolean("floating_yoink_enabled", checked).apply()
                        
                        val intent = Intent(context, com.clipboardpro.share.service.FloatingYoinkService::class.java)
                        if (checked) {
                            context.startService(intent)
                        } else {
                            context.stopService(intent)
                        }
                    }
                }
            )

            SettingCard(
                icon = Icons.Rounded.Bolt,
                title = "Text Expander Service",
                subtitle = "Configure triggers in Accessibility Settings",
                onClick = {
                    try {
                        val intent = Intent(android.provider.Settings.ACTION_ACCESSIBILITY_SETTINGS)
                        context.startActivity(intent)
                    } catch (e: Exception) {
                        Toast.makeText(context, "Could not open Accessibility settings", Toast.LENGTH_SHORT).show()
                    }
                }
            )

            SettingSectionLabel("LICENSE & TRIAL")

            val licenseService = remember { LicenseService(context) }
            val trialService = remember { TrialService(context) }
            var licenseStatus by remember { mutableStateOf(licenseService.getLicenseStatus()) }
            var showLicenseDialog by remember { mutableStateOf(false) }

            val statusText = when {
                licenseStatus.isLicensed -> "Pro Version Active (${licenseStatus.keyPreview})"
                licenseStatus.trialExpired -> "Trial Expired. Activate now."
                else -> "Trial Active (${trialService.getRemainingDays()} days remaining)"
            }

            SettingCard(
                icon = Icons.Rounded.Key,
                title = "License Management",
                subtitle = statusText,
                onClick = { showLicenseDialog = true }
            )

            SettingSectionLabel("DATABASE & MAINTENANCE")

            var maxHistory by remember {
                mutableStateOf(
                    getPrefs(context).getInt("max_history_items", 200)
                )
            }
            var showMaxHistoryDialog by remember { mutableStateOf(false) }

            SettingCard(
                icon = Icons.Rounded.History,
                title = "Max Clipboard Items",
                subtitle = "$maxHistory items limit",
                onClick = { showMaxHistoryDialog = true }
            )

            val scope = rememberCoroutineScope()
            SettingCard(
                icon = Icons.Rounded.CleaningServices,
                title = "Compact Database (Vacuum)",
                subtitle = "Defragment SQLite file size and clean cache",
                onClick = {
                    scope.launch(Dispatchers.IO) {
                        try {
                            AppDatabase.getDatabase(context).openHelper.writableDatabase.execSQL("VACUUM")
                            withContext(Dispatchers.Main) {
                                Toast.makeText(context, "Database compacted successfully", Toast.LENGTH_SHORT).show()
                            }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) {
                                Toast.makeText(context, "Compact failed: ${e.localizedMessage}", Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                }
            )

            SettingCard(
                icon = Icons.Rounded.Backup,
                title = "Export Database Backup",
                subtitle = "Save backup as zip to Downloads",
                onClick = {
                    scope.launch(Dispatchers.IO) {
                        try {
                            val dbFile = context.getDatabasePath("app_database")
                            val dbWal = context.getDatabasePath("app_database-wal")
                            val dbShm = context.getDatabasePath("app_database-shm")
                            
                            val exportDir = File(
                                android.os.Environment.getExternalStoragePublicDirectory(android.os.Environment.DIRECTORY_DOWNLOADS),
                                "ClipboardPro"
                            ).apply { mkdirs() }
                            
                            val backupZip = File(exportDir, "Backup_${System.currentTimeMillis()}.zip")
                            
                            java.util.zip.ZipOutputStream(FileOutputStream(backupZip)).use { zos ->
                                listOf(dbFile, dbWal, dbShm).forEach { file ->
                                    if (file.exists()) {
                                        zos.putNextEntry(java.util.zip.ZipEntry(file.name))
                                        file.inputStream().use { it.copyTo(zos) }
                                        zos.closeEntry()
                                    }
                                }
                            }
                            
                            withContext(Dispatchers.Main) {
                                Toast.makeText(context, "Backup exported: ${backupZip.name}", Toast.LENGTH_LONG).show()
                            }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) {
                                Toast.makeText(context, "Export failed: ${e.localizedMessage}", Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                }
            )

            var showRestoreDialog by remember { mutableStateOf(false) }
            var tempBackupFile by remember { mutableStateOf<File?>(null) }
            val zipPicker = rememberLauncherForActivityResult(
                ActivityResultContracts.GetContent()
            ) { uri ->
                if (uri != null) {
                    scope.launch(Dispatchers.IO) {
                        try {
                            val tempFile = File(context.cacheDir, "temp_backup.zip")
                            context.contentResolver.openInputStream(uri)?.use { input ->
                                tempFile.outputStream().use { output ->
                                    input.copyTo(output)
                                }
                            }
                            
                            val tempDbDir = File(context.cacheDir, "temp_restore_db_dir").apply { mkdirs() }
                            tempDbDir.listFiles()?.forEach { it.delete() }
                            
                            java.util.zip.ZipInputStream(tempFile.inputStream()).use { zis ->
                                var entry = zis.nextEntry
                                while (entry != null) {
                                    val target = File(tempDbDir, entry.name)
                                    target.outputStream().use { zis.copyTo(it) }
                                    zis.closeEntry()
                                    entry = zis.nextEntry
                                }
                            }
                            tempFile.delete()
                            
                            val dbBackup = File(tempDbDir, "app_database")
                            if (dbBackup.exists()) {
                                tempBackupFile = dbBackup
                                withContext(Dispatchers.Main) {
                                    showRestoreDialog = true
                                }
                            } else {
                                withContext(Dispatchers.Main) {
                                    Toast.makeText(context, "Invalid backup zip file: database not found", Toast.LENGTH_LONG).show()
                                }
                            }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) {
                                Toast.makeText(context, "Failed to parse backup: ${e.localizedMessage}", Toast.LENGTH_SHORT).show()
                            }
                        }
                    }
                }
            }

            if (showRestoreDialog && tempBackupFile != null) {
                AlertDialog(
                    onDismissRequest = {
                        showRestoreDialog = false
                        tempBackupFile?.parentFile?.deleteRecursively()
                    },
                    containerColor = CardBg,
                    title = { Text("Restore Options", color = TextPrimary, fontWeight = FontWeight.Bold) },
                    text = {
                        Text(
                            "Choose how you want to restore the backup. You can merge it with your current history, or completely replace it.",
                            color = TextSecondary,
                            fontSize = 14.sp
                        )
                    },
                    confirmButton = {
                        Button(
                            onClick = {
                                val file = tempBackupFile ?: return@Button
                                showRestoreDialog = false
                                scope.launch(Dispatchers.IO) {
                                    try {
                                        val db = AppDatabase.getDatabase(context)
                                        val sDb = db.openHelper.writableDatabase
                                        sDb.execSQL("ATTACH DATABASE '${file.absolutePath}' AS temp_db")
                                        sDb.execSQL("INSERT OR IGNORE INTO main.ClipboardItems SELECT * FROM temp_db.ClipboardItems")
                                        try {
                                            sDb.execSQL("INSERT OR IGNORE INTO main.SnippetItems SELECT * FROM temp_db.SnippetItems")
                                        } catch (e: Exception) { }
                                        sDb.execSQL("DETACH DATABASE temp_db")
                                        
                                        file.parentFile?.deleteRecursively()
                                        withContext(Dispatchers.Main) {
                                            Toast.makeText(context, "Backup merged successfully!", Toast.LENGTH_LONG).show()
                                        }
                                    } catch (e: Exception) {
                                        withContext(Dispatchers.Main) {
                                            Toast.makeText(context, "Merge failed: ${e.localizedMessage}", Toast.LENGTH_LONG).show()
                                        }
                                    }
                                }
                            },
                            colors = ButtonDefaults.buttonColors(containerColor = Teal400),
                            shape = RoundedCornerShape(10.dp)
                        ) {
                            Text("Merge", color = DarkBg, fontWeight = FontWeight.Bold)
                        }
                    },
                    dismissButton = {
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            TextButton(
                                onClick = {
                                    val file = tempBackupFile ?: return@TextButton
                                    showRestoreDialog = false
                                    scope.launch(Dispatchers.IO) {
                                        try {
                                            val currentDbFile = context.getDatabasePath("app_database")
                                            val currentDbWal = context.getDatabasePath("app_database-wal")
                                            val currentDbShm = context.getDatabasePath("app_database-shm")
                                            
                                            AppDatabase.getDatabase(context).close()
                                            
                                            currentDbFile.delete()
                                            currentDbWal.delete()
                                            currentDbShm.delete()
                                            
                                            file.copyTo(currentDbFile, overwrite = true)
                                            
                                            val backupWal = File(file.parentFile, "app_database-wal")
                                            if (backupWal.exists()) {
                                                backupWal.copyTo(currentDbWal, overwrite = true)
                                            }
                                            val backupShm = File(file.parentFile, "app_database-shm")
                                            if (backupShm.exists()) {
                                                backupShm.copyTo(currentDbShm, overwrite = true)
                                            }
                                            
                                            file.parentFile?.deleteRecursively()
                                            withContext(Dispatchers.Main) {
                                                Toast.makeText(context, "Database replaced! Please restart the app.", Toast.LENGTH_LONG).show()
                                            }
                                        } catch (e: Exception) {
                                            withContext(Dispatchers.Main) {
                                                Toast.makeText(context, "Replacement failed: ${e.localizedMessage}", Toast.LENGTH_LONG).show()
                                            }
                                        }
                                    }
                                }
                            ) {
                                Text("Replace All", color = DangerRed)
                            }
                            TextButton(
                                onClick = {
                                    showRestoreDialog = false
                                    tempBackupFile?.parentFile?.deleteRecursively()
                                }
                            ) {
                                Text("Cancel", color = TextMuted)
                            }
                        }
                    }
                )
            }

            SettingCard(
                icon = Icons.Rounded.Restore,
                title = "Restore Database Backup",
                subtitle = "Select a backup zip from filesystem",
                onClick = { zipPicker.launch("application/zip") }
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

    if (showMaxHistoryDialog) {
        var inputVal by remember { mutableStateOf(maxHistory.toString()) }
        AlertDialog(
            onDismissRequest = { showMaxHistoryDialog = false },
            containerColor = CardBg,
            title = { Text("Max History Limit", color = TextPrimary, fontWeight = FontWeight.Bold) },
            text = {
                OutlinedTextField(
                    value = inputVal,
                    onValueChange = { inputVal = it },
                    label = { Text("Max Items Limit") },
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedTextColor = TextPrimary,
                        unfocusedTextColor = TextPrimary,
                        focusedBorderColor = Teal400,
                        unfocusedBorderColor = BorderColor
                    )
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        val parsed = inputVal.toIntOrNull() ?: 200
                        maxHistory = parsed
                        getPrefs(context).edit().putInt("max_history_items", parsed).apply()
                        showMaxHistoryDialog = false
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                ) { Text("Save", color = DarkBg, fontWeight = FontWeight.Bold) }
            },
            dismissButton = {
                TextButton(onClick = { showMaxHistoryDialog = false }) {
                    Text("Cancel", color = TextMuted)
                }
            }
        )
    }

    if (showLicenseDialog) {
        var keyInput by remember { mutableStateOf("") }
        var emailInput by remember { mutableStateOf("") }
        var isLoading by remember { mutableStateOf(false) }
        var errorMsg by remember { mutableStateOf("") }
        var showTransferBtn by remember { mutableStateOf(false) }

        AlertDialog(
            onDismissRequest = { showLicenseDialog = false },
            containerColor = CardBg,
            title = {
                Text(
                    text = if (licenseStatus.isLicensed) "License Details" else "Activate License",
                    color = TextPrimary,
                    fontWeight = FontWeight.Bold
                )
            },
            text = {
                Column(
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    if (licenseStatus.isLicensed) {
                        Text("Your ClipboardPro Pro version is successfully activated and registered.", color = TextPrimary, fontSize = 14.sp)
                        Spacer(Modifier.height(4.dp))
                        Text("Registered Email: ${licenseStatus.email}", color = TextMuted, fontSize = 13.sp)
                        Text("License Type: ${licenseStatus.licenseType?.uppercase()}", color = TextMuted, fontSize = 13.sp)
                        Text("Key Preview: ${licenseStatus.keyPreview}", color = TextMuted, fontSize = 13.sp)
                    } else {
                        val trialDays = trialService.getRemainingDays()
                        Text(
                            text = if (licenseStatus.trialExpired) "Trial expired. Enter license key below." else "Trial active. $trialDays days remaining.",
                            color = if (licenseStatus.trialExpired) DangerRed else TextMuted,
                            fontSize = 13.sp
                        )

                        OutlinedTextField(
                            value = keyInput,
                            onValueChange = { keyInput = it.uppercase() },
                            label = { Text("License Key") },
                            singleLine = true,
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedTextColor = TextPrimary,
                                unfocusedTextColor = TextPrimary,
                                focusedBorderColor = Teal400,
                                unfocusedBorderColor = BorderColor,
                                focusedLabelColor = Teal400,
                                unfocusedLabelColor = TextMuted
                            ),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        )

                        OutlinedTextField(
                            value = emailInput,
                            onValueChange = { emailInput = it },
                            label = { Text("Email (optional)") },
                            singleLine = true,
                            colors = OutlinedTextFieldDefaults.colors(
                                focusedTextColor = TextPrimary,
                                unfocusedTextColor = TextPrimary,
                                focusedBorderColor = Teal400,
                                unfocusedBorderColor = BorderColor,
                                focusedLabelColor = Teal400,
                                unfocusedLabelColor = TextMuted
                            ),
                            shape = RoundedCornerShape(10.dp),
                            modifier = Modifier.fillMaxWidth()
                        )

                        if (errorMsg.isNotEmpty()) {
                            Text(errorMsg, color = DangerRed, fontSize = 12.sp)
                        }
                    }
                }
            },
            confirmButton = {
                if (licenseStatus.isLicensed) {
                    Button(
                        onClick = {
                            licenseService.deactivateLicense()
                            licenseStatus = licenseService.getLicenseStatus()
                            Toast.makeText(context, "License deactivated.", Toast.LENGTH_SHORT).show()
                            showLicenseDialog = false
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = DangerRed)
                    ) {
                        Text("Deactivate", color = Color.White, fontWeight = FontWeight.Bold)
                    }
                } else {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        if (showTransferBtn) {
                            Button(
                                onClick = {
                                    isLoading = true
                                    errorMsg = ""
                                    scope.launch {
                                        val res = licenseService.requestTransferAsync(keyInput, emailInput)
                                        isLoading = false
                                        if (res.valid) {
                                            Toast.makeText(context, "Transfer request submitted.", Toast.LENGTH_LONG).show()
                                            errorMsg = "Transfer requested. Click activate to check."
                                            showTransferBtn = false
                                        } else {
                                            errorMsg = res.message
                                        }
                                    }
                                },
                                colors = ButtonDefaults.buttonColors(containerColor = WarningAmber)
                            ) {
                                Text("Transfer", color = DarkBg, fontWeight = FontWeight.Bold)
                            }
                        }

                        Button(
                            enabled = !isLoading,
                            onClick = {
                                if (keyInput.isBlank()) {
                                    errorMsg = "Please enter key."
                                    return@Button
                                }
                                isLoading = true
                                errorMsg = ""
                                scope.launch {
                                    val res = licenseService.activateLicenseAsync(keyInput, emailInput)
                                    isLoading = false
                                    if (res.valid) {
                                        licenseStatus = licenseService.getLicenseStatus()
                                        Toast.makeText(context, "License activated!", Toast.LENGTH_SHORT).show()
                                        showLicenseDialog = false
                                    } else {
                                        errorMsg = res.message
                                        if (res.canRequestTransfer) {
                                            showTransferBtn = true
                                        }
                                    }
                                }
                            },
                            colors = ButtonDefaults.buttonColors(containerColor = Teal400)
                        ) {
                            if (isLoading) {
                                CircularProgressIndicator(color = DarkBg, modifier = Modifier.size(16.dp))
                            } else {
                                Text("Activate", color = DarkBg, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }
            },
            dismissButton = {
                TextButton(onClick = { showLicenseDialog = false }) {
                    Text("Close", color = TextMuted)
                }
            }
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
