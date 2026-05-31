"use client"

import { motion } from "framer-motion"
import { 
  Cpu, 
  Database, 
  EyeOff, 
  Keyboard, 
  Zap,
  Sparkles
} from "lucide-react"

const advancedFeatures = [
  {
    icon: Zap,
    title: "Instant Local Sync",
    description: "Fast, direct computer-to-computer sync on your local network.",
  },
  {
    icon: Cpu,
    title: "Instant Set Up",
    description: "Activate instantly with a simple licensing key.",
  },
  {
    icon: Database,
    title: "Smart Memory Control",
    description: "Saves large items directly to your disk to keep system RAM low.",
  },
  {
    icon: EyeOff,
    title: "Sensitive Masking",
    description: "Automatically hides passwords and API keys from view.",
  },
  {
    icon: Keyboard,
    title: "Custom Hotkeys",
    description: "Register custom hotkeys to access clipboard history instantly.",
  },
  {
    icon: Sparkles,
    title: "Self-Cleaning Cache",
    description: "Auto-cleans old items and prunes image history to save space.",
  },
]

export function AdvancedSection() {
  return (
    <section className="relative py-32 px-4 overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-br from-indigo-950/20 via-background to-violet-950/20" />
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[400px] bg-indigo-600/10 rounded-full blur-[120px]" />
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
          <span className="inline-block px-4 py-1.5 rounded-full glass-card text-sm text-indigo-300 mb-4 border border-indigo-500/20">
            Engineered for Speed
          </span>
          <h2 className="text-4xl md:text-5xl font-bold mb-6">
            <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">
              Under the Hood Performance
            </span>
          </h2>
        </motion.div>

        {/* Features Grid */}
        <motion.div
          initial={{ opacity: 0, y: 40 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-50px" }}
          transition={{ duration: 0.8, delay: 0.2 }}
          className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4"
        >
          {advancedFeatures.map((feature, index) => (
            <motion.div
              key={feature.title}
              initial={{ opacity: 0, scale: 0.9 }}
              whileInView={{ opacity: 1, scale: 1 }}
              viewport={{ once: true }}
              transition={{ duration: 0.5, delay: index * 0.1 }}
              whileHover={{ scale: 1.05, y: -5 }}
              className="group"
            >
              <div className="relative p-6 rounded-2xl glass-card text-center h-full hover:border-indigo-500/30 transition-all duration-300">
                {/* Glow effect */}
                <div className="absolute inset-0 rounded-2xl bg-gradient-to-br from-indigo-500/10 to-violet-500/10 opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
                
                <div className="relative">
                  <div className="w-12 h-12 mx-auto mb-4 rounded-xl bg-gradient-to-br from-indigo-500/20 to-violet-500/20 flex items-center justify-center group-hover:from-indigo-500/30 group-hover:to-violet-500/30 transition-all duration-300">
                    <feature.icon className="w-6 h-6 text-indigo-400 group-hover:text-indigo-300 transition-colors" />
                  </div>
                  <h3 className="font-semibold text-foreground text-sm mb-1 group-hover:text-indigo-300 transition-colors">
                    {feature.title}
                  </h3>
                  <p className="text-[10px] text-muted-foreground leading-snug">
                    {feature.description}
                  </p>
                </div>
              </div>
            </motion.div>
          ))}
        </motion.div>
      </div>
    </section>
  )
}
