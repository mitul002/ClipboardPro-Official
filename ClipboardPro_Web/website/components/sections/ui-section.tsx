"use client"

import { motion, AnimatePresence } from "framer-motion"
import { useState, useEffect, useRef } from "react"
import { Sliders, Cpu, Lock, ToggleLeft, ToggleRight, Radio, Database, EyeOff } from "lucide-react"

const glassPresets = [
  { name: "Slate Obsidian", accent: "#6366f1", bg: "rgba(15, 23, 42, 0.85)", border: "#312e81" },
  { name: "Arctic Indigo", accent: "#818cf8", bg: "rgba(30, 41, 59, 0.4)", border: "#4f46e5" },
  { name: "Neon Shield", accent: "#7c3aed", bg: "rgba(15, 10, 30, 0.85)", border: "#5b21b6" },
  { name: "Emerald Cache", accent: "#10b981", bg: "rgba(10, 25, 20, 0.85)", border: "#064e3b" },
  { name: "Sapphire Core", accent: "#3b82f6", bg: "rgba(10, 20, 40, 0.85)", border: "#1e3a8a" },
  { name: "Pure Ghost", accent: "#ffffff", bg: "rgba(255, 255, 255, 0.05)", border: "rgba(255, 255, 255, 0.15)" },
  { name: "Crimson Secure", accent: "#ef4444", bg: "rgba(25, 10, 15, 0.85)", border: "#7f1d1d" },
  { name: "Amber Auth", accent: "#f59e0b", bg: "rgba(30, 20, 10, 0.85)", border: "#78350f" },
  { name: "Teal Tunnel", accent: "#14b8a6", bg: "rgba(10, 25, 25, 0.85)", border: "#115e59" },
  { name: "Obsidian Dusk", accent: "#a855f7", bg: "rgba(20, 10, 30, 0.85)", border: "#6b21a8" },
]

const TEXTS_ITEMS = [
  // Inner ring items (exactly 18 items)
  { emoji: "📋", label: "Text Snippet" },
  { emoji: "💻", label: "C# Snippet" },
  { emoji: "🔑", label: "API Token" },
  { emoji: "🌐", label: "Web URL" },
  { emoji: "📊", label: "JSON Data" },
  { emoji: "📝", label: "Draft Note" },
  { emoji: "📜", label: "SQL Query" },
  { emoji: "💾", label: "Local History" },
  { emoji: "📋", label: "Rich Text" },
  { emoji: "🔑", label: "OAuth Key" },
  { emoji: "💻", label: "WPF Code" },
  { emoji: "💬", label: "Chat Log" },
  { emoji: "📝", label: "HTML Body" },
  { emoji: "📁", label: "Img Path" },
  { emoji: "👁️", label: "Masked PW" },
  { emoji: "💾", label: "Local Cache" },
  { emoji: "📦", label: "NuGet Command" },
  { emoji: "🧪", label: "RegEx Pattern" },
  // Outer ring items (exactly 22 items)
  { emoji: "📋", label: "Clipboard Hook" },
  { emoji: "🔑", label: "Activation Key" },
  { emoji: "💻", label: "JSON DB" },
  { emoji: "📜", label: "Sequence" },
  { emoji: "🔑", label: "License Key" },
  { emoji: "📦", label: "Local Payload" },
  { emoji: "🔄", label: "Dispatcher" },
  { emoji: "📡", label: "Sync Stream" },
  { emoji: "⚙️", label: "WPF Core" },
  { emoji: "⏱️", label: "Latency Test" },
  { emoji: "🌐", label: "TLS Link" },
  { emoji: "📊", label: "Flow Graph" },
  { emoji: "📁", label: "Asset Path" },
  { emoji: "💬", label: "P2P Handshake" },
  { emoji: "🔵", label: "UI Redraw" },
  { emoji: "🟢", label: "Active Socket" },
  { emoji: "💾", label: "RAM Cache" },
  { emoji: "📦", label: "Payload" },
  { emoji: "🔍", label: "Audit Node" },
  { emoji: "⚙️", label: "System Service" },
  { emoji: "⏱️", label: "Mirror Time" },
  { emoji: "🔑", label: "Validation" },
]

