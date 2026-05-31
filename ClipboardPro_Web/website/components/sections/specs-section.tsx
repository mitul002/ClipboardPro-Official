"use client"

import { motion } from "framer-motion"
import { 
  Check,
  Crop,
  Share2,
  Sparkles,
  Keyboard,
  Copy,
  Download,
  Folder,
  Layers,
  Zap,
  Key
} from "lucide-react"

const specs = [
  { icon: Copy, label: "Organize Text", value: "Smart Copy" },
  { icon: Download, label: "Instant Download", value: "Save Images" },
  { icon: Folder, label: "Custom Grouping", value: "Categories" },
  { icon: Crop, label: "Text & Image", value: "Quick Edit" },
  { icon: Share2, label: "Instant Beam", value: "LAN Share" },
  { icon: Keyboard, label: "Quick Cursor", value: "Paste Bar" },
  { icon: Layers, label: "Temporary Holder", value: "Temp Shelf" },
  { icon: Zap, label: "Text Snippets", value: "Expander" },
  { icon: Key, label: "Shortcut Keys", value: "Hotkeys" },
]

const requirements = [
  { title: "Copy and organize text smartly", desc: "Auto-detects hex colors, formats messy JSON data, and fetches webpage titles." },
  { title: "Instant download image", desc: "Save and extract image files directly from webpages." },
  { title: "Custom Category", desc: "Keep your pinned snippets organized in custom folders.." },
  { title: "Instant Edit Text and picture", desc: "Crop and draw on screenshots or modify copied text before pasting." },
  { title: "Instant Share", desc: "Beam clipboard content peer-to-peer over local network." },
  { title: "Quick paste bar", desc: "Snap a mini horizontal paste bar next to your cursor with a single hotkey." },
  { title: "Temporary shelf", desc: "Keep many file as temporary holder and paste in other place later ." },
  { title: "Instant Text expander", desc: "Expand keywords automatically into full templates." },
  { title: "Shortcut key", desc: "Fully custom global system-wide triggers." },
]

export function SpecsSection() {
  return (
    <section id="specs" className="relative py-32 px-4 overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-indigo-950/10 via-background to-background" />
        {/* Animated grid */}
        <div 
          className="absolute inset-0 opacity-[0.03]"
          style={{
            backgroundImage: `radial-gradient(circle at 1px 1px, rgba(99,102,241,0.8) 1px, transparent 0)`,
            backgroundSize: "40px 40px",
          }}
        />
      </div>

      <div className="relative z-10 max-w-6xl mx-auto">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16"
        >
          <span className="inline-block px-4 py-1.5 rounded-full glass-card text-sm text-indigo-300 mb-4">
            Designed for Your Workflow
          </span>
          <h2 className="text-4xl md:text-5xl font-bold mb-6">
            <span className="text-foreground">Built for Daily</span>{" "}
            <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">
              Productivity
            </span>
          </h2>
          <p className="text-muted-foreground text-lg max-w-2xl mx-auto text-pretty">
            A clipboard manager that respects your focus and your computer. Enjoy high-speed productivity without annoying background updates, cloud requirements, or resource drain.
          </p>
        </motion.div>

        <div className="grid lg:grid-cols-2 gap-12 items-start">
          {/* Stats Grid */}
          <motion.div
            initial={{ opacity: 0, x: -30 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6 }}
            className="grid grid-cols-2 sm:grid-cols-3 gap-4"
          >
            {specs.map((spec, index) => (
              <motion.div
                key={spec.label}
                initial={{ opacity: 0, scale: 0.9 }}
                whileInView={{ opacity: 1, scale: 1 }}
                viewport={{ once: true }}
                transition={{ duration: 0.4, delay: index * 0.1 }}
                whileHover={{ scale: 1.05, y: -5 }}
                className="group"
              >
                <div className="relative aspect-square p-4 rounded-2xl glass-card flex flex-col items-center justify-center text-center hover:border-indigo-500/40 transition-all duration-300">
                  <div className="absolute inset-0 rounded-2xl bg-gradient-to-br from-indigo-500/10 to-violet-500/10 opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
                  <spec.icon className="w-8 h-8 text-indigo-400 mb-3 group-hover:scale-110 transition-transform duration-300" />
                  <div className="text-2xl font-bold text-foreground mb-1">
                    {spec.value}
                  </div>
                  <div className="text-xs text-muted-foreground uppercase tracking-wide">
                    {spec.label}
                  </div>
                </div>
              </motion.div>
            ))}
          </motion.div>

          {/* Requirements List */}
          <motion.div
            initial={{ opacity: 0, x: 30 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="glass-card rounded-2xl p-8"
          >
            <h3 className="text-xl font-bold text-foreground mb-6 flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500/20 to-violet-500/20 flex items-center justify-center">
                <Sparkles className="w-5 h-5 text-indigo-400" />
              </div>
              Productivity Features
            </h3>
            <ul className="space-y-4">
              {requirements.map((req, index) => (
                <motion.li
                  key={index}
                  initial={{ opacity: 0, x: -20 }}
                  whileInView={{ opacity: 1, x: 0 }}
                  viewport={{ once: true }}
                  transition={{ duration: 0.4, delay: 0.3 + index * 0.1 }}
                  className="flex items-start gap-3 text-muted-foreground text-sm"
                >
                  <div className="w-5 h-5 rounded-full bg-indigo-500/20 flex items-center justify-center shrink-0 mt-0.5">
                    <Check className="w-3 h-3 text-indigo-400" />
                  </div>
                  <div>
                    <span className="font-semibold text-foreground">{req.title}</span> — {req.desc}
                  </div>
                </motion.li>
              ))}
            </ul>
          </motion.div>
        </div>
      </div>
    </section>
  )
}

