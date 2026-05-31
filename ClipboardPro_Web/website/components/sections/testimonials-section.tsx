"use client"

import { motion } from "framer-motion"
import { useEffect, useRef } from "react"
import { Star, Quote } from "lucide-react"

const testimonials = [
  {
    name: "Alex K.",
    role: "DevOps Engineer",
    content: "ClipboardPro changed how I deploy. Syncing environment variables and tokens securely between office machines takes milliseconds.",
    rating: 5,
  },
  {
    name: "Sarah M.",
    role: "Product Designer",
    content: "The screen-edge QuickDrop shelf is fantastic. Gathering layout references and drag-and-dropping UI assets directly between design files speeds up my work immensely.",
    rating: 5,
  },
  {
    name: "David L.",
    role: "Software Engineer",
    content: "The native WPF engine is incredibly optimized. Running under 8MB of RAM in my tray makes Electron-based tools look bloated.",
    rating: 5,
  },
  {
    name: "Emma R.",
    role: "Data Analyst",
    content: "Regex credential masking is a life saver. It automatically filters credit card info and passwords from accidental clipboard syncs.",
    rating: 5,
  },
  {
    name: "Michael T.",
    role: "Systems Administrator",
    content: "Deploying the C# WPF installer takes seconds. It integrates beautifully on Windows 10/11 with zero background performance overhead.",
    rating: 5,
  },
  {
    name: "Lisa W.",
    role: "Technical Writer",
    content: "The offline-first local JSON storage engine is brilliant. Even with thousands of items, fuzzy search works instantly with zero delay.",
    rating: 5,
  },
]

function TestimonialCard({ testimonial, index }: { testimonial: typeof testimonials[0]; index: number }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true }}
      transition={{ duration: 0.5, delay: index * 0.1 }}
      className="group relative min-w-[320px] md:min-w-[380px]"
    >
      <div className="relative p-6 rounded-2xl glass-card h-full hover:border-indigo-500/30 transition-all duration-500">
        {/* Quote icon */}
        <div className="absolute top-4 right-4 opacity-10 group-hover:opacity-20 transition-opacity">
          <Quote className="w-10 h-10 text-indigo-400" />
        </div>

        {/* Rating */}
        <div className="flex gap-1 mb-4">
          {[...Array(testimonial.rating)].map((_, i) => (
            <Star key={i} className="w-4 h-4 fill-indigo-400 text-indigo-400" />
          ))}
        </div>

        {/* Content */}
        <p className="text-muted-foreground leading-relaxed mb-6 text-pretty">
          &ldquo;{testimonial.content}&rdquo;
        </p>

        {/* Author */}
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-gradient-to-br from-indigo-500 to-violet-600 flex items-center justify-center text-foreground font-bold">
            {testimonial.name[0]}
          </div>
          <div>
            <div className="font-semibold text-foreground text-sm">{testimonial.name}</div>
            <div className="text-xs text-muted-foreground">{testimonial.role}</div>
          </div>
        </div>
      </div>
    </motion.div>
  )
}

export function TestimonialsSection() {
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const scroll = scrollRef.current
    if (!scroll) return

    let animationId: number
    let position = 0

    const animate = () => {
      position += 0.5
      if (position >= scroll.scrollWidth / 2) {
        position = 0
      }
      scroll.scrollLeft = position
      animationId = requestAnimationFrame(animate)
    }

    animationId = requestAnimationFrame(animate)

    const handleMouseEnter = () => cancelAnimationFrame(animationId)
    const handleMouseLeave = () => {
      animationId = requestAnimationFrame(animate)
    }

    scroll.addEventListener("mouseenter", handleMouseEnter)
    scroll.addEventListener("mouseleave", handleMouseLeave)

    return () => {
      cancelAnimationFrame(animationId)
      scroll.removeEventListener("mouseenter", handleMouseEnter)
      scroll.removeEventListener("mouseleave", handleMouseLeave)
    }
  }, [])

  return (
    <section id="reviews" className="relative py-32 overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-b from-background via-indigo-950/5 to-background" />
      </div>

      <div className="relative z-10">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: "-100px" }}
          transition={{ duration: 0.8 }}
          className="text-center mb-16 px-4"
        >
          <span className="inline-block px-4 py-1.5 rounded-full glass-card text-sm text-indigo-300 mb-4">
            Loved by Teams
          </span>
          <h2 className="text-4xl md:text-5xl font-bold mb-6">
            <span className="text-foreground">What Users</span>{" "}
            <span className="bg-gradient-to-r from-indigo-400 to-violet-400 bg-clip-text text-transparent">
              Are Saying
            </span>
          </h2>
        </motion.div>

        {/* Scrolling testimonials */}
        <div
          ref={scrollRef}
          className="flex gap-6 overflow-x-auto scrollbar-hide px-4"
          style={{ scrollBehavior: "auto" }}
        >
          {/* Double the testimonials for infinite scroll effect */}
          {[...testimonials, ...testimonials].map((testimonial, index) => (
            <TestimonialCard key={index} testimonial={testimonial} index={index % testimonials.length} />
          ))}
        </div>

        {/* Gradient fades */}
        <div className="absolute left-0 top-1/2 -translate-y-1/2 w-32 h-full bg-gradient-to-r from-background to-transparent pointer-events-none z-10" />
        <div className="absolute right-0 top-1/2 -translate-y-1/2 w-32 h-full bg-gradient-to-l from-background to-transparent pointer-events-none z-10" />
      </div>
    </section>
  )
}

