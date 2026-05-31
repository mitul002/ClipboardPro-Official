"use client"

import { motion } from "framer-motion"
import { Code2, Briefcase, Sparkles, Layers } from "lucide-react"

const useCases = [
  {
    icon: Code2,
    title: "Developers",
    description: "Sync complex code blocks, terminal commands, and raw JSON packets between local workstations and servers instantly.",
    color: "text-blue-400",
    bg: "bg-blue-500/10",
    border: "border-blue-500/20",
  },
  {
    icon: Briefcase,
    title: "Remote Workers",
    description: "Move rich text templates, documents, and link lists between workspace machines and laptops in under 100ms.",
    color: "text-indigo-400",
    bg: "bg-indigo-500/10",
    border: "border-indigo-500/20",
  },
  {
    icon: Layers,
    title: "Writers & Creators",
    description: "Stash templates, article drafts, and research snippets in the drag & drop shelf to organize all ideas in one place.",
    color: "text-emerald-400",
    bg: "bg-emerald-500/10",
    border: "border-emerald-500/20",
  },
  {
    icon: Sparkles,
    title: "Power Users",
    description: "Customize global system hotkeys, define text abbreviations, and ensure low memory consumption during background operation.",
    color: "text-violet-400",
    bg: "bg-violet-500/10",
    border: "border-violet-500/20",
  },
]

export function UseCasesSection() {
  return (
    <section className="relative py-32 px-4 overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0 bg-gradient-to-b from-indigo-950/10 via-background to-background" />
      
      {/* Decorative orbs */}
      <div className="absolute top-1/3 right-0 w-[400px] h-[400px] bg-indigo-600/10 rounded-full blur-[100px] translate-x-1/2" />
      <div className="absolute bottom-1/3 left-0 w-[400px] h-[400px] bg-violet-600/10 rounded-full blur-[100px] -translate-x-1/2" />

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
            Use Cases
          </span>
          <h2 className="text-4xl md:text-5xl font-bold mb-6">
            <span className="text-foreground">Perfect For</span>
            <br />
            <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">
              Every Workflow
            </span>
          </h2>
        </motion.div>

        {/* Use case cards */}
        <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
          {useCases.map((useCase, index) => (
            <motion.div
              key={useCase.title}
              initial={{ opacity: 0, y: 30 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.6, delay: index * 0.1 }}
              whileHover={{ y: -8 }}
              className="group"
            >
              <div className={`relative p-6 rounded-2xl glass-card h-full border ${useCase.border} hover:border-indigo-500/40 transition-all duration-300`}>
                {/* Icon */}
                <div className={`w-12 h-12 rounded-xl ${useCase.bg} flex items-center justify-center mb-5`}>
                  <useCase.icon className={`w-6 h-6 ${useCase.color}`} />
                </div>

                {/* Content */}
                <h3 className="text-lg font-semibold text-foreground mb-2 group-hover:text-indigo-300 transition-colors">
                  {useCase.title}
                </h3>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  {useCase.description}
                </p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}

