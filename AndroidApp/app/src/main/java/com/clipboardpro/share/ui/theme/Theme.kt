package com.clipboardpro.share.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight

// Desktop-matched color palette
val Teal400    = Color(0xFF4F46E5) // Primary Accent Indigo
val TealGlow   = Color(0xFF6366F1) // Secondary Accent Glow
val Blue400    = Color(0xFF818CF8) // Accent Light
val DarkBg     = Color(0xFF0F172A) // BgDeepColor
val CardBg     = Color(0xFF1E293B) // BgCardColor / BgSidebarColor
val SurfaceBg  = Color(0xFF1E293B)
val ElevatedBg = Color(0xFF334155) // BgCardHoverColor
val TextPrimary = Color(0xFFF8FAFC) // TextPrimaryColor
val TextSecondary = Color(0xFFCBD5E1) // TextSecondaryColor
val TextMuted  = Color(0xFFA3B1C6) // TextMutedColor
val DangerRed  = Color(0xFFEF4444) // DangerColor
val SuccessGreen = Color(0xFF10B981) // SuccessColor
val WarningAmber = Color(0xFFF59E0B) // Amber
val BorderColor = Color(0xFF2D3748) // BorderColor

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
