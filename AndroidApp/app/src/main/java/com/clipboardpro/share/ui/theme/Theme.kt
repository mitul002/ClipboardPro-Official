package com.clipboardpro.share.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

val Teal400 = Color(0xFF00F2FE)
val Teal700 = Color(0xFF0093B7)
val Blue400 = Color(0xFF4FACFE)
val DarkBg = Color(0xFF0D0D0F)
val CardBg = Color(0xFF1A1A1F)
val SurfaceBg = Color(0xFF141418)
val TextPrimary = Color(0xFFE8E8F0)
val TextMuted = Color(0xFF888899)
val DangerRed = Color(0xFFFF5555)
val SuccessGreen = Color(0xFF27C93F)

private val DarkColors = darkColorScheme(
    primary = Teal400,
    onPrimary = DarkBg,
    secondary = Blue400,
    onSecondary = DarkBg,
    background = DarkBg,
    surface = CardBg,
    onBackground = TextPrimary,
    onSurface = TextPrimary,
    error = DangerRed,
    tertiary = SuccessGreen
)

@Composable
fun ClipboardProTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = DarkColors,
        content = content
    )
}
