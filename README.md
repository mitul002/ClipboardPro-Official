# 📋 ClipboardPro Official

[![Windows Version](https://img.shields.io/badge/Windows-10%20%2F%2011-blue.svg?style=flat-square&logo=windows)](https://clipboardpro.vercel.app)
[![Release Version](https://img.shields.io/github/v/release/mitul002/ClipboardPro-Official?style=flat-square&logo=github)](https://github.com/mitul002/ClipboardPro-Official/releases/latest)
[![Official Website](https://img.shields.io/badge/Website-clipboardpro.vercel.app-indigo.svg?style=flat-square)](https://clipboardpro.vercel.app)
[![Buy License](https://img.shields.io/badge/Purchase-Lemon%20Squeezy-orange.svg?style=flat-square)](https://crosstech.lemonsqueezy.com/checkout/buy/0e946ba1-a181-4747-a3ee-3719a41cbbb0?enabled=1716270)

**ClipboardPro** is a premium, high-performance native Windows clipboard manager and productivity suite. Engineered in native C# / WPF for maximum speed and sub-10MB RAM efficiency, it features an intelligent multi-format history vault, macOS-style drag-and-drop temporary shelf, automatic content formatters, and secure LAN synchronization.

---

## ⚡ Direct Download & Shop Links

* 📥 **[Download Latest Installer (.exe)](https://github.com/mitul002/ClipboardPro-Official/releases/download/v1.4.0/ClipboardPro-Setup.exe)**
* 🌐 **[Official Website & Demo](https://clipboardpro.vercel.app)**
* 🛒 **[Purchase Yearly Pro License ($9.99/Year)](https://crosstech.lemonsqueezy.com/checkout/buy/0e946ba1-a181-4747-a3ee-3719a41cbbb0?enabled=1716270)**
* 🛒 **[Purchase Lifetime Pro License ($19.99 One-Time)](https://crosstech.lemonsqueezy.com/checkout/buy/b6bafa95-126c-4c5f-82ab-4b0d4a3f5e7f?enabled=1716321)**

---

## 🚀 Detailed Feature Walkthrough & User Operations

### 1. 🗄️ Multi-Format History Vault (Main Window)
* **How to use it**:
  1. Simply copy text, code blocks, images, links, or files as you normally would (`Ctrl + C` or right-click copy).
  2. Press `Ctrl + Alt + V` (default hotkey) to bring up the main ClipboardPro vault dashboard.
  3. Use the search bar at the top to filter items by typing keywords.
  4. Navigate the sidebar tabs (*Texts*, *Images*, *Colors*, *URLs*, and *Files*) to view categorized clipboard history.
  5. Double-click any item in the history grid to copy it back to your active clipboard and paste it.
* **Key Benefits**:
  * **Zero Lost Work:** Never lose code, links, or text templates if you accidentally copy over them or shut down your PC.
  * **Frictionless Recycling:** Reuse standard text blocks, email drafts, server paths, and links without opening separate scratch files.

### 2. ⚡ Cursor-Snapping Quick Paste Bar (Mini Mode)
* **How to use it**:
  1. Press `Ctrl + Shift + V` while editing or typing in any text editor.
  2. A sleek, compact horizontal bar immediately overlays next to your active mouse/text cursor showing your last 10 copied items.
  3. Use your keyboard arrow keys or mouse scroll to select the item you want and click or press **Enter** to paste it.
* **Key Benefits**:
  * **Stay in Focus:** You don't have to break your concentration by opening a large dashboard window. The paste menu comes directly to your text cursor, letting you paste previous items in milliseconds.

### 3. 🗃️ Drag & Drop Temporary Shelf (macOS Style Yoink/Dropover)
* **How to use it**:
  1. **Floating Edge Dock:** A small, circular bubble floats at the edge of your screen (automatically snaps to Left or Right margins).
  2. **Drag & Stash:** Drag any selected files, text, browser images, or web links towards the bubble. The bubble instantly expands into a slide-out drawer (Temporary Shelf) with a blue count badge.
  3. **Drag & Drop Out:** Drop items onto the shelf to collect them. Later, open the shelf and drag any card out directly into other apps (e.g. dragging an image into Discord or a code file into VS Code).
  4. **Integrated Bar Shelf:** The Quick Paste Bar also has a dedicated Shelf icon where you can drop items, opening a vertical popup drawer.
* **Key Benefits**:
  * **Batch Gathering:** Perfect for web research or file compiling. Instead of copy-pasting items one-by-one, drag them all to the temporary shelf, then paste them where they belong in one go.

### 4. 🧠 Smart Content Detection & Visual Helpers
* **JSON Prettifier:** Copy a messy JSON string. The Quick Paste Bar detects the format, displays a special icon, and lets you right-click and select **Structure/Prettify JSON** to clean it up with indented spacing instantly.
* **Color Code Preview:** Copy any Hex color code (e.g., `#3498db`). ClipboardPro automatically puts a colored circle badge next to the item so you can visually confirm the color before pasting.
* **URL Webpage Title Crawler:** Copy a link (e.g., `https://google.com`). In the background, the app crawls the page title and displays "Google" instead of a long messy URL.
* **Sensitive Eye Masking:** Copy API keys (AWS, Stripe, OpenAI), secrets, or credit card numbers. The app flags them, obfuscates the preview, and displays a sensitive eye toggle. Click it to show/hide the plain text.

### 5. 🎨 In-App Screenshot & Text Editor
* **How to use it**:
  1. Capture a screenshot or copy text to your clipboard.
  2. Right-click the item inside the ClipboardPro vault or Quick Paste Bar and select **Edit**.
  3. Use the built-in toolbar to crop the image, draw annotations, add text boxes, highlight specific areas, or directly modify copied text.
  4. Click **Save** to automatically update the clipboard item with your edits.

### 6. 🚀 Intelligent Keyboard Text Expander
* **How to use it**:
  1. Head to the **Snippets** tab (Expender) in ClipboardPro.
  2. Add a shortcut trigger abbreviation (e.g., `:email`) and the full text (e.g., `support@clipboardpro.com`).
  3. Type `:email` in any Windows application (Chrome, Outlook, Notepad, VS Code) and press **Space**, **Enter**, or **Tab**.
  4. The trigger is instantly replaced with your custom text template.

### 7. 🌐 Peer-to-Peer LAN Sharing
* **How to use it**:
  1. Install ClipboardPro on multiple Windows PCs connected to the same home or office Wi-Fi/LAN network.
  2. Enable LAN Sharing. The computers automatically discover each other in under 3 seconds.
  3. Copy text, links, or files on one computer, and paste them immediately on the other (`Ctrl + V`).

---

## 💾 Installation Options

### Method 1: Direct Executable Installer
1. Download `ClipboardPro-Setup.exe` from the **[Releases Page](https://github.com/mitul002/ClipboardPro-Official/releases/latest)**.
2. Double-click the installer, click **Install**, and the app will launch instantly in the system tray.

### Method 2: Windows Package Manager (Winget)
```cmd
winget install CrossTech.ClipboardPro
```

### Method 3: Chocolatey
```cmd
choco install clipboardpro
```

---

## 🔒 Privacy & Local Security
* **100% Offline:** All history databases and images are saved locally on your SSD cache.
* **No Telemetry:** We do not track, collect, or share your clipboard contents or personal data.

*Developed by **Cross Tech** by Magnetieght EU.*
