# 📋 ClipboardPro Next.js Landing Page Copy & Rebrand Checklist

This checklist outlines the absolute plan to copy the Next.js landing page from **OrbitSwipe** to **ClipboardPro**, preserving the high-fidelity premium radial launcher dials, floating particles, glass presets, and micro-animations, while fully adapting them to showcase **ClipboardPro's Zero-Trust Clipboard Sync** features and custom branding.

---

## 🎨 Phase 1: Global Branding & Color Accent Adaptation
- [x] **1. Logo Copying & Path Wiring:**
  - Copy the original premium logo file from `d:\SAAS PROJECTS\ClipboardPro\logo.png` to `website/public/logo.png`.
  - Update layout files, navbar, footer, and favicons to display `/logo.png`.
- [x] **2. Theme & Accent Alignment:**
  - Map colors to ClipboardPro's `DarkTheme.xaml` design tokens:
    - **Backgrounds:** Slate-950/Slate-900 (`#0B0F19` to `#0F172A`)
    - **Primary Accent:** Indigo-500 (`#6366F1`) & Indigo-600 (`#4F46E5`)
    - **Secondary Accent:** Violet-500 (`#7C3AED` to `#818CF8`)
    - **Success/Sync State:** Emerald-500 (`#10B981`)
  - Update `globals.css` with custom slate/indigo gradients and shadows.
  - Rebrand radial gradient overlay colors, cursor-glow spheres, and particle effects.

---

## 🏗️ Phase 2: Component-by-Component Copy & Adapt
Copy files directly from `D:\SAAS PROJECTS\OrbitSwipe\orbitswipe MODULER\orbitswipe-web\website\components\sections\` to `d:\SAAS PROJECTS\ClipboardPro\license-clipboardpro\website\components\sections\`, then modify them.

- [x] **1. Navigation Bar (`navbar.tsx`)**:
  - Rebrand text "OrbitSwipe" to "ClipboardPro".
  - Hook `/logo.png` image with a sleek, glowing hover effect.
  - Set active scroll navigation triggers: Features, Security, Specs, Pricing, Testimonials.
  - Connect CTAs: "Admin Dashboard 🔒" to `clipboardpro.vercel.app/admin-dashboard` and "Download Client" to trigger the Windows installer download.
- [x] **2. Hero Section & Sync Wheel (`hero-section.tsx`)**:
  - **Rebrand the gorgeous 180° radial launcher** into the **ClipboardPro Zero-Trust Cryptographic Radial Hub**:
    - **Wheel Tabs:** Re-label categories from OrbitSwipe's "Recents", "Favorites", "Toolbox" to:
      - `Texts` (Copied code blocks, snippets, formatted values)
      - `Devices` (Connected clients: Workstation-PC, Laptop-Home, Server-Cloud)
      - `Security` (Sync configuration, HWID encryption keys, regex masking filters)
    - **Wheel Items:**
      - Populate `Texts` with clipboard emojis: 📋 (Text), 💻 (Code), 🔑 (Credentials), 🌐 (URLs), 📊 (JSON).
      - Populate `Devices` with device nodes: 🖥️ (Workstation), 💻 (Laptop), 💾 (Cloud SQLite), 📱 (Mobile Sync).
      - Populate `Security` with modular items: 🔒 (AES-256), 🔑 (HWID Sign), 🛡️ (Regex Redact), 🗑️ (Clear Cache).
    - **Hub Center Mode Toggles:**
      - **System Stats Mode:** Retain the clock, and map stats to ClipboardPro performance values: RAM (`8 MB`), CPU (`0.1%`), Sync Buffer (`Active`), Network latency (`<100ms`).
      - **Cryptographic Mode:** Replace the media player mode with a "Cipher Visualizer" showing synced packets, encryption status, and animated spectrum visualizers mapped to simulated secure socket transmission frequencies!
      - **Theme Presets:** Re-color the 10 presets into Slate Obsidian, Arctic Indigo, Neon Shield, Emerald Cache, and Sapphire Core.
  - Headline: **"Secure Clipboard Mirroring"** with gradient subtitle: **"in One Fast Sync."**
  - Subtitle: **"A highly optimized C# WPF desktop suite and Firestore peer-to-peer sync engine that mirrors your clipboard history across Windows devices in under 100ms under a zero-trust model."**
- [x] **3. Why Clipboard Pro (`why-section.tsx`)**:
  - Adapt the "Built Different" layout:
    - *Zero Cleartext Transfer*: AES-GCM 256 client-side payload encryption.
    - *HWID Hardware Lock*: Secure binding to motherboard and CPU physical signatures.
    - *Native WPF Engine*: Under 10MB RAM footprint. 0% Electron bloat.
    - *Offline-First DB*: Fully-encrypted SQLite cache keeps history indexed locally.
- [x] **4. Features Grid (`features-section.tsx`)**:
  - Retain the responsive grid, mapping all cards to ClipboardPro capabilities:
    1. **AES-GCM-256 Cryptography** (Encrypted before sync).
    2. **Motherboard HWID Signatures** (Session binding).
    3. **Smart RegEx Redactors** (Auto-masks credentials).
    4. **Offline SQLite Engine** (Local database).
    5. **Ultra-Low Memory (WPF)** (<10MB RAM).
    6. **High-Speed Socket Mirroring** (<100ms peer mirror).
    7. **Custom Accelerator Bindings** (Hotkeys).
    8. **JSON & Rich-Text Support** (Advanced formats).
    9. **Licensing Console Integration** (Firebase admin).
- [x] **5. Cryptographic Flow Section (`advanced-section.tsx`)**:
  - Re-engineer the animated flow to reflect Zero-Trust packet mirroring:
    - *Client Capture* -> *AES Encryption* -> *HWID Token Signature* -> *Firestore Socket Sync* -> *Remote Decryption Verification*.
- [x] **6. UI Section settings dashboard (`ui-section.tsx`)**:
  - Keep the second radial interface completely intact but map its controls, labels, and toggles to ClipboardPro setting panels:
    - Sliders for local memory limit buffer (5MB to 50MB).
    - Toggle switches for: HWID Authentication, Encrypted Cache, Regex Password Masking, Hotkey Triggers.
- [x] **7. Technical Specs (`specs-section.tsx`)**:
  - Re-brand specs to showcase WPF & .NET 8.0 architecture details: Memory consumption, Sync intervals, SQLite parameters, and installation bundle sizes.
- [x] **8. Pricing Section & License Key Generator (`pricing-section.tsx`)**:
  - Adapt pricing cards to ClipboardPro tiers ($0 Trial, $9/mo, $59/yr, $49 Lifetime).
  - **Maintain complete Firestore API Trial key generation logic** that hits the `/api/create-trial` backend and generates/displays click-to-copy trial licenses.
- [x] **9. Download CTA & Footer (`download-section.tsx` & `footer.tsx`)**:
  - Point setup downloads to `/ClipboardPro-Setup.exe`.
  - Rebrand footer copy to highlight ClipboardPro Secure Sync Systems.

---

## 🚀 Phase 3: Build & Validation
- [x] Run a local production build inside `website/` to verify flawless compilation.
- [x] Spin up `npm run dev` to verify the interactive wheels and visuals.
