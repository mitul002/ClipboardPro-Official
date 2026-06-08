package com.clipboardpro.vault.ui.theme

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

// ─────────────────────────────────────────────────────────────────────────────
// Global reactive theme flag
// ─────────────────────────────────────────────────────────────────────────────
var isDarkThemeGlobal by mutableStateOf(true)

// ─────────────────────────────────────────────────────────────────────────────
// DARK PALETTE (unchanged – deep navy / slate)
// ─────────────────────────────────────────────────────────────────────────────
private object Dark {
    val Bg       = Color(0xFF0F172A) // Very deep navy
    val Card     = Color(0xFF1E293B) // Slate-800
    val Elevated = Color(0xFF334155) // Slate-700
    val Border   = Color(0xFF2D3748) // Slate border
    val TxtPri   = Color(0xFFF8FAFC) // Near white
    val TxtSec   = Color(0xFFCBD5E1) // Slate-300
    val TxtMuted = Color(0xFFA3B1C6) // Slate-400
}

// ─────────────────────────────────────────────────────────────────────────────
// LIGHT PALETTE – clean white / light-gray product look
// All text and icons are explicitly dark so they are ALWAYS visible.
// ─────────────────────────────────────────────────────────────────────────────
private object Light {
    val Bg       = Color(0xFFF4F6FA) // Off-white page background
    val Card     = Color(0xFFFFFFFF) // Pure white cards
    val Elevated = Color(0xFFE8EDF5) // Slightly gray for elevated surfaces
    val Border   = Color(0xFFD1D9E6) // Subtle cool-gray borders
    val TxtPri   = Color(0xFF0F172A) // Near-black – always readable on white
    val TxtSec   = Color(0xFF374151) // Dark gray
    val TxtMuted = Color(0xFF6B7280) // Medium gray
}

// ─────────────────────────────────────────────────────────────────────────────
// Shared accent / status colours – identical in both themes for brand consistency
// ─────────────────────────────────────────────────────────────────────────────
val AccentIndigo   = Color(0xFF4F46E5)
val AccentIndigoLt = Color(0xFF6366F1)
val AccentBlue     = Color(0xFF818CF8)
val DangerRed      = Color(0xFFEF4444)
val SuccessGreen   = Color(0xFF10B981)
val WarningAmber   = Color(0xFFF59E0B)

// ─────────────────────────────────────────────────────────────────────────────
// Dynamic surface tokens (read by all composables)
// ─────────────────────────────────────────────────────────────────────────────
val Teal400: Color  get() = AccentIndigo
val TealGlow: Color get() = if (isDarkThemeGlobal) AccentIndigoLt else AccentIndigo
val Blue400: Color  get() = if (isDarkThemeGlobal) AccentBlue else AccentIndigo

val DarkBg:    Color get() = if (isDarkThemeGlobal) Dark.Bg       else Light.Bg
val CardBg:    Color get() = if (isDarkThemeGlobal) Dark.Card     else Light.Card
val SurfaceBg: Color get() = if (isDarkThemeGlobal) Dark.Card     else Light.Card
val ElevatedBg:Color get() = if (isDarkThemeGlobal) Dark.Elevated else Light.Elevated
val BorderColor:Color get()= if (isDarkThemeGlobal) Dark.Border   else Light.Border

val TextPrimary:   Color get() = if (isDarkThemeGlobal) Dark.TxtPri   else Light.TxtPri
val TextSecondary: Color get() = if (isDarkThemeGlobal) Dark.TxtSec   else Light.TxtSec
val TextMuted:     Color get() = if (isDarkThemeGlobal) Dark.TxtMuted else Light.TxtMuted

// ─────────────────────────────────────────────────────────────────────────────
// Material 3 colour schemes
// ─────────────────────────────────────────────────────────────────────────────
private val DarkColors = darkColorScheme(
    primary          = AccentIndigo,
    onPrimary        = Color.White,
    primaryContainer = Color(0xFF312E81),
    onPrimaryContainer = Color(0xFFE0E7FF),
    secondary        = AccentIndigoLt,
    onSecondary      = Color.White,
    background       = Dark.Bg,
    surface          = Dark.Card,
    surfaceVariant   = Dark.Elevated,
    onBackground     = Dark.TxtPri,
    onSurface        = Dark.TxtPri,
    onSurfaceVariant = Dark.TxtSec,
    error            = DangerRed,
    tertiary         = SuccessGreen,
    outline          = Dark.Border
)

private val LightColors = lightColorScheme(
    primary          = AccentIndigo,
    onPrimary        = Color.White,
    primaryContainer = Color(0xFFEEF2FF),
    onPrimaryContainer = Color(0xFF312E81),
    secondary        = AccentIndigoLt,
    onSecondary      = Color.White,
    background       = Light.Bg,
    surface          = Light.Card,
    surfaceVariant   = Light.Elevated,
    onBackground     = Light.TxtPri,
    onSurface        = Light.TxtPri,
    onSurfaceVariant = Light.TxtSec,
    error            = DangerRed,
    tertiary         = SuccessGreen,
    outline          = Light.Border
)

// ─────────────────────────────────────────────────────────────────────────────
// Theme composable
// ─────────────────────────────────────────────────────────────────────────────
@Composable
fun ClipboardProTheme(themeMode: String, content: @Composable () -> Unit) {
    val systemInDark = isSystemInDarkTheme()
    val isDark = when (themeMode) {
        "light" -> false
        "dark"  -> true
        else    -> systemInDark
    }

    isDarkThemeGlobal = isDark

    MaterialTheme(
        colorScheme = if (isDark) DarkColors else LightColors,
        content = content
    )
}
