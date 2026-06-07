package com.clipboardpro.share.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.PushPin
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material.icons.rounded.StarBorder
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.clipboardpro.share.data.ClipboardItemEntity
import com.clipboardpro.share.model.ClipboardItemType
import com.clipboardpro.share.ui.theme.*
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EditClipDialog(
    item: ClipboardItemEntity,
    onDismiss: () -> Unit,
    onSave: (updatedContent: String, isPinned: Boolean, isFavorite: Boolean, category: String?) -> Unit
) {
    var editedContent by remember { mutableStateOf(item.content) }
    var isPinned by remember { mutableStateOf(item.isPinned) }
    var isFavorite by remember { mutableStateOf(item.isFavorite) }
    var categoryInput by remember { mutableStateOf(item.category ?: "") }

    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = CardBg,
        title = {
            Text(
                text = "Edit Clipboard Item",
                color = TextPrimary,
                fontWeight = FontWeight.Bold,
                fontSize = 18.sp
            )
        },
        text = {
            Column(
                verticalArrangement = Arrangement.spacedBy(14.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                // If it is an image, show preview instead of text editor
                if (item.type == ClipboardItemType.IMAGE.value && !item.imagePath.isNullOrBlank()) {
                    val file = remember(item.imagePath) { File(item.imagePath) }
                    if (file.exists()) {
                        val bitmap = remember(item.imagePath) {
                            android.graphics.BitmapFactory.decodeFile(file.absolutePath)
                        }
                        if (bitmap != null) {
                            Image(
                                bitmap = bitmap.asImageBitmap(),
                                contentDescription = "Edit Clip Image Preview",
                                contentScale = ContentScale.Fit,
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(140.dp)
                                    .clip(RoundedCornerShape(8.dp))
                            )
                        }
                    }
                } else {
                    // Text Editor
                    OutlinedTextField(
                        value = editedContent,
                        onValueChange = { editedContent = it },
                        label = { Text("Content") },
                        minLines = 3,
                        maxLines = 6,
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
                }

                // Category
                OutlinedTextField(
                    value = categoryInput,
                    onValueChange = { categoryInput = it },
                    label = { Text("Category (e.g. Work, Personal)") },
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

                // Switches Row
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Pin Option
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.weight(1f)
                    ) {
                        Checkbox(
                            checked = isPinned,
                            onCheckedChange = { isPinned = it },
                            colors = CheckboxDefaults.colors(
                                checkedColor = Teal400,
                                uncheckedColor = TextMuted
                            )
                        )
                        Icon(
                            imageVector = Icons.Rounded.PushPin,
                            contentDescription = null,
                            tint = if (isPinned) Teal400 else TextMuted,
                            modifier = Modifier.size(16.dp)
                        )
                        Spacer(Modifier.width(4.dp))
                        Text("Pin", color = TextPrimary, fontSize = 13.sp)
                    }

                    // Favorite Option
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.weight(1f)
                    ) {
                        Checkbox(
                            checked = isFavorite,
                            onCheckedChange = { isFavorite = it },
                            colors = CheckboxDefaults.colors(
                                checkedColor = DangerRed,
                                uncheckedColor = TextMuted
                            )
                        )
                        Icon(
                            imageVector = if (isFavorite) Icons.Rounded.Star else Icons.Rounded.StarBorder,
                            contentDescription = null,
                            tint = if (isFavorite) DangerRed else TextMuted,
                            modifier = Modifier.size(16.dp)
                        )
                        Spacer(Modifier.width(4.dp))
                        Text("Favorite", color = TextPrimary, fontSize = 13.sp)
                    }
                }
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    onSave(
                        editedContent,
                        isPinned,
                        isFavorite,
                        categoryInput.trim().ifBlank { null }
                    )
                },
                colors = ButtonDefaults.buttonColors(containerColor = Teal400),
                shape = RoundedCornerShape(10.dp)
            ) {
                Text("Save", color = DarkBg, fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel", color = TextMuted)
            }
        }
    )
}
