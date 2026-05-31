"use client"

import { useState } from "react"
import { motion, AnimatePresence } from "framer-motion"
import { Database, Keyboard, Layers, Share2, Sliders } from "lucide-react"

const SCREENSHOTS = [
  {
    id: "vault",
    title: "WPF Clipboard Vault",
    subtitle: "Local Database & Search",
    image: "/Screenshots/clipboard dark.png",
    icon: Database,
    color: "#6366f1",
    desc: "Query, sort, and search your clipboard history instantly. The WPF client automatically categorizes items into clear tabs for texts, high-res images, files, hex color codes, and web links.",
    highlights: ["Fluent Dark/Light UI layout", "Transparent Glass theme in Light & Dark Mode", "Full-text fuzzy search bar", "Instant classification tabs", "Lightweight JSON & SSD cache"],
  },
  {
    id: "expander",
    title: "Keyboard Text Expander",
    subtitle: "Custom Trigger expansion",
    image: "/Screenshots/Text Expender Dark.png",
    icon: Keyboard,
    color: "#a78bfa",
    desc: "Assign custom keyboard triggers to instantly paste long emails, signatures, or templates. A low-level keyboard hook processes expansions on space, enter, or tab globally in any app.",
    highlights: ["Low-latency trigger hooks", "Universal expansion injection", "Clean template editor", "Preserves copy history state"],
  },
  {
    id: "shelf",
    title: "QuickDrop Shelf",
    subtitle: "Screen-Edge Drag & Drop",
    image: "/Screenshots/Temporary Shelf Dark.png",
    icon: Layers,
    color: "#10b981",
    desc: "Drag files, text, or links to the screen margin to drop them into a temporary shelf. Hover to slide out the shelf and drag items back out to move them in batches.",
    highlights: ["Stealth screen-edge dock", "Direct drag & drop out", "Thumbnails & file sizes", "Ultra-low memory usage"],
  },
  {
    id: "share",
    title: "Local LAN Sync",
    subtitle: "High-Speed Cross-PC Mirroring",
    image: "/Screenshots/Local Share Dark.png",
    icon: Share2,
    color: "#38bdf8",
    desc: "Mirror your clipboard across all your computers on the same network automatically. Copy on your desktop workstation and paste instantly on your laptop.",
    highlights: ["Instant auto-discovery", "Under 100ms local transmission", "No cloud or internet needed", "Device list status indicator"],
  },
  {
    id: "settings",
    title: "Settings & Tuning",
    subtitle: "Optimization & Hotkeys",
    image: "/Screenshots/Settings Dark.png",
    icon: Sliders,
    color: "#fb923c",
    desc: "Configure global hotkey shortcuts, adjust history storage caps, run JSON cleanup and prune image caches, and set custom rules to customize the clipboard dashboard behavior.",
    highlights: ["Custom global hotkeys", "JSON & Cache auto-clean", "Adjust retention limits", "Toggle smart detections"],
  },
]

