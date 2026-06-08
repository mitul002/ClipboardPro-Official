package com.clipboardpro.vault.ui

import android.content.Context
import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Key
import androidx.compose.material.icons.rounded.Lock
import androidx.compose.material.icons.rounded.Mail
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.clipboardpro.vault.service.LicenseService
import com.clipboardpro.vault.service.TrialService
import com.clipboardpro.vault.ui.theme.*
import kotlinx.coroutines.launch

@Composable
fun LicenseGateScreen(
    onActivationSuccess: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    
    val licenseService = remember { LicenseService(context) }
    val trialService = remember { TrialService(context) }
    val status = remember { licenseService.getLicenseStatus() }
    
    var licenseKey by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var isLoading by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf("") }
    var showTransferBtn by remember { mutableStateOf(false) }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(DarkBg)
            .padding(24.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(24.dp))
                .background(CardBg)
                .border(1.dp, BorderColor, RoundedCornerShape(24.dp))
                .padding(28.dp)
        ) {
            Icon(
                imageVector = Icons.Rounded.Lock,
                contentDescription = null,
                tint = Teal400,
                modifier = Modifier.size(64.dp)
            )
            
            Spacer(Modifier.height(16.dp))
            
            Text(
                text = "ClipboardPro Lock Gate",
                color = TextPrimary,
                fontSize = 22.sp,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center
            )
            
            Spacer(Modifier.height(8.dp))

            // State Display
            val statusText = when {
                status.offlineExpired -> "Offline grace period expired. Active internet connection required."
                status.trialExpired -> "Trial period expired. License activation required to continue."
                else -> "App requires activation."
            }

            Text(
                text = statusText,
                color = TextMuted,
                fontSize = 13.sp,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(horizontal = 8.dp)
            )

            Spacer(Modifier.height(24.dp))

            // License key input
            OutlinedTextField(
                value = licenseKey,
                onValueChange = { licenseKey = it.uppercase() },
                label = { Text("License Key") },
                leadingIcon = { Icon(Icons.Rounded.Key, null, tint = TextMuted) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Text,
                    imeAction = ImeAction.Next
                ),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedTextColor = TextPrimary,
                    unfocusedTextColor = TextPrimary,
                    focusedBorderColor = Teal400,
                    unfocusedBorderColor = BorderColor,
                    focusedLabelColor = Teal400,
                    unfocusedLabelColor = TextMuted
                ),
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(Modifier.height(12.dp))

            // Email input
            OutlinedTextField(
                value = email,
                onValueChange = { email = it },
                label = { Text("Email address (optional)") },
                leadingIcon = { Icon(Icons.Rounded.Mail, null, tint = TextMuted) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Email,
                    imeAction = ImeAction.Done
                ),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedTextColor = TextPrimary,
                    unfocusedTextColor = TextPrimary,
                    focusedBorderColor = Teal400,
                    unfocusedBorderColor = BorderColor,
                    focusedLabelColor = Teal400,
                    unfocusedLabelColor = TextMuted
                ),
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth()
            )

            if (errorMessage.isNotEmpty()) {
                Spacer(Modifier.height(14.dp))
                Text(
                    text = errorMessage,
                    color = DangerRed,
                    fontSize = 12.sp,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth()
                )
            }

            Spacer(Modifier.height(24.dp))

            if (isLoading) {
                CircularProgressIndicator(color = Teal400, modifier = Modifier.size(32.dp))
            } else {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    if (showTransferBtn) {
                        Button(
                            onClick = {
                                isLoading = true
                                errorMessage = ""
                                scope.launch {
                                    val res = licenseService.requestTransferAsync(licenseKey, email)
                                    isLoading = false
                                    if (res.valid) {
                                        Toast.makeText(context, "Transfer request submitted. Awaiting approval.", Toast.LENGTH_LONG).show()
                                        errorMessage = "Transfer pending approval. Click activate to retry."
                                        showTransferBtn = false
                                    } else {
                                        errorMessage = res.message
                                    }
                                }
                            },
                            colors = ButtonDefaults.buttonColors(containerColor = WarningAmber),
                            shape = RoundedCornerShape(12.dp),
                            modifier = Modifier.weight(1f)
                        ) {
                            Text("Request Transfer", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                        }
                    }

                    Button(
                        onClick = {
                            if (licenseKey.isBlank()) {
                                errorMessage = "Please enter your license key."
                                return@Button
                            }
                            isLoading = true
                            errorMessage = ""
                            scope.launch {
                                val res = licenseService.activateLicenseAsync(licenseKey, email)
                                isLoading = false
                                if (res.valid) {
                                    Toast.makeText(context, "Activation successful!", Toast.LENGTH_SHORT).show()
                                    onActivationSuccess()
                                } else {
                                    errorMessage = res.message
                                    if (res.canRequestTransfer) {
                                        showTransferBtn = true
                                    }
                                }
                            }
                        },
                        colors = ButtonDefaults.buttonColors(containerColor = Teal400),
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Activate License", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    }
                }
            }
        }
    }
}
