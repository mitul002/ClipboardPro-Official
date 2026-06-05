# 📋 ClipboardPro Desktop App — Detailed Features & User Benefits Guide

This guide details the complete feature set of the **ClipboardPro** Windows WPF application, showing exactly how each feature is operated from the user's end and how it benefits your day-to-day productivity.

---

### 1. Multi-Format Clipboard History Vault (Main Window)
* **How to use it from the User End**:
  1. Simply copy text, code blocks, images, links, or files as you normally would (`Ctrl + C` or right-click copy).
  2. Press `Ctrl + Alt + V` (default hotkey) to bring up the main ClipboardPro vault dashboard.
  3. Use the search bar at the top to filter items by typing keywords.
  4. Navigate the sidebar tabs (*Texts*, *Images*, *Colors*, *URLs*, and *Files*) to view categorized clipboard history.
  5. Double-click any item in the history grid to copy it back to your active clipboard and paste it.
* **How you benefit**:
  * **Zero Lost Work**: Never lose code, links, or text templates if you accidentally copy over them or shut down your PC.
  * **Frictionless Recycling**: Easily reuse standard text blocks, email drafts, server paths, and links without opening separate scratch files or scratchpads.
  * **Instant Visual Search**: Quickly find previous entries in seconds instead of scrolling through hundreds of lines of code or notepad history.

---

### 2. Cursor-Snapping Quick Paste Bar (Mini Mode)
* **How to use it from the User End**:
  1. Press `Ctrl + Shift + V` while editing or typing in any text editor.
  2. A sleek, compact horizontal bar immediately overlays next to your active mouse/text cursor showing your last 10 copied items.
  3. Use your keyboard arrow keys or mouse scroll to select the item you want and click or press **Enter** to paste it.
* **How you benefit**:
  * **Stay in Focus**: You don't have to break your concentration by opening a large dashboard window. The paste menu comes directly to your text cursor, letting you paste previous items in milliseconds.
  * **Keyboard-Only Operation**: Perfect for developers, writers, and power-users who want to maintain flow without touching the mouse.

---

### 3. Drag & Drop Temporary Shelf (macOS Style Yoink/Dropover)
* **How to use it from the User End**:
  1. **Floating Edge Dock**: A small, circular bubble floats at the edge of your screen (automatically snaps to Left or Right margins).
  2. **Drag & Stash**: Drag any selected files, text, browser images, or web links towards the bubble. The bubble instantly expands into a slide-out drawer (Temporary Shelf) with a blue count badge.
  3. **Drag & Drop Out**: Drop items onto the shelf to collect them. Later, open the shelf and drag any card out directly into other apps (e.g. dragging an image into Discord or a code file into VS Code).
  4. **Integrated Bar Shelf**: The Quick Paste Bar also has a dedicated Shelf icon where you can drop items, opening a vertical popup drawer.
* **How you benefit**:
  * **Batch Gathering**: Perfect for web research or file compiling. Instead of copy-pasting items one-by-one between folders or tabs, drag them all to the temporary shelf, then paste them where they belong in one go.
  * **Zero RAM Bloat**: The shelf only holds paths to local files, maintaining a sub-1MB footprint even if you drop a 10GB video file into it.

---

### 4. Smart Content Detection & Visual Helpers
* **How to use it from the User End**:
  * **JSON Prettifier**: Copy a messy JSON string. The Quick Paste Bar detects the format, displays a special icon, and lets you right-click and select **Structure/Prettify JSON** to clean it up with indented spacing instantly.
  * **Color Code Preview**: Copy any Hex color code (e.g., `#3498db`). ClipboardPro automatically puts a colored circle badge next to the item so you can visually confirm the color before pasting.
  * **URL Webpage Title Crawler**: Copy a link (e.g., `https://google.com`). In the background, the app crawls the page title and displays "Google" instead of a long messy URL.
  * **Sensitive Eye Masking**: Copy API keys (AWS, Stripe, OpenAI), secrets, or credit card numbers. The app flags them, obfuscates the preview, and displays a sensitive eye toggle. Click it to show/hide the plain text.
* **How you benefit**:
  * **Visual Confidence**: Instantly see what color, image, or website title you are pasting without guessing.
  * **Developer Utilities**: Format data blocks on the fly without visiting web-based JSON formatters.
  * **Privacy Protection**: Keeps passwords and credentials hidden from over-the-shoulder lookers when sharing your screen or presenting.

---

### 5. In-App Screenshot & Text Editor
* **How to use it from the User End**:
  1. Capture a screenshot or copy text to your clipboard.
  2. Right-click the item inside the ClipboardPro vault or Quick Paste Bar and select **Edit**.
  3. Use the built-in toolbar to crop the image, draw annotations, add text boxes, highlight specific areas, or directly modify copied text.
  4. Click **Save** to automatically update the clipboard item with your edits.
* **How you benefit**:
  * **Quick Annotations**: Edit screenshots, crop out unwanted details, or highlight code snippets on the fly without launching bulky external design tools or MS Paint.
  * **Dynamic Content Tweaks**: Correct typos or format copied code/text directly inside your clipboard manager before pasting it.

---

### 6. Intelligent Keyboard Text Expander
* **How to use it from the User End**:
  1. Head to the **Snippets** tab in ClipboardPro.
  2. Add a shortcut trigger abbreviation (e.g., `:email`) and the full text (e.g., `support@clipboardpro.com`).
  3. Type `:email` in any Windows application (Chrome, Outlook, Notepad, VS Code) and press **Space**, **Enter**, or **Tab**.
  4. The trigger is instantly replaced with your custom text template.
* **How you benefit**:
  * **Typing Speed Accelerator**: Speed up repetitive emails, signatures, code blocks, or database queries.
  * **Perfect Accuracy**: Avoid typos in critical inputs like URLs, bank accounts, or configuration settings.

---

### 7. Peer-to-Peer LAN Sharing
* **How to use it from the User End**:
  1. Install ClipboardPro on multiple Windows PCs connected to the same home or office Wi-Fi/LAN network.
  2. Enable LAN Sharing. The computers automatically discover each other in under 3 seconds.
  3. Copy text, links, or files on one computer, and paste them immediately on the other (`Ctrl + V`).
* **How you benefit**:
  * **Cross-PC Harmony**: No more emailing links to yourself, sending messages to empty chat channels, or uploading text to temporary cloud folders.

---

### 8. Full Database Backups & Portability
* **How to use it from the User End**:
  1. Click **Export Backup** in settings.
  2. Select a target directory to save a portable compressed `.zip` archive containing your entire history, settings, snippets, and images database.
  3. On a new machine, click **Import Backup** and select the `.zip` to restore or merge your database.
* **How you benefit**:
  * **Seamless Migrations**: Keep all your templates, shortcuts, and snippets when setting up a new workstation.

---

### 9. Local JSON Storage & SSD Offload Cache
* **How to use it from the User End**:
  1. Set your history retention limits (e.g., store last 100 items, or delete after 7 days) in settings.
  2. Pin important items to prevent them from ever being auto-cleaned.
  3. Let the app automatically run JSON database cleanup and image cache pruning routines in the background to compact storage.
* **How you benefit**:
  * **Always Fast**: Native WPF/C# execution keeps the RAM usage under 10MB, ensuring 0% performance impact on your PC.
  * **Offline Reliability**: All copied items are saved locally on your SSD so your history is always available.