const DEVICES_ITEMS = [
  // Inner ring items (exactly 18)
  { emoji: "🖥️", label: "Workstation-PC" },
  { emoji: "💻", label: "Laptop-Home" },
  { emoji: "💾", label: "Cloud Backup" },
  { emoji: "📱", label: "Mobile Client" },
  { emoji: "🖥️", label: "Work-Office" },
  { emoji: "💻", label: "Developer-Rig" },
  { emoji: "💾", label: "Firestore Sync" },
  { emoji: "📱", label: "Tablet Client" },
  { emoji: "🖥️", label: "Backup Host" },
  { emoji: "🌐", label: "Remote Node" },
  { emoji: "📡", label: "Local Agent" },
  { emoji: "🛡️", label: "Device Node 1" },
  { emoji: "🛡️", label: "Device Node 2" },
  { emoji: "🖥️", label: "Main Station" },
  { emoji: "💻", label: "Travel Laptop" },
  { emoji: "💾", label: "Sync Buffer" },
  { emoji: "📱", label: "Android Sync" },
  { emoji: "📦", label: "WPF Client 2" },
  // Outer ring items (exactly 22 items)
  { emoji: "🖥️", label: "Core Console" },
  { emoji: "💻", label: "Home Base" },
  { emoji: "💾", label: "Local JSON" },
  { emoji: "📡", label: "Web Broadcast" },
  { emoji: "🌐", label: "P2P Socket" },
  { emoji: "🖥️", label: "Workstation Node" },
  { emoji: "💻", label: "Secondary Node" },
  { emoji: "📡", label: "Sync Listener" },
  { emoji: "🔄", label: "Hot Syncer" },
  { emoji: "⏱️", label: "Ping Watcher" },
  { emoji: "📊", label: "Sync Telemetry" },
  { emoji: "🔍", label: "Node Auditor" },
  { emoji: "🟢", label: "Online Node" },
  { emoji: "🔵", label: "Active Sync" },
  { emoji: "⚙️", label: "System Daemon" },
  { emoji: "📦", label: "C# Process" },
  { emoji: "💾", label: "JSON Index" },
  { emoji: "🔌", label: "App Listener" },
  { emoji: "📜", label: "Manifest" },
  { emoji: "💬", label: "WPF Signal" },
  { emoji: "💡", label: "Daemon Status" },
  { emoji: "🔋", label: "Power Agent" },
]

const UTILITY_ITEMS = [
  // Inner ring items (exactly 18)
  { emoji: "🎨", label: "Hex Color Preview" },
  { emoji: "📊", label: "JSON Prettifier" },
  { emoji: "🌐", label: "Title Crawler" },
  { emoji: "📝", label: "Smart Detect" },
  { emoji: "📁", label: "Path Checker" },
  { emoji: "⚡", label: "Fast Hotkeys" },
  { emoji: "🔄", label: "Memory Trim" },
  { emoji: "💾", label: "JSON Vault" },
  { emoji: "📦", label: "Portable ZIP" },
  { emoji: "⏱️", label: "10s Undo" },
  { emoji: "💬", label: "P2P Stream" },
  { emoji: "🎹", label: "Text triggers" },
  { emoji: "📥", label: "QuickDrop Shelf" },
  { emoji: "🔍", label: "Instant Search" },
  { emoji: "⚙️", label: "WPF Dispatcher" },
  { emoji: "🖥️", label: "Low RAM Footprint" },
  { emoji: "📜", label: "Auto Clean Log" },
  { emoji: "💡", label: "Visual Helpers" },
  // Outer ring items (exactly 22 items)
  { emoji: "🎨", label: "Design Swatch" },
  { emoji: "📊", label: "Format Engine" },
  { emoji: "🌐", label: "HTML Scraper" },
  { emoji: "📝", label: "String Helper" },
  { emoji: "📁", label: "Explorer Sync" },
  { emoji: "⚡", label: "Low Latency" },
  { emoji: "🔄", label: "Cache Pruning" },
  { emoji: "💾", label: "Local History" },
  { emoji: "📦", label: "Backup MERGE" },
  { emoji: "⏱️", label: "Auto-Countdown" },
  { emoji: "💬", label: "LAN Broadcast" },
  { emoji: "🎹", label: "Expander Hook" },
  { emoji: "📥", label: "Floating Bubble" },
  { emoji: "🔍", label: "Query Filter" },
  { emoji: "⚙️", label: "Native Threading" },
  { emoji: "🖥️", label: "Process Tray" },
  { emoji: "💾", label: "JSON Index" },
  { emoji: "💡", label: "Hover Badge" },
  { emoji: "🔌", label: "Clipboard Hook" },
  { emoji: "🔧", label: "Settings Setup" },
  { emoji: "🔋", label: "Low Battery Drain" },
  { emoji: "🚀", label: "GPU Accel Rendering" },
]

const TABS = ["Texts", "Devices", "Utilities"]

