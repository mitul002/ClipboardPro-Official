"use client"

import { motion, useMotionTemplate, useMotionValue } from "framer-motion"
import { useRef } from "react"
import { 
  Database,
  Layers,
  EyeOff,
  Sparkles,
  Crop,
  Keyboard,
  Share2,
  FileArchive,
  Gauge
} from "lucide-react"

const features = [
  {
    icon: Database,
    title: "Multi-Format Vault",
    description: "Never lose a copied item. Instantly search, filter, and recall texts, high-res images, files, hex colors, and webpage links from a single dashboard.",
    gradient: "from-indigo-500 to-violet-600",
    tag: "History",
  },
  {
    icon: Gauge,
    title: "Cursor Paste Bar",
    description: "Press Ctrl+Shift+V to snap a mini-paste bar to your text cursor. Instantly paste items directly into your active window with zero clicks and zero lag.",
    gradient: "from-indigo-500 to-blue-600",
    tag: "Interface",
  },
  {
    icon: Layers,
    title: "QuickDrop Shelf",
    description: "Drag text, links, or files to the screen edge to stash them temporarily. Keep files handy for quick access without cluttering your desktop.",
    gradient: "from-violet-500 to-indigo-600",
    tag: "Productivity",
  },
  {
    icon: Sparkles,
    title: "Smart Helpers",
    description: "Detects color codes to show visual badges, automatically formats messy JSON strings, and fetches web page titles in the background to clean up your dashboard.",
    gradient: "from-indigo-500 to-violet-500",
    tag: "Intelligence",
  },
  {
    icon: EyeOff,
    title: "Sensitive Masking",
    description: "Automatically flags and hides passwords, credit cards, or API keys in your history. Use a simple eye toggle to reveal or hide them safely on your screen.",
    gradient: "from-blue-500 to-indigo-600",
    tag: "Privacy",
  },
  {
    icon: Crop,
    title: "In-App Screenshot & Text Editor",
    description: "Crop, annotate, and edit captured screenshots or modify clipboard text dynamically before pasting. A built-in toolbar keeps your quick edits seamless and productive.",
    gradient: "from-blue-500 to-indigo-600",
    tag: "Editing",
  },
  {
    icon: Keyboard,
    title: "Hotkey Text Expander",
    description: "Define shortcuts (like :email) that automatically expand into full email templates or code blocks. Works instantly across all your apps.",
    gradient: "from-violet-500 to-indigo-500",
    tag: "Automation",
  },
  {
    icon: Share2,
    title: "Instant Local Sharing",
    description: "Share your copied snippets and files between your workstation and laptop instantly. Discovers nearby devices automatically over local Wi-Fi with zero setup.",
    gradient: "from-blue-500 to-indigo-600",
    tag: "Sharing",
  },
  {
    icon: FileArchive,
    title: "Modern Windows UI",
    description: "Beautiful visual styling that perfectly matches Windows 11. Soft, translucent backdrops blend with your wallpaper for a clean, premium desktop look.",
    gradient: "from-indigo-600 to-violet-500",
    tag: "Aesthetic",
  },
]

const containerVariants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.08,
    },
  },
}

const itemVariants = {
  hidden: { opacity: 0, y: 30 },
  visible: { 
    opacity: 1, 
    y: 0,
    transition: { duration: 0.6, ease: "easeOut" }
  },
}

