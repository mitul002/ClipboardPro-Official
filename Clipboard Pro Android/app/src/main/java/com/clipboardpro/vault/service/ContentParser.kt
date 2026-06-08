package com.clipboardpro.vault.service

import com.clipboardpro.vault.model.ClipboardItemType
import java.util.regex.Pattern

object ContentParser {

    private val urlPattern = Pattern.compile(
        "^(https?://|www\\.)[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,15}(/\\S*)?\$|^[a-zA-Z0-9.-]+\\.(com|net|org|edu|gov|io|ai|me|info|sh|app|dev|xyz|so|online|site|tech)\\b(/\\S*)?\$",
        Pattern.CASE_INSENSITIVE
    )

    private val emailPattern = Pattern.compile(
        "^[\\w\\.-]+@[\\w\\.-]+\\.\\w{2,}\$",
        Pattern.CASE_INSENSITIVE
    )

    private val phonePattern = Pattern.compile(
        "^(\\+?\\d{1,3}[-.\\s]?)?\\(?\\d{3}\\)?[-.\\s]?\\d{3}[-.\\s]?\\d{4,9}\$"
    )

    private val codePattern = Pattern.compile(
        "(^[\\s\\r\\n]*(def |import |from |function |var |const |let |class |public |private |protected |internal |namespace |using |using static |#include |#define |#if |#endif |extern |SELECT |INSERT |UPDATE |DELETE |CREATE |ALTER |DROP |GRANT |REVOKE ))|(\\{[\\s\\r\\n]*[\"'][^\"']+[\"'][\\s\\r\\n]*:)|(<(html|div|script|style|body|head|span|p|a|ul|li|table|tr|td|img|form|input|button|link|meta|iframe))|(\\b(if|for|while|foreach|switch|try|catch|finally)\\s*\\(.*\\)\\s*\\{)|(\\b(bool|int|string|var|float|double|char|long|byte|decimal)\\s+[a-zA-Z_]\\w*\\s*[=;])",
        Pattern.CASE_INSENSITIVE
    )

    private val jsonPattern = Pattern.compile(
        "^[\\s\\r\\n]*[\\{\\[]"
    )

    private val hexColorPattern = Pattern.compile(
        "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})\$"
    )

    private val pathPattern = Pattern.compile(
        "(^[a-zA-Z]:\\\\.*)|(^/([^/\\0]+/)+[^/\\0]*)|(^[\\\\/]{2}[^/]+/.*)"
    )

    fun detectType(text: String): ClipboardItemType {
        if (text.isBlank()) return ClipboardItemType.TEXT

        val trimmed = text.trim().removeSurrounding("\"")

        // 1. High-priority strict matches
        if (urlPattern.matcher(trimmed).matches()) return ClipboardItemType.URL
        if (emailPattern.matcher(trimmed).matches()) return ClipboardItemType.EMAIL

        // 2. Phone numbers
        if (phonePattern.matcher(trimmed).matches()) return ClipboardItemType.PHONE

        // 3. Colors
        if (hexColorPattern.matcher(trimmed).matches()) return ClipboardItemType.COLOR

        // 4. Code / JSON detection
        if (trimmed.length > 5) {
            if (jsonPattern.matcher(trimmed).find()) {
                if (trimmed.length < 50 && (trimmed.startsWith("[") || trimmed.startsWith("{"))) {
                    try {
                        // Basic validation
                        org.json.JSONObject(trimmed)
                        return ClipboardItemType.CODE
                    } catch (e: Exception) {
                        try {
                            org.json.JSONArray(trimmed)
                            return ClipboardItemType.CODE
                        } catch (e2: Exception) {
                            // Fall through
                        }
                    }
                } else if (trimmed.length >= 50) {
                    return ClipboardItemType.CODE
                }
            }
            if (codePattern.matcher(trimmed).find()) return ClipboardItemType.CODE
        }

        // 5. Paths
        if (pathPattern.matcher(trimmed).matches()) {
            return ClipboardItemType.PATH
        }

        return ClipboardItemType.TEXT
    }

    fun isSensitive(text: String): Boolean {
        val trimmed = text.trim()
        val ccRegex = Regex("\\b(?:\\d{4}[ -]?\\d{4}[ -]?\\d{4}[ -]?\\d{4}|\\d{4}[ -]?\\d{6}[ -]?\\d{5}|\\d{4}[ -]?\\d{6}[ -]?\\d{4}|\\d{13,16})\\b")
        val apiRegex1 = Regex("\\b(sk|pk|ak|uk)_(?:live|test|prod)_[a-zA-Z0-9]{20,}\\b", RegexOption.IGNORE_CASE)
        val apiRegex2 = Regex("\\b(AKIA|ASIA)[0-9A-Z]{16}\\b")
        val apiRegex3 = Regex("\\b(AIza[0-9A-Za-z-_]{35})\\b")
        val apiRegex4 = Regex("\\b[a-fA-F0-9]{32,64}\\b")
        val apiRegex5 = Regex("\\b((?:sk|pk|secret|key|auth|api|token)[-_a-zA-Z0-9]*[:=][\\s]*[a-zA-Z0-9]{12,})\\b", RegexOption.IGNORE_CASE)

        return ccRegex.containsMatchIn(trimmed) ||
                apiRegex1.containsMatchIn(trimmed) ||
                apiRegex2.containsMatchIn(trimmed) ||
                apiRegex3.containsMatchIn(trimmed) ||
                apiRegex4.containsMatchIn(trimmed) ||
                apiRegex5.containsMatchIn(trimmed)
    }
}
