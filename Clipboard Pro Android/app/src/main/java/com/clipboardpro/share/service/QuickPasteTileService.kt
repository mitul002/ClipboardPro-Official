package com.clipboardpro.share.service

import android.app.Dialog
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.service.quicksettings.TileService
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.Window
import android.view.WindowManager
import android.widget.BaseAdapter
import android.widget.LinearLayout
import android.widget.ListView
import android.widget.TextView
import android.widget.Toast
import com.clipboardpro.share.data.AppDatabase
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class QuickPasteTileService : TileService() {
    private val job = SupervisorJob()
    private val scope = CoroutineScope(Dispatchers.Main + job)

    override fun onClick() {
        super.onClick()
        scope.launch {
            val db = AppDatabase.getDatabase(applicationContext)
            val items = withContext(Dispatchers.IO) {
                db.clipboardDao().getAllItems().take(5)
            }
            if (items.isEmpty()) {
                Toast.makeText(applicationContext, "Clipboard history is empty", Toast.LENGTH_SHORT).show()
                return@launch
            }
            showQuickPasteDialog(items.map { it.content })
        }
    }

    private fun showQuickPasteDialog(items: List<String>) {
        val dialog = Dialog(this)
        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE)

        // Fluent Dark background container
        val container = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            padding = 16
            background = GradientDrawable().apply {
                setColor(Color.parseColor("#0F172A")) // DarkBg
                cornerRadius = 24f
                setStroke(2, Color.parseColor("#2D3748")) // BorderColor
            }
            layoutParams = ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT
            )
        }

        // Title
        val titleView = TextView(this).apply {
            text = "QUICK VAULT"
            setTextColor(Color.parseColor("#6366F1")) // Accent Indigo
            textSize = 12f
            typeface = android.graphics.Typeface.DEFAULT_BOLD
            gravity = Gravity.CENTER_HORIZONTAL
            setPadding(0, 16, 0, 16)
        }
        container.addView(titleView)

        // List
        val listView = ListView(this).apply {
            divider = GradientDrawable().apply {
                setColor(Color.parseColor("#2D3748"))
                setSize(ViewGroup.LayoutParams.MATCH_PARENT, 1)
            }
            dividerHeight = 1
            adapter = object : BaseAdapter() {
                override fun getCount(): Int = items.size
                override fun getItem(position: Int): Any = items[position]
                override fun getItemId(position: Int): Long = position.toLong()
                override fun getView(position: Int, convertView: View?, parent: ViewGroup?): View {
                    val tv = (convertView as? TextView) ?: TextView(this@QuickPasteTileService).apply {
                        setTextColor(Color.WHITE)
                        textSize = 14f
                        setPadding(32, 24, 32, 24)
                        maxLines = 2
                        ellipsize = android.text.TextUtils.TruncateAt.END
                        setBackgroundColor(Color.parseColor("#1E293B")) // CardBg
                    }
                    tv.text = items[position]
                    return tv
                }
            }
            setOnItemClickListener { _, _, position, _ ->
                val text = items[position]
                val cb = getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                cb.setPrimaryClip(ClipData.newPlainText("Quick Paste", text))
                Toast.makeText(applicationContext, "Copied to active clip", Toast.LENGTH_SHORT).show()
                dialog.dismiss()
            }
        }
        
        container.addView(listView)
        dialog.setContentView(container)

        // Set window attributes to overlay correctly
        dialog.window?.let { window ->
            window.setBackgroundDrawableResource(android.R.color.transparent)
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                window.setType(WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY)
            }
        }

        showDialog(dialog)
    }

    override fun onDestroy() {
        job.cancel()
        super.onDestroy()
    }
}