function FeatureCard({ feature }: { feature: typeof features[0] }) {
  const cardRef = useRef<HTMLDivElement>(null)
  const mouseX = useMotionValue(0)
  const mouseY = useMotionValue(0)

  function handleMouseMove({ clientX, clientY }: React.MouseEvent) {
    if (!cardRef.current) return
    const { left, top } = cardRef.current.getBoundingClientRect()
    mouseX.set(clientX - left)
    mouseY.set(clientY - top)
  }

  const bgTemplate = useMotionTemplate`
    radial-gradient(
      250px circle at ${mouseX}px ${mouseY}px,
      rgba(99, 102, 241, 0.12),
      transparent 80%
    )
  `

  const borderTemplate = useMotionTemplate`
    radial-gradient(
      150px circle at ${mouseX}px ${mouseY}px,
      rgba(99, 102, 241, 0.3),
      transparent 80%
    )
  `

  return (
    <motion.div
      variants={itemVariants}
      onMouseMove={handleMouseMove}
      className="relative rounded-2xl glass-card overflow-hidden group border border-white/5"
      ref={cardRef}
    >
      {/* Light sweep hover background */}
      <motion.div 
        className="absolute inset-0 pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity duration-300 z-0"
        style={{ background: bgTemplate }}
      />
      
      {/* Dynamic border highlight */}
      <motion.div
        className="absolute inset-0 pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity duration-300 z-10"
        style={{
          border: "1px solid transparent",
          backgroundImage: borderTemplate,
          backgroundClip: "border-box",
          WebkitMask: "linear-gradient(#fff 0 0) padding-box, linear-gradient(#fff 0 0)",
          WebkitMaskComposite: "destination-out",
          maskComposite: "exclude"
        }}
      />

      <div className="p-8 relative z-20 flex flex-col h-full justify-between gap-6">
        <div>
          {/* Header */}
          <div className="flex justify-between items-center mb-6">
            <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-400 bg-indigo-500/10 px-2.5 py-1 rounded-full border border-indigo-500/15">
              {feature.tag}
            </span>
            <div className={`w-12 h-12 rounded-xl bg-gradient-to-br ${feature.gradient} p-0.5 shadow-md shadow-indigo-500/5`}>
              <div className="w-full h-full rounded-xl bg-background flex items-center justify-center">
                <feature.icon className="w-5 h-5 text-indigo-300 group-hover:scale-110 transition-transform duration-300" />
              </div>
            </div>
          </div>

          {/* Title */}
          <h3 className="text-lg font-bold text-foreground group-hover:text-indigo-300 transition-colors mb-3">
            {feature.title}
          </h3>

          {/* Description */}
          <p className="text-muted-foreground text-sm leading-relaxed">
            {feature.description}
          </p>
        </div>
      </div>
    </motion.div>
  )
}

export function FeaturesSection() {
  return (
    <section id="features" className="relative py-32 px-4 overflow-hidden">
      {/* Background gradients */}
      <div className="absolute inset-0">
        <div className="absolute top-0 right-1/4 w-[600px] h-[600px] rounded-full bg-indigo-900/5 blur-[120px] pointer-events-none" />
        <div className="absolute bottom-0 left-1/4 w-[600px] h-[600px] rounded-full bg-violet-900/5 blur-[120px] pointer-events-none" />
      </div>

      <div className="relative z-10 max-w-7xl mx-auto">
        {/* Section Header */}
        <div className="text-center max-w-3xl mx-auto mb-20">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6 }}
          >
            <span className="inline-block px-4 py-1.5 rounded-full glass-card text-xs font-semibold uppercase tracking-wider text-indigo-300 mb-4 border border-indigo-500/20 shadow-sm shadow-indigo-500/5">
              Supercharged Productivity
            </span>
          </motion.div>
          <motion.h2
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="text-4xl md:text-5xl font-extrabold leading-tight tracking-tight text-white mb-6 text-balance"
          >
            Copy Smarter. Paste Faster.
            <br />
            <span className="bg-gradient-to-r from-indigo-400 via-violet-300 to-indigo-500 bg-clip-text text-transparent">
              Save Hours Every Week.
            </span>
          </motion.h2>
          <motion.p
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="text-base text-muted-foreground leading-relaxed text-pretty"
          >
            ClipboardPro is a lightweight, offline-first productivity hub designed to keep you focused and speed up your daily typing.
          </motion.p>
        </div>

        {/* Feature Cards Grid */}
        <motion.div
          variants={containerVariants}
          initial="hidden"
          whileInView="visible"
          viewport={{ once: true, margin: "-100px" }}
          className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6"
        >
          {features.map((feature) => (
            <FeatureCard key={feature.title} feature={feature} />
          ))}
        </motion.div>
      </div>
    </section>
  )
}
