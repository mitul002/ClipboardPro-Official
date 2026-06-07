package com.clipboardpro.share.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color

// Global state to track theme mode reactively
var isDarkThemeGlobal by mutableStateOf(true)

// Dynamic colors mapping based on dark/light theme state
val Teal400: Color get() = Color(0xFF4F46E5) // Primary Accent Indigo
val TealGlow: Color get() = if (isDarkThemeGlobal) Color(0xFF6366F1) else Color(0xFF4338CA) // Secondary Accent Glow
val Blue400: Color get() = if (isDarkThemeGlobal) Color(0xFF818CF8) else Color(0xFF6366F1) // Accent Light
val DarkBg: Color get() = if (isDarkThemeGlobal) Color(0xFF0F172A) else Color(0xFFF1F5F9) // BgDeepColor
val CardBg: Color get() = if (isDarkThemeGlobal) Color(0xFF1E293B) else Color(0xFFFFFFFF) // BgCardColor / BgSidebarColor
val SurfaceBg: Color get() = if (isDarkThemeGlobal) Color(0xFF1E293B) else Color(0xFFFFFFFF)
val ElevatedBg: Color get() = if (isDarkThemeGlobal) Color(0xFF334155) else Color(0xFFEDF2F7) // BgCardHoverColor
val TextPrimary: Color get() = if (isDarkThemeGlobal) Color(0xFFF8FAFC) else Color(0xFF0F172A) // TextPrimaryColor
val TextSecondary: Color get() = if (isDarkThemeGlobal) Color(0xFFCBD5E1) else Color(0xFF475569) // TextSecondaryColor
val TextMuted: Color get() = if (isDarkThemeGlobal) Color(0xFFA3B1C6) else Color(0xFF64748B) // TextMutedColor
val DangerRed: Color get() = Color(0xFFEF4444) // DangerColor
val SuccessGreen: Color get() = Color(0xFF10B981) // SuccessColor
val WarningAmber: Color get() = Color(0xFFF59E0B) // Amber
val BorderColor: Color get() = if (isDarkThemeGlobal) Color(0xFF2D3748) else Color(0xFFCBD5E1) // BorderColor

private val DarkColors = darkColorScheme(
    primary          = Color(0xFF4F46E5),
    onPrimary        = Color(0xFF0F172A),
    secondary        = Color(0xFF818CF8),
    onSecondary      = Color(0xFF0F172A),
    background       = Color(0xFF0F172A),
    surface          = Color(0xFF1E293B),
    surfaceVariant   = Color(0xFF334155),
    onBackground     = Color(0xFFF8FAFC),
    onSurface        = Color(0xFFF8FAFC),
    onSurfaceVariant = Color(0xFFCBD5E1),
    error            = Color(0xFFEF4444),
    tertiary         = Color(0xFF10B981),
    outline          = Color(0xFF2D3748)
)

private val LightColors = lightColorScheme(
    primary          = Color(0xFF4F46E5),
    onPrimary        = Color(0xFFFFFFFF),
    secondary        = Color(0xFF6366F1),
    onSecondary      = Color(0xFFFFFFFF),
    background       = Color(0xFFF1F5F9),
    surface          = Color(0xFFFFFFFF),
    surfaceVariant   = Color(0xFFEDF2F7),
    onBackground     = Color(0xFF0F172A),
    onSurface        = Color(0xFF0F172A),
    onSurfaceVariant = Color(0xFF475569),
    error            = Color(0xFFEF4444),
    tertiary         = Color(0xFF10B981),
    outline          = Color(0xFFCBD5E1)
)

@Composable
fun ClipboardProTheme(themeMode: String, content: @Composable () -> Unit) {
    val systemInDark = isSystemInDarkTheme()
    val isDark = when (themeMode) {
        "light" -> false
        "dark" -> true
        else -> systemInDark
    }

    SideEffect {
        isDarkThemeGlobal = isDark
    }

    MaterialTheme(
        colorScheme = if (isDark) DarkColors else LightColors,
        content = content
    )
}
