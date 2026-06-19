"use client"

import { useEffect, useState, useRef } from "react"
import { motion, AnimatePresence } from "framer-motion"
import {
  Download,
  Play,
  ChevronDown,
  Sparkles,
  ArrowRight,
  Monitor,
  Cpu,
  Radio,
  Shield,
  Lock,
  Laptop,
  Terminal,
  Search,
  Copy,
  Check,
  Share2,
  Settings,
  Zap,
  Key,
  RefreshCw,
  Send,
  CheckCircle,
  Database,
  EyeOff,
  Layers,
  Plus,
  Trash2,
  FileText,
  FileImage,
  Globe,
  X
} from "lucide-react"
import { Button } from "@/components/ui/button"

function ParticleField() {
  const [particles, setParticles] = useState<Array<{ x: number; y: number; scale: number; delay: number }>>([])

  useEffect(() => {
    setParticles(
      [...Array(30)].map(() => ({
        x: Math.random() * 100,
        y: Math.random() * 100,
        scale: Math.random() * 0.5 + 0.5,
        delay: Math.random() * 5,
      }))
    )
  }, [])

  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none">
      {particles.map((particle, i) => (
        <motion.div
          key={i}
          className="absolute w-1 h-1 bg-indigo-500/30 rounded-full"
          style={{
            left: `${particle.x}%`,
            top: `${particle.y}%`,
          }}
          animate={{
            y: [0, -100, -200],
            opacity: [0, 1, 0],
            scale: [particle.scale, particle.scale * 1.5, particle.scale],
          }}
          transition={{
            duration: 12 + Math.random() * 12,
            repeat: Infinity,
            delay: particle.delay,
            ease: "linear",
          }}
        />
      ))}
    </div>
  )
}

function FloatingOrbs() {
  return (
    <div className="absolute inset-0 pointer-events-none overflow-hidden">
      <motion.div
        className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px]"
        animate={{ rotate: 360 }}
        transition={{ duration: 80, repeat: Infinity, ease: "linear" }}
      >
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-36 h-36 bg-indigo-600/10 rounded-full blur-3xl" />
        <div className="absolute bottom-0 left-1/2 -translate-x-1/2 w-48 h-48 bg-violet-600/10 rounded-full blur-3xl" />
      </motion.div>
      <motion.div
        className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[500px] h-[500px] rounded-full"
        style={{
          background: "radial-gradient(circle, rgba(99,102,241,0.1) 0%, transparent 70%)",
        }}
        animate={{
          scale: [1, 1.15, 1],
          opacity: [0.4, 0.6, 0.4],
        }}
        transition={{ duration: 5, repeat: Infinity, ease: "easeInOut" }}
      />
    </div>
  )
}