export function ScreenshotsSection() {
  const [activeTab, setActiveTab] = useState(0)
  const current = SCREENSHOTS[activeTab]
  const Icon = current.icon

  return (
    <section id="screenshots" className="relative py-24 px-4 overflow-hidden">
      {/* Subtle grid accent */}
      <div 
        className="absolute inset-0 opacity-5 pointer-events-none"
        style={{
          backgroundImage: `radial-gradient(circle, rgba(99,102,241,0.15) 1px, transparent 1.5px)`,
          backgroundSize: "40px 40px",
        }}
      />

      <div className="relative z-10 max-w-7xl mx-auto">
        
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="text-center mb-16"
        >
          <span className="inline-block px-4 py-1.5 rounded-full bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 text-xs font-semibold uppercase tracking-wider mb-4">
            Product Showcase
          </span>
          <h2 className="text-4xl md:text-5xl font-extrabold mb-4 text-white">
            Actual Client{" "}
            <span className="bg-gradient-to-r from-indigo-400 via-violet-300 to-indigo-500 bg-clip-text text-transparent">
              UI
            </span>
          </h2>
          <p className="text-indigo-200/50 max-w-2xl mx-auto font-light text-base">
            Take a look at the real high-fidelity WPF Fluent interface running live on Windows 10/11.
          </p>
        </motion.div>

        {/* Dynamic Navigation Tabs */}
        <div className="flex flex-wrap justify-center gap-3 mb-12">
          {SCREENSHOTS.map((screen, idx) => {
            const isActive = activeTab === idx
            const TabIcon = screen.icon
            return (
              <button
                key={screen.id}
                onClick={() => setActiveTab(idx)}
                className={`flex items-center gap-2 px-5 py-3 rounded-full text-xs font-bold uppercase tracking-wider transition-all duration-300 border ${
                  isActive 
                    ? "text-white shadow-lg" 
                    : "text-indigo-300/60 border-white/5 bg-slate-900/30 hover:border-indigo-500/20 hover:text-indigo-300"
                }`}
                style={{
                  backgroundColor: isActive ? `${screen.color}20` : "transparent",
                  borderColor: isActive ? screen.color : "transparent",
                  boxShadow: isActive ? `0 0 20px ${screen.color}30` : "none",
                }}
              >
                <TabIcon className="w-4 h-4 shrink-0" style={{ color: screen.color }} />
                <span>{screen.title}</span>
              </button>
            )
          })}
        </div>

        {/* Large Layout Presentation Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
          
          {/* Left Column: High-Fidelity Screenshot Image */}
          <div className="lg:col-span-6 w-full flex justify-center">
            <div className="relative w-full max-w-[540px] lg:max-w-full group">
              {/* Animated backdrop light behind screenshot */}
              <AnimatePresence mode="wait">
                <motion.div
                  key={current.id}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 0.35 }}
                  exit={{ opacity: 0 }}
                  className="absolute -inset-2 rounded-[32px] blur-2xl transition duration-500 pointer-events-none"
                  style={{ background: current.color }}
                />
              </AnimatePresence>

              {/* Screenshot frame */}
              <div className="relative rounded-[32px] p-2 bg-slate-950/70 border border-white/10 backdrop-blur-2xl shadow-2xl overflow-hidden">
                <AnimatePresence mode="wait">
                  <motion.img
                    key={current.id}
                    src={current.image}
                    alt={current.title}
                    initial={{ opacity: 0, scale: 0.98 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.98 }}
                    transition={{ duration: 0.4 }}
                    className="w-full rounded-[24px] border border-white/5 select-none shadow-inner"
                  />
                </AnimatePresence>
              </div>
            </div>
          </div>

          {/* Right Column: Detailed Screenshot Specification */}
          <div className="lg:col-span-6 flex flex-col justify-center items-start text-left">
            <AnimatePresence mode="wait">
              <motion.div
                key={current.id}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -20 }}
                transition={{ duration: 0.4 }}
                className="w-full"
              >
                <div className="flex items-center gap-3 mb-4">
                  <div 
                    className="p-3 rounded-2xl border"
                    style={{ 
                      backgroundColor: `${current.color}15`, 
                      borderColor: `${current.color}35` 
                    }}
                  >
                    <Icon className="w-6 h-6" style={{ color: current.color }} />
                  </div>
                  <div>
                    <h3 className="text-2xl font-extrabold text-white">{current.title}</h3>
                    <p className="text-xs uppercase tracking-wider font-bold" style={{ color: current.color }}>
                      {current.subtitle}
                    </p>
                  </div>
                </div>

                <p className="text-indigo-200/70 text-sm leading-relaxed mb-8 font-light">
                  {current.desc}
                </p>

                {/* Highlights List */}
                <div className="space-y-3">
                  <span className="text-[10px] uppercase tracking-widest font-extrabold text-indigo-300/40">
                    Feature Highlights
                  </span>
                  {current.highlights.map((highlight, idx) => (
                    <div key={idx} className="flex items-center gap-2.5 text-xs text-indigo-200/80 font-medium">
                      <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ backgroundColor: current.color }} />
                      <span>{highlight}</span>
                    </div>
                  ))}
                </div>
              </motion.div>
            </AnimatePresence>
          </div>

        </div>

      </div>
    </section>
  )
}

