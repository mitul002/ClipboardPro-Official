package com.clipboardpro.share.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight

// Desktop-matched color palette
val Teal400    = Color(0xFF00B4D8)
val TealGlow   = Color(0xFF48CAE4)
val Blue400    = Color(0xFF4FACFE)
val DarkBg     = Color(0xFF0D0D0F)
val CardBg     = Color(0xFF1A1A1F)
val SurfaceBg  = Color(0xFF141418)
val ElevatedBg = Color(0xFF222228)
val TextPrimary = Color(0xFFE8E8F0)
val TextSecondary = Color(0xFFB0B0C0)
val TextMuted  = Color(0xFF888899)
val DangerRed  = Color(0xFFFF5555)
val SuccessGreen = Color(0xFF27C93F)
val WarningAmber = Color(0xFFFFA500)
val BorderColor = Color(0xFF2A2A35)

private val DarkColors = darkColorScheme(
    primary          = Teal400,
    onPrimary        = DarkBg,
    secondary        = Blue400,
    onSecondary      = DarkBg,
    background       = DarkBg,
    surface          = CardBg,
    surfaceVariant   = ElevatedBg,
    onBackground     = TextPrimary,
    onSurface        = TextPrimary,
    onSurfaceVariant = TextSecondary,
    error            = DangerRed,
    tertiary         = SuccessGreen,
    outline          = BorderColor
)

@Composable
fun ClipboardProTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = DarkColors,
        content = content
    )
}