export function HeroSection() {
  const [activeTab, setActiveTab] = useState<"vault" | "expander" | "share" | "shelf" | "masking" | "settings">("vault")
  const [searchQuery, setSearchQuery] = useState("")
  const [expanderText, setExpanderText] = useState("")
  const [toast, setToast] = useState<string | null>(null)
  const [copiedId, setCopiedId] = useState<string | null>(null)

  // Temporary Shelf state (simulating drag-and-drop batch collection)
  const [shelfItems, setShelfItems] = useState<Array<{ id: string; name: string; size: string; type: "image" | "file" | "link"; path: string }>>([
    { id: "s1", name: "invoice_draft_v3.pdf", size: "1.2 MB", type: "file", path: "C:\\Users\\Desktop\\invoice_draft_v3.pdf" },
    { id: "s2", name: "hero_mockup.png", size: "2.4 MB", type: "image", path: "C:\\Users\\Downloads\\hero_mockup.png" },
    { id: "s3", name: "https://github.com/mitul002/ClipboardPro-Official", size: "Web Link", type: "link", path: "https://github.com/mitul002/ClipboardPro-Official" }
  ])

  // Masking Toggles
  const [eyeMasking, setEyeMasking] = useState(true)
  const [sensitiveMasking, setSensitiveMasking] = useState(true)
  const [mirrorEnabled, setMirrorEnabled] = useState(true)

  // P2P Share states
  const [selectedPeer, setSelectedPeer] = useState("Workstation-PC")
  const [selectedClip, setSelectedClip] = useState("API Token")
  const [isBeaming, setIsBeaming] = useState(false)
  const [beamProgress, setBeamProgress] = useState(0)

  // Settings states
  const [vaultHotkey, setVaultHotkey] = useState("Ctrl + Alt + V")
  const [startMinimized, setStartMinimized] = useState(true)

  // Real-time telemetry emulation
  const [telemetry, setTelemetry] = useState({ ram: 7.2, cpu: 0.04 })

  const simulatorRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const timer = setInterval(() => {
      setTelemetry({
        ram: parseFloat((7.1 + Math.random() * 0.5).toFixed(1)),
        cpu: parseFloat((0.02 + Math.random() * 0.08).toFixed(2))
      })
    }, 2000)
    return () => clearInterval(timer)
  }, [])

  // Expander logic
  const handleExpanderInput = (val: string) => {
    setExpanderText(val)
    if (val.endsWith(":email ")) {
      setExpanderText(val.replace(":email ", "support@clipboardpro.vercel.app"))
      showToast("Expander active: replaced ':email' shortcut!")
    } else if (val.endsWith(":sig ")) {
      setExpanderText(val.replace(":sig ", "Best regards,\nClipboardPro Engineering Team"))
      showToast("Expander active: replaced ':sig' shortcut!")
    } else if (val.endsWith(":guid ")) {
      setExpanderText(val.replace(":guid ", "c72f4a81-9d03-4e25-af46-b65d3c900a08"))
      showToast("Expander active: replaced ':guid' shortcut!")
    }
  }

  // Toast notifier helper
  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => {
      setToast(null)
    }, 3000)
  }

  // Copy helper
  const handleSecureCopy = (id: string, text: string) => {
    setCopiedId(id)
    navigator.clipboard.writeText(text)
    showToast("Copied successfully to clipboard!")
    setTimeout(() => setCopiedId(null), 2000)
  }

  // Beam packet simulation
  const handleBeamClipboard = () => {
    if (isBeaming) return
    setIsBeaming(true)
    setBeamProgress(0)

    const interval = setInterval(() => {
      setBeamProgress((prev) => {
        if (prev >= 100) {
          clearInterval(interval)
          setTimeout(() => {
            setIsBeaming(false)
            showToast(`Synchronized clipboard to ${selectedPeer} in 42ms!`)
          }, 500)
          return 100
        }
        return prev + 10
      })
    }, 80)
  }

  // Mock clipboard list items
  const vaultItems = [
    {
      id: "1",
      type: "Sensitive Info",
      content: "my-secret-password-123",
      masked: "••••••••••••••••••••••",
      label: "Wi-Fi Password",
      tag: "Auto-Hidden"
    },
    {
      id: "2",
      type: "Code Snippet",
      content: "public static void Main() {\n  Console.WriteLine(\"Hello World\");\n}",
      masked: "public static void Main() { ... }",
      label: "C# Main Method",
      tag: "Source Code"
    },
    {
      id: "3",
      type: "Web URL",
      content: "https://clipboardpro.vercel.app",
      masked: "https://clipboardpro.vercel.app",
      label: "ClipboardPro Website",
      tag: "Navigation"
    },
    {
      id: "4",
      type: "JSON Data",
      content: '{"user": "alex", "plan": "Lifetime"}',
      masked: '{"user": "alex", "plan": "Lifetime"}',
      label: "User Settings JSON",
      tag: "JSON Payload"
    },
    {
      id: "5",
      type: "Color HEX",
      content: "#6366F1",
      masked: "#6366F1",
      label: "Branding Accent Color",
      tag: "Design Color"
    }
  ]

  // Filter items
  const filteredItems = vaultItems.filter(item =>
    item.label.toLowerCase().includes(searchQuery.toLowerCase()) ||
    item.type.toLowerCase().includes(searchQuery.toLowerCase())
  )

  return (
    <section id="hero" className="relative min-h-screen flex flex-col items-center justify-center overflow-hidden px-4 pt-36 pb-24">
      {/* Background gradients */}
      <div
        className="absolute inset-0 animate-gradient"
        style={{
          background: "linear-gradient(135deg, #050510 0%, #0c122c 25%, #0b0f19 50%, #161a3f 75%, #050510 100%)",
        }}
      />

      {/* Grid pattern overlay */}
      <div
        className="absolute inset-0 opacity-15"
        style={{
          backgroundImage: `linear-gradient(rgba(99,102,241,0.08) 1px, transparent 1.5px),
                           linear-gradient(90deg, rgba(99,102,241,0.08) 1px, transparent 1.5px)`,
          backgroundSize: "60px 60px",
        }}
      />

      <ParticleField />
      <FloatingOrbs />

      <div className="relative z-10 max-w-7xl mx-auto w-full px-4 md:px-8">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">

          {/* LEFT COLUMN */}
          <div className="lg:col-span-6 text-left flex flex-col items-start justify-center relative">
            {/* Ambient background glow to cover the screen area and make it feel rich and premium */}
            <div className="absolute -inset-10 md:-inset-20 bg-indigo-500/15 rounded-full blur-[130px] pointer-events-none z-0" />
            <div className="absolute -top-10 -left-10 w-[300px] h-[300px] bg-violet-600/12 rounded-full blur-[110px] pointer-events-none z-0" />
            <div className="absolute -bottom-10 -left-10 w-[350px] h-[350px] bg-indigo-600/12 rounded-full blur-[120px] pointer-events-none z-0" />

            <div className="relative z-10 w-full flex flex-col items-start">
              {/* Version Badge */}
              <motion.div
                initial={{ opacity: 0, y: 15 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.6 }}
                className="mb-8"
              >
                <span className="inline-flex items-center gap-2 px-5 py-2 rounded-full glass-card text-xs font-semibold uppercase tracking-wider text-indigo-300 border-indigo-500/25 hover:border-indigo-400/40 transition-colors cursor-default">
                  <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
                  <span>Version 1.4.0 — Native Windows App</span>
                  <ArrowRight className="w-3.5 h-3.5 text-indigo-400" />
                </span>
              </motion.div>

              {/* Headline */}
              <motion.h1
                initial={{ opacity: 0, y: 25 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.8, delay: 0.1 }}
                className="text-5xl md:text-6xl lg:text-7xl font-extrabold mb-8 leading-[1.1] tracking-tight text-white animate-fade-in"
              >
                Instant
                <br />
                <span className="relative inline-block">
                  <span className="bg-gradient-to-r from-indigo-400 via-violet-300 to-indigo-500 bg-clip-text text-transparent">
                    Clipboard Sync
                  </span>
                  <motion.span
                    className="absolute -bottom-2 left-0 right-0 h-1.5 bg-gradient-to-r from-indigo-500/0 via-indigo-500/60 to-indigo-500/0 blur-sm"
                    initial={{ scaleX: 0 }}
                    animate={{ scaleX: 1 }}
                    transition={{ duration: 1, delay: 0.5 }}
                  />
                </span>
              </motion.h1>

              {/* Subtext */}
              <motion.p
                initial={{ opacity: 0, y: 25 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.8, delay: 0.2 }}
                className="text-lg md:text-xl text-indigo-200/70 max-w-xl mb-12 text-pretty leading-relaxed font-light"
              >
                A lightweight desktop assistant that automatically saves everything you copy, expands text abbreviations instantly as you type, and mirrors your clipboard history across all your Windows devices in real-time.
              </motion.p>

              {/* CTA action buttons */}
              <motion.div
                initial={{ opacity: 0, y: 25 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.8, delay: 0.3 }}
                className="flex flex-row flex-wrap gap-4 items-center w-full sm:w-auto"
              >
                <Button
                  size="default"
                  className="relative group h-12 sm:h-14 px-10 min-w-[180px] sm:min-w-[200px] text-sm sm:text-base font-bold bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 border-0 transition-all duration-300 overflow-hidden shadow-[0_0_15px_rgba(99,102,241,0.2)] hover:shadow-[0_0_20px_rgba(99,102,241,0.35)] rounded-xl flex items-center justify-center shrink-0"
                  asChild
                >
                  <a href="/ClipboardPro.exe" download className="flex items-center justify-center gap-2">
                    <motion.div
                      className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent -translate-x-full"
                      animate={{ translateX: ["100%", "-100%"] }}
                      transition={{ duration: 3.5, repeat: Infinity, repeatDelay: 2 }}
                    />
                    <Download className="w-4 h-4 relative z-10 shrink-0" />
                    <span className="relative z-10">Download</span>
                  </a>
                </Button>

                <Button
                  size="default"
                  variant="outline"
                  className="h-12 sm:h-14 px-10 min-w-[180px] sm:min-w-[200px] text-sm sm:text-base font-semibold glass-card border-indigo-500/25 hover:border-indigo-400/40 hover:bg-indigo-500/10 text-white transition-all duration-300 rounded-xl cursor-pointer flex items-center justify-center gap-2 shrink-0"
                  onClick={() => {
                    simulatorRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
                  }}
                >
                  <Play className="w-4 h-4 shrink-0" />
                  Try Simulator
                </Button>
              </motion.div>
            </div>
          </div>

          {/* RIGHT COLUMN: Interactive WPF App Simulator Card */}
          <div ref={simulatorRef} className="lg:col-span-6 w-full flex flex-col justify-center items-center">
            <motion.div
              initial={{ opacity: 0, scale: 0.95, y: 30 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              transition={{ duration: 0.8, delay: 0.3 }}
              className="w-full max-w-[580px] relative group"
            >
              {/* Glow Blur */}
              <div className="absolute -inset-1 bg-gradient-to-r from-indigo-500 to-violet-600 rounded-2xl blur-xl opacity-30 group-hover:opacity-40 transition duration-1000" />

              {/* Floating Screen-Edge QuickDrop Bubble (Simulating the WPF Floating Edge Dock) */}
              <motion.div
                className="absolute -right-5 top-1/2 -translate-y-1/2 z-30 cursor-pointer flex items-center"
                whileHover={{ x: 2 }}
                onClick={() => {
                  setActiveTab("shelf");
                  showToast("Opened QuickDrop Temporary Shelf!");
                }}
              >
                {/* Connecting bar */}
                <div className="w-1.5 h-12 bg-indigo-500/80 rounded-l border-y border-l border-indigo-400/35 backdrop-blur-md" />

                {/* Main Circular Bubble */}
                <div className="w-11 h-11 rounded-full bg-slate-950/90 border border-indigo-500/50 flex items-center justify-center relative shadow-[0_0_15px_rgba(99,102,241,0.4)] hover:shadow-[0_0_20px_rgba(99,102,241,0.6)] hover:border-indigo-400 transition-all group/bubble">
                  {/* Pulse Effect when shelf has items */}
                  {shelfItems.length > 0 && (
                    <span className="absolute inset-0 rounded-full bg-indigo-500/20 animate-ping" />
                  )}

                  <Layers className="w-5 h-5 text-indigo-400 group-hover/bubble:text-indigo-300 group-hover/bubble:scale-110 transition-all" />

                  {/* Badge */}
                  {shelfItems.length > 0 && (
                    <span className="absolute -top-1.5 -right-1.5 text-[9px] font-extrabold bg-indigo-600 border border-indigo-400 text-white px-1.5 py-0.5 rounded-full min-w-[18px] text-center shadow-md animate-bounce">
                      {shelfItems.length}
                    </span>
                  )}

                  {/* Tooltip */}
                  <div className="absolute right-full mr-3 top-1/2 -translate-y-1/2 bg-slate-950/95 border border-indigo-500/30 px-2.5 py-1.5 rounded-lg text-[10px] font-bold text-indigo-200 whitespace-nowrap opacity-0 group-hover/bubble:opacity-100 transition-opacity pointer-events-none shadow-xl backdrop-blur-md">
                    <span className="block text-[11px] text-white">QuickDrop Shelf</span>
                    <span className="text-[9px] text-slate-400 font-normal">Drag files here to compile</span>
                  </div>
                </div>
              </motion.div>

              {/* WPF Simulated App Window Frame */}
              <div
                className="relative rounded-2xl p-[1px] bg-slate-950/80 border border-white/10 backdrop-blur-2xl shadow-2xl overflow-hidden w-full flex flex-col h-[460px] select-none"
                style={{
                  background: "linear-gradient(135deg, #070913 0%, #0d1226 50%, #070914 100%)",
                }}
              >

                {/* 1. TITLE BAR */}
                <div className="flex items-center justify-between px-4 py-3 bg-slate-950/60 border-b border-white/5">
                  <div className="flex items-center gap-2">
                    {/* Official borderless Mini logo */}
                    <img src="/logo.png" alt="Logo" className="w-4.5 h-4.5 object-contain" />
                    <span className="text-xs font-bold text-slate-200 tracking-tight flex items-center gap-1.5">
                      <span>ClipboardPro</span>
                      <span className="text-[9px] text-indigo-400 font-mono bg-indigo-500/10 px-1.5 py-0.5 rounded">Native Windows v1.4.0</span>
                    </span>
                  </div>
                  {/* Title Bar Action Dots */}
                  <div className="flex items-center gap-2">
                    <span className="w-2.5 h-2.5 rounded-full bg-slate-700/60 hover:bg-slate-600 transition-colors cursor-pointer" />
                    <span className="w-2.5 h-2.5 rounded-full bg-slate-700/60 hover:bg-slate-600 transition-colors cursor-pointer" />
                    <span className="w-2.5 h-2.5 rounded-full bg-indigo-600/60 hover:bg-indigo-500 transition-colors cursor-pointer" />
                  </div>
                </div>

                {/* 2. BODY CONTENT (LEFT SIDEBAR + MAIN PANEL) */}
                <div className="flex flex-1 overflow-hidden">

                  {/* SIDEBAR NAVIGATION */}
                  <div className="w-[140px] bg-slate-950/40 border-r border-white/5 p-2 flex flex-col justify-between">
                    <div className="space-y-1">
                      {[
                        { id: "vault", icon: Database, label: "History Vault" },
                        { id: "expander", icon: Terminal, label: "Text Expander" },
                        { id: "share", icon: Share2, label: "Cross-PC Sync" },
                        { id: "shelf", icon: Layers, label: "Temporary Shelf" },
                        { id: "masking", icon: EyeOff, label: "Sensitive Masking" },
                        { id: "settings", icon: Settings, label: "App Settings" },
                      ].map(tab => {
                        const Icon = tab.icon
                        const isActive = activeTab === tab.id
                        return (
                          <button
                            key={tab.id}
                            onClick={() => setActiveTab(tab.id as any)}
                            className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-left text-xs font-semibold tracking-wide transition-all duration-150 cursor-pointer ${isActive
                                ? "bg-indigo-500/15 text-indigo-300 border-l-2 border-indigo-500"
                                : "text-slate-400 hover:text-slate-200 hover:bg-white/5"
                              }`}
                          >
                            <div className="flex items-center gap-2">
                              <Icon className={`w-3.5 h-3.5 ${isActive ? "text-indigo-400" : "text-slate-400"}`} />
                              <span>{tab.label}</span>
                            </div>
                            {tab.id === "shelf" && shelfItems.length > 0 && (
                              <span className="text-[9px] font-bold bg-indigo-500/35 border border-indigo-400/50 text-indigo-300 px-1.5 py-0.5 rounded-full min-w-[15px] text-center scale-90">
                                {shelfItems.length}
                              </span>
                            )}
                          </button>
                        )
                      })}
                    </div>

                    {/* Miniature live telemetry counters */}
                    <div className="p-2 bg-slate-950/60 rounded-lg border border-white/5 space-y-1">
                      <div className="text-[8px] uppercase tracking-wider font-extrabold text-indigo-400/50">Background Service Active</div>
                      <div className="flex justify-between text-[9px] font-mono text-slate-400">
                        <span className="flex items-center gap-0.5"><Cpu className="w-2.5 h-2.5 text-emerald-400" /> CPU</span>
                        <span className="text-slate-200">{telemetry.cpu}%</span>
                      </div>
                      <div className="flex justify-between text-[9px] font-mono text-slate-400">
                        <span className="flex items-center gap-0.5"><Monitor className="w-2.5 h-2.5 text-indigo-400" /> Memory</span>
                        <span className="text-slate-200">{telemetry.ram}MB</span>
                      </div>
                    </div>
                  </div>

                  {/* MAIN PANEL */}
                  <div className="flex-1 p-4 overflow-y-auto flex flex-col justify-between">

                    {/* PANEL VIEWPORTS */}
                    <div className="flex-1">

                      {/* TAB 1: SECURE VAULT CONTAINER */}
                      {activeTab === "vault" && (
                        <div className="space-y-3 flex flex-col h-full">
                          <div className="flex items-center gap-2 bg-slate-950/60 border border-white/5 rounded-lg px-2.5 py-1.5">
                            <Search className="w-3.5 h-3.5 text-slate-500" />
                            <input
                              type="text"
                              placeholder="Search clipboard history..."
                              value={searchQuery}
                              onChange={e => setSearchQuery(e.target.value)}
                              className="bg-transparent border-0 outline-none text-xs text-white placeholder-slate-500 w-full"
                            />
                          </div>

                          <div className="flex-1 space-y-1.5 max-h-[260px] overflow-y-auto pr-1">
                            {filteredItems.length > 0 ? (
                              filteredItems.map(item => (
                                <div
                                  key={item.id}
                                  className="flex items-center justify-between p-2 rounded-lg bg-slate-950/50 hover:bg-slate-950 border border-white/5 group/row transition-all duration-150"
                                >
                                  <div className="space-y-0.5 overflow-hidden pr-2">
                                    <div className="flex items-center gap-1.5">
                                      <span className="text-[10px] font-bold text-indigo-400 uppercase font-mono">{item.type}</span>
                                      <span className="text-[8px] bg-slate-800 text-slate-400 px-1 rounded">{item.tag}</span>
                                    </div>
                                    <div className="text-xs text-slate-200 font-mono truncate max-w-[220px]">
                                      {sensitiveMasking && item.type === "Sensitive Info" ? item.masked : item.content}
                                    </div>
                                  </div>

                                  <button
                                    onClick={() => handleSecureCopy(item.id, item.type === "Sensitive Info" && sensitiveMasking ? item.masked : item.content)}
                                    className="p-1.5 rounded bg-slate-900 border border-white/5 hover:border-indigo-500/30 text-indigo-400 hover:text-white transition-all duration-150 cursor-pointer shrink-0"
                                    title="Copy"
                                  >
                                    {copiedId === item.id ? (
                                      <Check className="w-3 h-3 text-emerald-400" />
                                    ) : (
                                      <Copy className="w-3 h-3" />
                                    )}
                                  </button>
                                </div>
                              ))
                            ) : (
                              <div className="text-center py-8 text-xs text-slate-500">No cached items found.</div>
                            )}
                          </div>
                        </div>
                      )}

                      {/* TAB 2: TEXT EXPANDER */}
                      {activeTab === "expander" && (
                        <div className="space-y-4">
                          <div className="bg-slate-950/50 border border-indigo-500/10 rounded-lg p-3 space-y-2">
                            <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-400">Registered Expander Snippets</span>
                            <div className="grid grid-cols-3 gap-2">
                              {[
                                { key: ":email", val: "support@..." },
                                { key: ":sig", val: "Best regards..." },
                                { key: ":guid", val: "c72f4a81..." },
                              ].map(item => (
                                <div key={item.key} className="bg-slate-950/80 border border-white/5 p-1.5 rounded text-center">
                                  <span className="text-[10px] font-mono text-indigo-300 font-bold block">{item.key}</span>
                                  <span className="text-[8px] text-slate-500 block truncate">{item.val}</span>
                                </div>
                              ))}
                            </div>
                          </div>

                          <div className="space-y-2">
                            <label className="text-[10px] font-bold text-slate-400 uppercase">Live Sandbox Expander Input</label>
                            <textarea
                              rows={3}
                              placeholder="Type ':email ' or ':sig ' (with spaces) to see how abbreviations expand instantly in any editor..."
                              value={expanderText}
                              onChange={e => handleExpanderInput(e.target.value)}
                              className="w-full bg-slate-950/80 border border-white/10 rounded-lg p-2.5 text-xs text-slate-100 placeholder-slate-500 focus:outline-none focus:border-indigo-500/40 focus:ring-1 focus:ring-indigo-500/20"
                            />
                            <div className="flex justify-between text-[9px] text-slate-500">
                              <span>Expander Status: <span className="text-emerald-400 font-bold">Active</span></span>
                              <button
                                onClick={() => setExpanderText("")}
                                className="text-indigo-400 hover:underline cursor-pointer"
                              >
                                Clear sandbox
                              </button>
                            </div>
                          </div>
                        </div>
                      )}

                      {/* TAB 3: LOCAL P2P SHARE */}
                      {activeTab === "share" && (
                        <div className="space-y-4">
                          <div className="grid grid-cols-2 gap-3">
                            <div className="space-y-1.5">
                              <label className="text-[10px] font-bold text-slate-400 uppercase">Select Target Client</label>
                              <select
                                value={selectedPeer}
                                onChange={e => setSelectedPeer(e.target.value)}
                                className="w-full bg-slate-950/80 border border-white/5 rounded-lg p-2 text-xs text-slate-200 focus:outline-none"
                              >
                                <option value="Workstation-PC">🖥️ WORKSTATION-PC (Active)</option>
                                <option value="Laptop-Office">💻 LAPTOP-OFFICE (Active)</option>
                                <option value="Dev-Rig-Local">⚙️ DEV-RIG (Active)</option>
                              </select>
                            </div>
                            <div className="space-y-1.5">
                              <label className="text-[10px] font-bold text-slate-400 uppercase">Select Payload</label>
                              <select
                                value={selectedClip}
                                onChange={e => setSelectedClip(e.target.value)}
                                className="w-full bg-slate-950/80 border border-white/5 rounded-lg p-2 text-xs text-slate-200 focus:outline-none"
                              >
                                <option value="API Token">🔑 API Token Credentials</option>
                                <option value="WPF Core Signature">📋 Code Block</option>
                                <option value="Admin Portal Console">🌐 URL Link</option>
                              </select>
                            </div>
                          </div>

                          <div className="bg-slate-950/60 border border-white/5 rounded-xl p-3.5 flex flex-col items-center justify-center min-h-[120px] relative">
                            {isBeaming ? (
                              <div className="w-full space-y-3 text-center">
                                <span className="text-[10px] text-indigo-400 uppercase font-bold tracking-wider animate-pulse block">Syncing Clipboard Item...</span>
                                <div className="flex items-center justify-center gap-1.5 font-mono text-[9px] text-slate-400">
                                  <span>Local Network Route</span>
                                  <span>•</span>
                                  <span>Direct Transfer</span>
                                </div>
                                <div className="w-full bg-slate-900 rounded-full h-1.5 overflow-hidden">
                                  <motion.div
                                    className="bg-indigo-500 h-full"
                                    initial={{ width: "0%" }}
                                    animate={{ width: `${beamProgress}%` }}
                                    transition={{ duration: 0.1 }}
                                  />
                                </div>
                                <span className="text-[10px] text-indigo-300 font-mono font-bold block">{beamProgress}%</span>
                              </div>
                            ) : (
                              <div className="text-center space-y-3">
                                <Radio className="w-8 h-8 text-indigo-400/40 mx-auto animate-pulse" />
                                <div className="space-y-1">
                                  <span className="text-xs text-slate-300 font-semibold block">Local PC-to-PC Sync</span>
                                  <p className="text-[10px] text-slate-500 max-w-[260px] mx-auto">Sends clipboard text and files directly to your other computers on your home or office Wi-Fi network.</p>
                                </div>
                                <Button
                                  onClick={handleBeamClipboard}
                                  className="h-8 px-4 text-[10px] font-bold bg-indigo-600 hover:bg-indigo-500 rounded-lg flex items-center gap-1.5 mx-auto"
                                >
                                  <Send className="w-3 h-3" />
                                  Beam Clipboard Payload
                                </Button>
                              </div>
                            )}
                          </div>
                        </div>
                      )}

                      {/* TAB 3.5: TEMPORARY SHELF */}
                      {activeTab === "shelf" && (
                        <div className="space-y-3 flex flex-col h-full">
                          <div className="flex justify-between items-center bg-slate-950/60 border border-white/5 rounded-lg px-2.5 py-1.5">
                            <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-400">Temporary Shelf (Batch Stash)</span>
                            <div className="flex gap-1.5">
                              <button
                                onClick={() => {
                                  const mockFiles = [
                                    { id: `s-${Date.now()}-1`, name: "logo_outline.svg", size: "45 KB", type: "image" as const, path: "C:\\Projects\\logo_outline.svg" },
                                    { id: `s-${Date.now()}-2`, name: "project_archive.zip", size: "32.4 MB", type: "file" as const, path: "D:\\Backups\\project_archive.zip" },
                                    { id: `s-${Date.now()}-3`, name: "https://google.com/search?q=clipboard", size: "Web Link", type: "link" as const, path: "https://google.com/search?q=clipboard" }
                                  ]
                                  const nextFile = mockFiles[Math.floor(Math.random() * mockFiles.length)]
                                  if (shelfItems.some(i => i.name === nextFile.name)) {
                                    showToast("File is already in the shelf!")
                                    return
                                  }
                                  setShelfItems([...shelfItems, nextFile])
                                  showToast(`Dropped '${nextFile.name}' onto the shelf!`)
                                }}
                                className="text-[9px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 hover:bg-indigo-500/30 px-1.5 py-0.5 rounded flex items-center gap-0.5 cursor-pointer"
                              >
                                <Plus className="w-2.5 h-2.5 animate-pulse" />
                                Drop File
                              </button>
                              {shelfItems.length > 0 && (
                                <button
                                  onClick={() => {
                                    setShelfItems([])
                                    showToast("Cleared all items from shelf")
                                  }}
                                  className="text-[9px] bg-red-500/20 text-red-300 border border-red-500/30 hover:bg-red-500/30 px-1.5 py-0.5 rounded flex items-center gap-0.5 cursor-pointer"
                                >
                                  <Trash2 className="w-2.5 h-2.5" />
                                  Clear All
                                </button>
                              )}
                            </div>
                          </div>

                          <div className="flex-1 space-y-1.5 max-h-[220px] overflow-y-auto pr-1">
                            {shelfItems.length > 0 ? (
                              shelfItems.map(item => (
                                <div
                                  key={item.id}
                                  className="flex items-center justify-between p-2 rounded-lg bg-slate-950/50 hover:bg-slate-950 border border-white/5 group/row transition-all duration-150 animate-fade-in"
                                >
                                  <div className="flex items-center gap-2 overflow-hidden pr-2">
                                    <div className="w-7 h-7 rounded bg-indigo-500/10 flex items-center justify-center shrink-0 border border-indigo-500/20">
                                      {item.type === "image" ? (
                                        <FileImage className="w-3.5 h-3.5 text-indigo-400" />
                                      ) : item.type === "link" ? (
                                        <Globe className="w-3.5 h-3.5 text-indigo-400" />
                                      ) : (
                                        <FileText className="w-3.5 h-3.5 text-indigo-400" />
                                      )}
                                    </div>
                                    <div className="space-y-0.5 overflow-hidden">
                                      <div className="text-[11px] text-slate-200 font-bold truncate max-w-[170px]" title={item.name}>
                                        {item.name}
                                      </div>
                                      <div className="text-[9px] text-slate-500 font-mono flex items-center gap-1.5">
                                        <span>{item.size}</span>
                                        <span>•</span>
                                        <span className="truncate max-w-[120px]" title={item.path}>{item.path}</span>
                                      </div>
                                    </div>
                                  </div>

                                  <div className="flex items-center gap-1 shrink-0">
                                    <button
                                      onClick={() => {
                                        navigator.clipboard.writeText(item.path)
                                        showToast(`Dragged out '${item.name}': path copied!`)
                                      }}
                                      className="p-1.5 rounded bg-slate-900 border border-white/5 hover:border-indigo-500/30 text-indigo-400 hover:text-white transition-all duration-150 cursor-pointer"
                                      title="Simulate Drag Out / Copy Path"
                                    >
                                      <ArrowRight className="w-3 h-3" />
                                    </button>
                                    <button
                                      onClick={() => {
                                        setShelfItems(shelfItems.filter(i => i.id !== item.id))
                                        showToast(`Removed '${item.name}' from shelf`)
                                      }}
                                      className="p-1.5 rounded bg-slate-900 border border-white/5 hover:border-red-500/30 text-slate-500 hover:text-red-400 transition-all duration-150 cursor-pointer"
                                      title="Remove"
                                    >
                                      <X className="w-3 h-3" />
                                    </button>
                                  </div>
                                </div>
                              ))
                            ) : (
                              <div className="flex flex-col items-center justify-center py-10 text-center gap-2 border border-dashed border-white/15 rounded-xl bg-slate-950/20">
                                <Layers className="w-8 h-8 text-slate-600 animate-pulse" />
                                <div className="text-xs font-semibold text-slate-400">Temporary Shelf is Empty</div>
                                <p className="text-[10px] text-slate-500 max-w-[180px] leading-snug">Drag files, links, or folders to the screen edge or click 'Drop File' to pool them here.</p>
                              </div>
                            )}
                          </div>

                          <div className="text-[9px] text-slate-500 font-mono leading-snug bg-slate-950/40 p-2 rounded border border-white/5">
                            <span className="text-indigo-400 font-bold block mb-0.5">Smart Memory Optimization:</span>
                            Keeps memory usage under 1MB by referencing files directly instead of loading them into memory.
                          </div>
                        </div>
                      )}

                      {/* TAB 4: SENSITIVE MASKING */}
                      {activeTab === "masking" && (
                        <div className="space-y-3.5">
                          <div className="bg-slate-950/60 border border-white/5 rounded-lg p-3 space-y-1.5">
                            <span className="text-[10px] font-bold text-slate-400 uppercase block">Sensitive Helper Engine</span>
                            <div className="flex items-center justify-between gap-2 font-mono text-[10px] bg-slate-950 border border-white/10 p-2 rounded text-indigo-300 select-all">
                              <span>Auto-Detect Active (Local Storage)</span>
                              <EyeOff className="w-3.5 h-3.5 text-indigo-400 shrink-0" />
                            </div>
                          </div>

                          <div className="space-y-2">
                            <label className="text-[10px] font-bold text-slate-400 uppercase">Masking Settings</label>

                            <div className="space-y-1.5">
                              <div className="flex items-center justify-between p-2 rounded-lg bg-slate-950/50 border border-white/5">
                                <div className="space-y-0.5">
                                  <span className="text-xs text-slate-200 font-bold block">Sensitive Eye Masking</span>
                                  <span className="text-[9px] text-slate-500 block">Saves your history lists with auto-masking active.</span>
                                </div>
                                <button
                                  onClick={() => setEyeMasking(!eyeMasking)}
                                  className={`w-9 h-5 rounded-full p-0.5 transition-colors duration-200 cursor-pointer focus:outline-none ${eyeMasking ? "bg-indigo-600" : "bg-slate-800"}`}
                                >
                                  <div className={`bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200 ${eyeMasking ? "translate-x-4" : "translate-x-0"}`} />
                                </button>
                              </div>

                              <div className="flex items-center justify-between p-2 rounded-lg bg-slate-950/50 border border-white/5">
                                <div className="space-y-0.5">
                                  <span className="text-xs text-slate-200 font-bold block">Regex Pattern Detection</span>
                                  <span className="text-[9px] text-slate-500 block">Auto-detect credentials and secrets in list view.</span>
                                </div>
                                <button
                                  onClick={() => setSensitiveMasking(!sensitiveMasking)}
                                  className={`w-9 h-5 rounded-full p-0.5 transition-colors duration-200 cursor-pointer focus:outline-none ${sensitiveMasking ? "bg-indigo-600" : "bg-slate-800"}`}
                                >
                                  <div className={`bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200 ${sensitiveMasking ? "translate-x-4" : "translate-x-0"}`} />
                                </button>
                              </div>
                            </div>
                          </div>
                        </div>
                      )}

                      {/* TAB 5: APP SETTINGS */}
                      {activeTab === "settings" && (
                        <div className="space-y-4">
                          <div className="grid grid-cols-2 gap-3">
                            <div className="bg-slate-950/50 border border-white/5 p-3 rounded-lg space-y-1.5">
                              <span className="text-[9px] font-bold text-slate-500 uppercase block">Global Open Hotkey</span>
                              <select
                                value={vaultHotkey}
                                onChange={e => setVaultHotkey(e.target.value)}
                                className="w-full bg-slate-950 border border-white/10 rounded p-1 text-xs text-slate-200 focus:outline-none"
                              >
                                <option value="Ctrl + Alt + V">Ctrl + Alt + V</option>
                                <option value="Ctrl + Shift + C">Ctrl + Shift + C</option>
                                <option value="Alt + X">Alt + X</option>
                              </select>
                            </div>

                            <div className="bg-slate-950/50 border border-white/5 p-3 rounded-lg space-y-1.5">
                              <span className="text-[9px] font-bold text-slate-500 uppercase block">Storage Optimization</span>
                              <button
                                onClick={() => showToast("Storage optimization and image cache cleanup completed successfully!")}
                                className="w-full h-8 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-bold text-[10px] transition-colors cursor-pointer"
                              >
                                Optimize Storage
                              </button>
                            </div>
                          </div>

                          <div className="bg-slate-950/50 border border-white/5 rounded-xl p-3 space-y-2">
                            <span className="text-[10px] font-bold text-slate-400 uppercase block">Startup Launch Configuration</span>

                            <div className="flex items-center justify-between">
                              <div className="space-y-0.5">
                                <span className="text-xs text-slate-200 font-bold block">Start Minimized in System Tray</span>
                                <span className="text-[9px] text-slate-500 block">Launch minimized to Windows taskbar notification tray.</span>
                              </div>
                              <button
                                onClick={() => setStartMinimized(!startMinimized)}
                                className={`w-9 h-5 rounded-full p-0.5 transition-colors duration-200 cursor-pointer focus:outline-none ${startMinimized ? "bg-indigo-600" : "bg-slate-800"}`}
                              >
                                <div className={`bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200 ${startMinimized ? "translate-x-4" : "translate-x-0"}`} />
                              </button>
                            </div>
                          </div>
                        </div>
                      )}

                    </div>

                    {/* 3. SIMULATED TOAST NOTIFICATION POPUP */}
                    <AnimatePresence>
                      {toast && (
                        <motion.div
                          initial={{ opacity: 0, y: 15 }}
                          animate={{ opacity: 1, y: 0 }}
                          exit={{ opacity: 0, y: 15 }}
                          className="mt-3 bg-emerald-500/10 border border-emerald-500/30 rounded-lg p-2.5 flex items-center gap-2 text-emerald-400 font-semibold"
                        >
                          <CheckCircle className="w-4 h-4 shrink-0" />
                          <span className="text-[10.5px] leading-tight select-all">{toast}</span>
                        </motion.div>
                      )}
                    </AnimatePresence>

                  </div>
                </div>

                {/* 4. MOCK WINDOW FOOTER BAR */}
                <div className="bg-slate-950/80 border-t border-white/5 px-4 py-2 flex items-center justify-between text-[9px] text-slate-500">
                  <div className="flex items-center gap-1.5">
                    <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse" />
                    <span className="font-bold text-slate-400">Local Sync Ready: Connected to Network</span>
                  </div>
                  <div className="flex items-center gap-3 font-mono">
                    <span>Storage: Connected</span>
                    <span>•</span>
                    <span>Latency: &lt;1ms</span>
                  </div>
                </div>

              </div>
            </motion.div>
          </div>

        </div>

        {/* BOTTOM SECTION: Majestic Floating Glass Widescreen Stats Telemetry Bar */}
        <motion.div
          initial={{ opacity: 0, y: 35 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.4 }}
          className="mt-16 mx-auto max-w-7xl w-full px-12 py-8 rounded-3xl glass-card border border-indigo-500/25 shadow-2xl backdrop-blur-2xl flex flex-wrap justify-around items-center gap-8 md:gap-12 animate-fade-in"
        >
          {[
            { label: "Transfer Speed", value: "Instant", suffix: "" },
            { label: "System Memory Usage", value: "Low", suffix: " Memory" },
            { label: "Light & Dark Mode", value: "Glass", suffix: " Theme" },
            { label: "Clipboard History", value: "Unlimited", suffix: "" },
          ].map((stat, index) => (
            <motion.div
              key={stat.label}
              initial={{ opacity: 0, scale: 0.7 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.5, delay: 0.5 + index * 0.1 }}
              className="text-center group cursor-default"
            >
              <div className="text-3xl md:text-4xl font-extrabold tracking-tight">
                <span className="bg-gradient-to-r from-indigo-300 to-violet-300 bg-clip-text text-transparent group-hover:from-indigo-200 group-hover:to-violet-200 transition-all">
                  {stat.value}
                </span>
                <span className="text-indigo-400/60 font-bold ml-0.5">{stat.suffix}</span>
              </div>
              <div className="text-xs font-bold uppercase tracking-wider text-indigo-200/50 mt-1.5">{stat.label}</div>
            </motion.div>
          ))}
        </motion.div>
      </div>

      {/* Scroll indicator */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 1.5, duration: 1 }}
        className="absolute bottom-8 left-1/2 -translate-x-1/2"
      >
        <motion.a
          href="#features"
          animate={{ y: [0, 8, 0] }}
          transition={{ duration: 2.2, repeat: Infinity, ease: "easeInOut" }}
          className="flex flex-col items-center gap-1.5 text-muted-foreground hover:text-indigo-400 transition-colors"
        >
          <span className="text-[10px] uppercase tracking-widest font-semibold">Scroll to explore</span>
          <ChevronDown className="w-4 h-4" />
        </motion.a>
      </motion.div>
    </section>
  )
}