export function UISection() {
  const [selectedPreset, setSelectedPreset] = useState(0)
  const [isExpanded, setIsExpanded] = useState(true)
  const [activeTab, setActiveTab] = useState(2)
  const [rotation, setRotation] = useState(0)
  const [hubMode, setHubMode] = useState<"stats" | "utilities">("stats")
  const [hoveredItem, setHoveredItem] = useState<string | null>(null)
  
  // ClipboardPro Specific Settings Hooks
  const [memoryLimit, setMemoryLimit] = useState(25)
  const [lanSync, setLanSync] = useState(true)
  const [memoryTrim, setMemoryTrim] = useState(true)
  const [smartDetect, setSmartDetect] = useState(true)
  const [hotkeyTrig, setHotkeyTrig] = useState(true)

  const [time, setTime] = useState({ hour: "10", minute: "43", period: "PM" })
  const [stats, setStats] = useState({ cpu: 0.1, ram: 8.2, latency: 45, network: "<100ms" })
  const [isPlaying, setIsPlaying] = useState(true)

  const preset = glassPresets[selectedPreset]
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const clockInterval = setInterval(() => {
      const now = new Date()
      setTime({
        hour: (now.getHours() % 12 || 12).toString().padStart(2, "0"),
        minute: now.getMinutes().toString().padStart(2, "0"),
        period: now.getHours() >= 12 ? "PM" : "AM"
      })
    }, 5000)

    // CPU & Memory oscillation simulation
    const telemetryInterval = setInterval(() => {
      setStats({
        cpu: Number((0.1 + Math.random() * 0.1).toFixed(2)),
        ram: Number((7.8 + Math.random() * 0.5).toFixed(1)),
        latency: Math.floor(40 + Math.random() * 25),
        network: `${Math.floor(20 + Math.random() * 80)}ms`
      })
    }, 4000)

    return () => {
      clearInterval(clockInterval)
      clearInterval(telemetryInterval)
    }
  }, [])

  // Rotate icons smoothly using scroll wheel and touch drag intercepters
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const handleWheelPrevent = (e: WheelEvent) => {
      e.preventDefault()
      const speed = 0.08
      const step = e.deltaY * speed
      setRotation(prev => (prev + step + 360) % 360)
    }

    let startY = 0

    const handleTouchStart = (e: TouchEvent) => {
      startY = e.touches[0].clientY
    }

    const handleTouchMove = (e: TouchEvent) => {
      if (startY === 0) return
      const currentY = e.touches[0].clientY
      const deltaY = currentY - startY
      
      if (isExpanded) {
        e.preventDefault()
      }
      
      const speed = 0.45
      setRotation(prev => (prev - deltaY * speed + 360) % 360)
      startY = currentY
    }

    const handleTouchEnd = () => {
      startY = 0
    }

    container.addEventListener("wheel", handleWheelPrevent, { passive: false })
    container.addEventListener("touchstart", handleTouchStart, { passive: true })
    container.addEventListener("touchmove", handleTouchMove, { passive: false })
    container.addEventListener("touchend", handleTouchEnd, { passive: true })

    return () => {
      container.removeEventListener("wheel", handleWheelPrevent)
      container.removeEventListener("touchstart", handleTouchStart)
      container.removeEventListener("touchmove", handleTouchMove)
      container.removeEventListener("touchend", handleTouchEnd)
    }
  }, [isExpanded])

  const getTabItems = () => {
    switch (activeTab) {
      case 0: return TEXTS_ITEMS
      case 1: return DEVICES_ITEMS
      case 2: return UTILITY_ITEMS
      default: return UTILITY_ITEMS
    }
  }

  const tabItems = getTabItems()
  const innerLimit = 18
  const innerItems = tabItems.slice(0, innerLimit)
  const outerItems = tabItems.slice(innerLimit)

  // Floating helper rounding
  const r = (v: number) => Number(v.toFixed(2))

  const getWedgePath = (index: number, r1: number, r2: number) => {
    const cx = 0
    const cy = 250
    const startAngle = -90 + (index * 60)
    const endAngle = -90 + ((index + 1) * 60)
    const radStart = (startAngle * Math.PI) / 180
    const radEnd = (endAngle * Math.PI) / 180
    
    const x1_in = r(cx + r1 * Math.cos(radStart))
    const y1_in = r(cy + r1 * Math.sin(radStart))
    const x1_out = r(cx + r2 * Math.cos(radStart))
    const y1_out = r(cy + r2 * Math.sin(radStart))
    
    const x2_in = r(cx + r1 * Math.cos(radEnd))
    const y2_in = r(cy + r1 * Math.sin(radEnd))
    const x2_out = r(cx + r2 * Math.cos(radEnd))
    const y2_out = r(cy + r2 * Math.sin(radEnd))
    
    return `M ${x1_in} ${y1_in} L ${x1_out} ${y1_out} A ${r2} ${r2} 0 0 1 ${x2_out} ${y2_out} L ${x2_in} ${y2_in} A ${r1} ${r1} 0 0 0 ${x1_in} ${y1_in} Z`
  }

  return (
    <section className="relative py-32 px-4 overflow-hidden bg-slate-950/20">
      {/* Background gradients */}
      <div className="absolute inset-0">
        <div className="absolute top-1/2 left-1/4 w-[600px] h-[600px] rounded-full bg-indigo-900/5 blur-[120px] pointer-events-none" />
        <div className="absolute top-1/3 right-1/4 w-[600px] h-[600px] rounded-full bg-violet-900/5 blur-[120px] pointer-events-none" />
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
            <span className="inline-block px-4 py-1.5 rounded-full glass-card text-xs font-semibold uppercase tracking-wider text-indigo-300 mb-4 border border-indigo-500/20">
              Interactive Diagnostics
            </span>
          </motion.div>
          <motion.h2
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="text-4xl md:text-5xl font-extrabold leading-tight tracking-tight text-white mb-6 text-balance"
          >
            Fully-Wired Control System
          </motion.h2>
          <motion.p
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="text-base text-muted-foreground leading-relaxed text-pretty"
          >
            Drag and scroll the radial selector simulator to interact with live active device arrays. 
            Toggle client features on the right panel to instantly watch performance parameters update.
          </motion.p>
        </div>

        {/* Dynamic Interactive Box Grid */}
        <div className="grid lg:grid-cols-12 gap-8 items-center max-w-5xl mx-auto">
          {/* LEFT: Radial dial Simulator (takes 7 columns) */}
          <div className="lg:col-span-7 flex justify-center relative">
            
            {/* Visualizer blur circle backplate */}
            <div 
              className="absolute w-[420px] h-[420px] rounded-full blur-[80px] opacity-15 pointer-events-none"
              style={{ background: preset.accent }}
            />

            <AnimatePresence>
              {isExpanded && (
                <motion.div
                  ref={containerRef}
                  initial={{ opacity: 0, scale: 0.95 }}
                  whileInView={{ opacity: 1, scale: 1 }}
                  viewport={{ once: true }}
                  transition={{ duration: 0.8 }}
                  className="relative rounded-[28px] p-1 bg-slate-950/70 border border-white/10 backdrop-blur-2xl shadow-2xl overflow-hidden w-full max-w-[380px] h-[500px] select-none cursor-ns-resize"
                  style={{
                    background: "linear-gradient(135deg, #050511 0%, #0b0f24 50%, #050512 100%)",
                  }}
                >
                  <svg 
                    viewBox="0 0 380 500" 
                    className="absolute inset-0 w-full h-full"
                    style={{ filter: `drop-shadow(0 0 30px ${preset.accent}35)` }}
                  >
                    <defs>
                      <clipPath id="halfCircleClip">
                        <rect x="0" y="0" width="250" height="500" />
                      </clipPath>
                      <radialGradient id={`bgGrad-${selectedPreset}`} cx="0%" cy="50%" r="100%">
                        <stop offset="0%" stopColor={preset.accent} stopOpacity="0.4" />
                        <stop offset="60%" stopColor={preset.accent} stopOpacity="0.18" />
                        <stop offset="100%" stopColor={preset.accent} stopOpacity="0.03" />
                      </radialGradient>
                    </defs>

                    {/* Glass plate backdrop */}
                    <circle
                      cx="0"
                      cy="250"
                      r="180"
                      fill={preset.bg}
                      stroke="none"
                      clipPath="url(#halfCircleClip)"
                      style={{ filter: "blur(0.5px)" }}
                    />
                    <circle
                      cx="0"
                      cy="250"
                      r="180"
                      fill={`url(#bgGrad-${selectedPreset})`}
                      clipPath="url(#halfCircleClip)"
                    />

                    {/* Solid Inner Ring Path (Ri = 120px) */}
                    <circle
                      cx="0"
                      cy="250"
                      r="120"
                      fill="none"
                      stroke={preset.accent}
                      strokeWidth="1.5"
                      opacity="0.35"
                      clipPath="url(#halfCircleClip)"
                    />

                    {/* Solid Outer Ring Path (Ro = 180px) */}
                    <circle
                      cx="0"
                      cy="250"
                      r="180"
                      fill="none"
                      stroke={preset.accent}
                      strokeWidth="1.8"
                      opacity="0.5"
                      clipPath="url(#halfCircleClip)"
                    />

                    {/* Hub circle (dashboard area) */}
                    <circle
                      cx="0"
                      cy="250"
                      r="50"
                      fill="rgba(6, 4, 20, 0.85)"
                      stroke={preset.border}
                      strokeWidth="1.5"
                      clipPath="url(#halfCircleClip)"
                    />

                    {/* Radial connection lines */}
                    {[-70, -45, -20, 0, 20, 45, 70].map((angle, i) => {
                      const rad = (angle * Math.PI) / 180
                      return (
                        <line
                          key={i}
                          x1={r(Math.cos(rad) * 50)}
                          y1={r(250 + Math.sin(rad) * 50)}
                          x2={r(Math.cos(rad) * 180)}
                          y2={r(250 + Math.sin(rad) * 180)}
                          stroke={preset.border}
                          strokeWidth="0.5"
                          opacity="0.12"
                        />
                      )
                    })}

                    {/* 3 Wedges Selector Ring */}
                    {TABS.map((tab, i) => {
                      const isActive = i === activeTab
                      const baseAngle = -60 + (i * 60)
                      const rad = (baseAngle * Math.PI) / 180
                      const tx = r(Math.cos(rad) * 65)
                      const ty = r(250 + Math.sin(rad) * 65)

                      return (
                        <g 
                          key={tab} 
                          className="group cursor-pointer select-none" 
                          onClick={() => setActiveTab(i)}
                        >
                          <path
                            d={getWedgePath(i, 50, 80)}
                            fill={isActive ? `${preset.accent}35` : "rgba(6, 4, 20, 0.4)"}
                            stroke={preset.border}
                            strokeWidth="1.2"
                            className="transition-all duration-300 group-hover:fill-indigo-500/20"
                          />
                          <text
                            x={tx}
                            y={ty}
                            fill={isActive ? "#ffffff" : "rgba(255, 255, 255, 0.5)"}
                            fontSize={isActive ? "9" : "8"}
                            fontWeight={isActive ? "600" : "400"}
                            textAnchor="middle"
                            dominantBaseline="middle"
                            transform={`rotate(${baseAngle + 90}, ${tx}, ${ty})`}
                            className="transition-all duration-200"
                            style={{
                              filter: isActive ? `drop-shadow(0 0 5px ${preset.accent})` : "none",
                            }}
                          >
                            {tab}
                          </text>
                        </g>
                      )
                    })}
                  </svg>

                  {/* Clickable Dashboard inside hub */}
                  <div 
                    className="absolute flex flex-col items-center justify-center text-white text-center select-none cursor-pointer hover:bg-white/5 rounded-full p-1 transition-all duration-300 animate-fade-in"
                    style={{
                      left: "0px",
                      top: "50%",
                      transform: "translateY(-50%)",
                      width: "48px",
                      height: "85px",
                    }}
                    onClick={() => setHubMode(prev => prev === "stats" ? "utilities" : "stats")}
                    title="Click to toggle Hub Mode!"
                  >
                    {hubMode === "stats" ? (
                      <div className="flex flex-col items-center justify-center w-full">
                        <div className="text-[7.5px] text-emerald-400 font-bold flex items-center gap-0.5 animate-pulse">
                          <span>⚡</span>
                          <span>Sync</span>
                        </div>
                        
                        <div className="text-[6.5px] text-white/55 font-bold uppercase tracking-wider mt-0.5">
                          ACTIVE
                        </div>

                        <div className="text-xs font-extrabold leading-none text-white my-1 select-none">
                          {time.hour}:{time.minute}
                        </div>

                        <div className="w-8 h-[0.5px] bg-white/20 my-0.5" />

                        <div className="text-[6.5px] text-white/80 font-medium space-y-0.5">
                          <div>RAM {memoryTrim ? (stats.ram - 2).toFixed(1) : stats.ram}MB</div>
                          <div>CPU {stats.cpu}%</div>
                        </div>

                        <div className="w-8 h-[0.5px] bg-white/20 my-0.5" />

                        <div className="text-[6.5px] text-indigo-400 font-semibold flex items-center gap-0.5">
                          <span>📶</span>
                          <span>{stats.network}</span>
                        </div>
                      </div>
                    ) : (
                      <div className="flex flex-col items-center justify-center w-full px-0.5" onClick={(e) => e.stopPropagation()}>
                        <div className="w-4 h-4 rounded-full border border-indigo-400/50 flex items-center justify-center bg-black/40 mb-1">
                          <span className="text-[9px]">💾</span>
                        </div>
                        
                        <div 
                          className="text-[6.5px] font-extrabold text-white w-10 truncate text-center select-none mb-0.5 cursor-help"
                          title="Local JSON Database"
                          onClick={() => setHubMode("stats")}
                        >
                          JSON
                        </div>

                        <div className="w-8 h-[2px] bg-white/20 rounded-full my-1 relative cursor-pointer group">
                          <motion.div 
                            className="absolute left-0 top-0 h-full"
                            style={{ backgroundColor: preset.accent }}
                            animate={isPlaying ? { width: ["20%", "90%", "20%"] } : {}}
                            transition={{ duration: 10, ease: "linear", repeat: Infinity }}
                          />
                          <motion.div 
                            className="absolute w-1 h-1 rounded-full bg-white -top-[1px] shadow-sm shadow-black"
                            animate={isPlaying ? { left: ["20%", "90%", "20%"] } : {}}
                            transition={{ duration: 10, ease: "linear", repeat: Infinity }}
                            style={{ marginLeft: "-2px" }}
                          />
                        </div>
                        
                        <div className="text-[4.5px] text-white/40 tracking-wider mb-0.5">SYNC ON</div>

                        <div className="flex items-center justify-between gap-1.5 my-0.5">
                          <span className="text-[6px] text-white/70">P2P</span>
                          <span className="text-[7px] text-indigo-400">⚡</span>
                          <span className="text-[6px] text-white/70">WPF</span>
                        </div>

                        {/* Animated spectrum visualizer */}
                        <div className="flex items-end gap-[1px] h-2.5 mt-0.5 overflow-hidden w-9 justify-center">
                          {[...Array(6)].map((_, idx) => (
                            <motion.div
                              key={idx}
                              className="w-[1.5px] rounded-t"
                              style={{ backgroundColor: preset.accent }}
                              animate={isPlaying ? {
                                height: [
                                  "1px", 
                                  `${Math.floor(Math.random() * 8) + 2}px`, 
                                  "1px"
                                ]
                              } : { height: "1.5px" }}
                              transition={{
                                duration: 0.35 + idx * 0.08,
                                repeat: Infinity,
                                ease: "easeInOut"
                              }}
                              style={{ height: "1.5px" }}
                            />
                          ))}
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Inner Ring Icons */}
                  <AnimatePresence>
                    {innerItems.map((item, i) => {
                      const innerStep = 360 / innerItems.length
                      let a_norm = (i * innerStep + rotation + 180) % 360 - 180
                      const d = Math.abs(a_norm)
                      
                      let opacity = 1
                      if (d < 78) opacity = 1
                      else if (d > 102) opacity = 0
                      else opacity = (102 - d) / 24.0

                      if (opacity <= 0) return null

                      const rad = (a_norm * Math.PI) / 180
                      const radius = 120
                      const x = Math.cos(rad) * radius
                      const y = 250 + Math.sin(rad) * radius
                      const isHovered = hoveredItem === `inner-${i}`

                      return (
                        <motion.div
                          key={`${activeTab}-inner-${i}`}
                          className="absolute flex flex-col items-center cursor-pointer"
                          style={{ left: 0, top: 0, opacity }}
                          onMouseEnter={() => setHoveredItem(`inner-${i}`)}
                          onMouseLeave={() => setHoveredItem(null)}
                          whileHover={{ scale: 1.15 }}
                          initial={{ opacity: 0, scale: 0 }}
                          animate={{ 
                            opacity, 
                            scale: 1, 
                            x: x - 16, 
                            y: y - 16 
                          }}
                          exit={{ opacity: 0, scale: 0 }}
                          transition={{ type: "spring", damping: 20, stiffness: 150 }}
                        >
                          <div
                            className="w-8 h-8 rounded-full flex items-center justify-center text-base transition-all duration-200"
                            style={{
                              background: isHovered 
                                ? `linear-gradient(135deg, ${preset.accent}80, ${preset.accent}40)`
                                : preset.bg,
                              border: `1.5px solid ${isHovered ? preset.border : preset.accent + "50"}`,
                              boxShadow: isHovered ? `0 0 12px ${preset.accent}50` : `0 2px 6px rgba(0,0,0,0.3)`,
                            }}
                          >
                            {item.emoji}
                          </div>
                          <span 
                            className="text-[6px] mt-0.5 whitespace-nowrap font-medium pointer-events-none select-none"
                            style={{ color: isHovered ? preset.accent : "rgba(255,255,255,0.6)" }}
                          >
                            {item.label}
                          </span>
                        </motion.div>
                      )
                    })}
                  </AnimatePresence>

                  {/* Outer Ring Icons */}
                  <AnimatePresence>
                    {outerItems.map((item, i) => {
                      const outerStep = 360 / outerItems.length
                      let a_norm = (i * outerStep + rotation + 180) % 360 - 180
                      const d = Math.abs(a_norm)
                      
                      let opacity = 1
                      if (d < 78) opacity = 1
                      else if (d > 102) opacity = 0
                      else opacity = (102 - d) / 24.0

                      if (opacity <= 0) return null

                      const rad = (a_norm * Math.PI) / 180
                      const radius = 180
                      const x = Math.cos(rad) * radius
                      const y = 250 + Math.sin(rad) * radius
                      const isHovered = hoveredItem === `outer-${i}`

                      return (
                        <motion.div
                          key={`${activeTab}-outer-${i}`}
                          className="absolute flex flex-col items-center cursor-pointer"
                          style={{ left: 0, top: 0, opacity }}
                          onMouseEnter={() => setHoveredItem(`outer-${i}`)}
                          onMouseLeave={() => setHoveredItem(null)}
                          whileHover={{ scale: 1.15 }}
                          initial={{ opacity: 0, scale: 0 }}
                          animate={{ 
                            opacity, 
                            scale: 1, 
                            x: x - 20, 
                            y: y - 20 
                          }}
                          exit={{ opacity: 0, scale: 0 }}
                          transition={{ type: "spring", damping: 20, stiffness: 150 }}
                        >
                          <div
                            className="w-10 h-10 rounded-full flex items-center justify-center text-lg transition-all duration-200"
                            style={{
                              background: isHovered 
                                ? `linear-gradient(135deg, ${preset.accent}80, ${preset.accent}40)`
                                : preset.bg,
                              border: `2px solid ${isHovered ? preset.border : preset.accent + "60"}`,
                              boxShadow: isHovered ? `0 0 16px ${preset.accent}60` : `0 3px 10px rgba(0,0,0,0.3)`,
                            }}
                          >
                            {item.emoji}
                          </div>
                          <span 
                            className="text-[7px] mt-0.5 whitespace-nowrap font-medium pointer-events-none select-none"
                            style={{ color: isHovered ? preset.accent : "rgba(255,255,255,0.6)" }}
                          >
                            {item.label}
                          </span>
                        </motion.div>
                      )
                    })}
                  </AnimatePresence>
                </motion.div>
              )}
            </AnimatePresence>

            {/* Scroll Instruction Overlay */}
            <div className="absolute top-4 left-1/2 -translate-x-1/2 text-[9px] text-white/40 flex items-center gap-1 bg-black/30 px-2 py-1 rounded border border-white/5 backdrop-blur pointer-events-none select-none z-20">
              <span>🖱️</span>
              <span>Scroll wheel or drag vertical edge to rotate</span>
            </div>
          </div>

          {/* RIGHT: Integrated Settings & Diagnostics Control Dashboard (takes 5 columns) */}
          <div className="lg:col-span-5 w-full">
            <div className="w-full bg-slate-950/60 border border-white/10 backdrop-blur-xl rounded-[24px] p-6 flex flex-col gap-6 shadow-2xl">
              
              {/* Header */}
              <div className="flex items-center gap-2 text-xs font-extrabold uppercase tracking-widest text-indigo-300 border-b border-white/10 pb-3 select-none">
                <Sliders className="w-4 h-4" />
                <span>Diagnostics Settings</span>
              </div>
              
              {/* Slider for local memory limit buffer (5MB to 50MB) */}
              <div className="flex flex-col gap-2">
                <div className="flex justify-between items-center text-xs">
                  <span className="font-bold uppercase tracking-wider text-white/50">Memory Limit Buffer</span>
                  <span className="font-mono text-indigo-400 font-bold">{memoryLimit} MB</span>
                </div>
                <input 
                  type="range" 
                  min="5" 
                  max="50" 
                  value={memoryLimit} 
                  onChange={(e) => setMemoryLimit(Number(e.target.value))}
                  className="w-full h-1.5 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-indigo-500"
                />
                <span className="text-[10px] text-white/40 leading-snug">
                  Allocates maximum offline JSON & Cache buffer footprint size before automated cache rotation begins.
                </span>
              </div>

              {/* Toggles Panel */}
              <div className="flex flex-col gap-3">
                <span className="text-[10px] font-bold uppercase tracking-wider text-white/50">Performance Parameters</span>
                
                {/* P2P Sync */}
                <div className="flex items-center justify-between p-2.5 rounded-xl bg-white/5 border border-white/5">
                  <div className="flex items-center gap-2">
                    <Radio className="w-3.5 h-3.5 text-indigo-400" />
                    <span className="text-xs font-semibold text-white/80">P2P LAN Syncing</span>
                  </div>
                  <button onClick={() => setLanSync(!lanSync)} className="text-indigo-400">
                    {lanSync ? <ToggleRight className="w-8 h-8" /> : <ToggleLeft className="w-8 h-8 text-white/40" />}
                  </button>
                </div>

                {/* Memory Trimmer */}
                <div className="flex items-center justify-between p-2.5 rounded-xl bg-white/5 border border-white/5">
                  <div className="flex items-center gap-2">
                    <Cpu className="w-3.5 h-3.5 text-indigo-400" />
                    <span className="text-xs font-semibold text-white/80">Memory Trimming Engine</span>
                  </div>
                  <button onClick={() => setMemoryTrim(!memoryTrim)} className="text-indigo-400">
                    {memoryTrim ? <ToggleRight className="w-8 h-8" /> : <ToggleLeft className="w-8 h-8 text-white/40" />}
                  </button>
                </div>

                {/* Smart Detect */}
                <div className="flex items-center justify-between p-2.5 rounded-xl bg-white/5 border border-white/5">
                  <div className="flex items-center gap-2">
                    <Database className="w-3.5 h-3.5 text-indigo-400" />
                    <span className="text-xs font-semibold text-white/80">Smart Type Detection</span>
                  </div>
                  <button onClick={() => setSmartDetect(!smartDetect)} className="text-indigo-400">
                    {smartDetect ? <ToggleRight className="w-8 h-8" /> : <ToggleLeft className="w-8 h-8 text-white/40" />}
                  </button>
                </div>

                {/* Hotkey Triggers */}
                <div className="flex items-center justify-between p-2.5 rounded-xl bg-white/5 border border-white/5">
                  <div className="flex items-center gap-2">
                    <Sliders className="w-3.5 h-3.5 text-indigo-400" />
                    <span className="text-xs font-semibold text-white/80">Global Hotkey Hooks</span>
                  </div>
                  <button onClick={() => setHotkeyTrig(!hotkeyTrig)} className="text-indigo-400">
                    {hotkeyTrig ? <ToggleRight className="w-8 h-8" /> : <ToggleLeft className="w-8 h-8 text-white/40" />}
                  </button>
                </div>
              </div>

              {/* Swatch Preset theme selector */}
              <div className="flex flex-col gap-2 border-t border-white/10 pt-4">
                <span className="text-[10px] font-bold uppercase tracking-wider text-white/50">WPF Interface Color Accent</span>
                <div className="grid grid-cols-5 gap-2">
                  {glassPresets.slice(0, 5).map((p, i) => (
                    <button
                      key={p.name}
                      onClick={() => setSelectedPreset(i)}
                      className={`h-8 rounded-lg transition-all duration-200 relative group cursor-pointer ${
                        selectedPreset === i ? "ring-2 ring-white ring-offset-1 ring-offset-background scale-105" : "opacity-60 hover:opacity-100"
                      }`}
                      style={{
                        background: `linear-gradient(135deg, ${p.accent}, ${p.bg})`,
                        border: `1.5px solid ${p.border}`,
                      }}
                      title={p.name}
                    />
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Feature Highlights Grid at bottom */}
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ delay: 0.2 }}
          className="mt-20 grid grid-cols-2 md:grid-cols-4 gap-4 max-w-4xl mx-auto"
        >
          {[
            { title: "Multi-Format Vault", desc: "Categorized search and storage for all copied elements" },
            { title: "Low Memory Footprint", desc: "Runs with low memory consumption and automated trimming" },
            { title: "Smart Utility Helpers", desc: "JSON prettifying, Hex previews, and webpage title crawling" },
            { title: "QuickDrop Shelf", desc: "Floating temporary screen-edge dock to compile files" },
          ].map((feat, i) => (
            <div 
              key={i}
              className="p-4 rounded-xl text-center"
              style={{
                background: `linear-gradient(135deg, ${preset.bg}, transparent)`,
                border: `1px solid ${preset.accent}30`,
              }}
            >
              <h4 className="font-semibold text-xs text-foreground mb-1">{feat.title}</h4>
              <p className="text-[10px] text-muted-foreground leading-normal">{feat.desc}</p>
            </div>
          ))}
        </motion.div>
      </div>
    </section>
  )
}
