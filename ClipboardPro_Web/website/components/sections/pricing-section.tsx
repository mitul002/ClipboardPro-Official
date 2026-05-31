"use client"

import { useState } from "react"
import { motion, AnimatePresence } from "framer-motion"
import { Check, ShieldCheck, Sparkles, Key, ArrowRight, Copy, CheckSquare, Loader2, Download } from "lucide-react"
import { Button } from "@/components/ui/button"
import Script from "next/script"

const PLANS = [
  {
    name: "1-Month Free Trial",
    price: "0",
    period: "Month",
    desc: "Test the native WPF C# desktop suite and high-speed local clipboard sync channel completely free for 30 days.",
    features: [
      "Access to full desktop clipboard history",
      "P2P local sync under 100ms",
      "Offline local JSON & SSD cache",
      "Download and run instantly",
      "Smart privacy history filters",
      "No credit card or registration required",
    ],
    cta: "Download Trial",
    isTrial: true,
    featured: false,
    badge: "30 Days Free",
  },
  {
    name: "Yearly Pro License",
    price: "9.99",
    period: "Year",
    desc: "Get an entire year of high-speed clipboard synchronization, priority updates, and dedicated customer support.",
    features: [
      "Unlimited device sync streams",
      "Offline local JSON & SSD cache",
      "Instant history filter queries",
      "Smart privacy masking system",
      "Activate on up to 2 workstations",
      "Continuous WPF runtime updates",
      "Priority customer helpdesk",
    ],
    cta: "Subscribe Yearly",
    href: "https://crosstech.lemonsqueezy.com/checkout/buy/0e946ba1-a181-4747-a3ee-3719a41cbbb0?enabled=1716270",
    featured: false,
    badge: "Best Value",
  },
  {
    name: "Lifetime License",
    price: "19.99",
    period: "One-Time",
    desc: "Permanently unlock ClipboardPro on all your devices with lifetime updates and no recurring subscription fees.",
    features: [
      "Lifetime unlimited sync channels",
      "Activate on up to 3 workstations",
      "Offline local JSON & SSD cache",
      "Instant history filter queries",
      "Smart privacy history filter",
      "Permanent developer update track",
      "Priority synchronization routing",
    ],
    cta: "Get Lifetime Access",
    href: "https://crosstech.lemonsqueezy.com/checkout/buy/b6bafa95-126c-4c5f-82ab-4b0d4a3f5e7f?enabled=1716321",
    featured: true,
    badge: "Highly Recommended",
  },
]

export function PricingSection() {
  const [email, setEmail] = useState("")
  const [loading, setLoading] = useState(false)
  const [generatedKey, setGeneratedKey] = useState("")
  const [error, setError] = useState("")
  const [copied, setCopied] = useState(false)
  const [expiry, setExpiry] = useState("")

  async function handleGenerateKey(e: React.FormEvent) {
    e.preventDefault()
    setError("")
    setGeneratedKey("")

    if (!email || !email.includes("@")) {
      setError("Please enter a valid email address.")
      return
    }

    setLoading(true)
    try {
      const res = await fetch("/api/create-trial", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      })
      const data = await res.json()
      if (!res.ok || data.error) {
        throw new Error(data.error || "Failed to generate key.")
      }
      setGeneratedKey(data.key)
      if (data.expires) {
        const expDate = new Date(data.expires)
        setExpiry(expDate.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" }))
      }
    } catch (err: any) {
      setError(err.message || "An unexpected error occurred.")
    } finally {
      setLoading(false)
    }
  }

  function handleCopy() {
    if (!generatedKey) return
    navigator.clipboard.writeText(generatedKey)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <section id="pricing" className="relative py-32 px-4 overflow-hidden">
      {/* Lemon Squeezy Overlay Loader Script */}
      <Script src="https://assets.lemonsqueezy.com/lemon.js" strategy="lazyOnload" />

      {/* Background gradients */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="absolute inset-0 bg-gradient-to-b from-background via-indigo-950/5 to-background" />
        <div className="absolute top-1/2 left-1/4 w-96 h-96 rounded-full bg-indigo-500/10 blur-3xl" />
        <div className="absolute bottom-1/4 right-1/4 w-96 h-96 rounded-full bg-violet-600/10 blur-3xl" />
      </div>

      <div className="relative z-10 max-w-6xl mx-auto">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center mb-20"
        >
          <span className="inline-block px-4 py-1.5 rounded-full bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 text-sm font-medium mb-4">
            Transparent Pricing
          </span>
          <h2 className="text-4xl md:text-5xl font-bold mb-6">
            <span className="text-white">Activate Your </span>
            <span className="bg-gradient-to-r from-indigo-400 via-violet-300 to-indigo-500 bg-clip-text text-transparent">
              ClipboardPro License
            </span>
          </h2>
          <p className="text-muted-foreground text-lg max-w-2xl mx-auto text-pretty">
            Generate an instant trial key inside the dashboard simulator, or secure a permanent license key to synchronize without limits.
          </p>
        </motion.div>

        {/* Pricing Cards Grid */}
        <div className="grid lg:grid-cols-3 md:grid-cols-2 grid-cols-1 gap-8 max-w-6xl mx-auto items-stretch">
          {PLANS.map((plan, i) => (
            <motion.div
              key={plan.name}
              initial={{ opacity: 0, y: 40 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ duration: 0.6, delay: i * 0.15 }}
              whileHover={{ y: -8 }}
              className={`relative flex flex-col justify-between rounded-3xl p-8 transition-all duration-300 ${
                plan.featured
                  ? "bg-gradient-to-b from-indigo-950/30 via-indigo-900/10 to-transparent border-2 border-indigo-500/50 shadow-lg shadow-indigo-500/10"
                  : "glass-card border border-white/10"
              }`}
            >
              {/* Badge */}
              <div className="absolute top-6 right-6">
                <span
                  className={`text-[10px] font-bold px-3 py-1 rounded-full uppercase tracking-wider ${
                    plan.featured
                      ? "bg-indigo-500 text-white shadow-md shadow-indigo-500/30 animate-pulse"
                      : "bg-white/5 text-indigo-300 border border-indigo-500/20"
                  }`}
                >
                  {plan.badge}
                </span>
              </div>

              <div>
                {/* Plan Name */}
                <h3 className="text-xl font-bold text-white mb-2 flex items-center gap-2">
                  {plan.featured ? (
                    <Key className="w-5 h-5 text-indigo-400" />
                  ) : (
                    <ShieldCheck className="w-5 h-5 text-indigo-400" />
                  )}
                  {plan.name}
                </h3>
                <p className="text-sm text-muted-foreground mb-6 min-h-[40px]">
                  {plan.desc}
                </p>

                {/* Price Display */}
                <div className="flex items-baseline gap-1 mb-8">
                  <span className="text-4xl md:text-5xl font-extrabold text-white">
                    ${plan.price}
                  </span>
                  <span className="text-xs text-muted-foreground font-medium uppercase tracking-wider">
                    / {plan.period}
                  </span>
                </div>

                {/* Separator */}
                <div className="w-full h-[1px] bg-white/10 mb-8" />

                {/* Features List */}
                <ul className="space-y-4 mb-8">
                  {plan.features.map((feature, idx) => (
                    <li key={idx} className="flex items-start gap-3 text-sm text-muted-foreground">
                      <div className="w-5 h-5 rounded-full bg-indigo-500/20 flex items-center justify-center shrink-0 mt-0.5">
                        <Check className="w-3.5 h-3.5 text-indigo-400" />
                      </div>
                      <span className="text-white/85 leading-snug">{feature}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Action Button or Trial Generator Form */}
              {plan.isTrial ? (
                <div className="w-full pt-4">
                  <Button
                    size="lg"
                    className="w-full py-6 text-base font-semibold group bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 border-0 text-white shadow-lg shadow-indigo-500/20 transition-all duration-300"
                    asChild
                  >
                    <a
                      href="/ClipboardPro-Setup.exe"
                      download
                      className="flex items-center justify-center w-full h-full gap-2 cursor-pointer"
                    >
                      <Download className="w-4 h-4 mr-2 group-hover:scale-110 transition-transform" />
                      Download ClipboardPro
                    </a>
                  </Button>
                </div>
              ) : (
                <Button
                  size="lg"
                  className="w-full py-6 text-base font-semibold group bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 border-0 text-white shadow-lg shadow-indigo-500/20 transition-all duration-300"
                  asChild
                >
                  <a
                    href={plan.href}
                    className="lemonsqueezy-button cursor-pointer"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {plan.cta}
                    <ArrowRight className="w-4 h-4 ml-2 group-hover:translate-x-1 transition-transform" />
                  </a>
                </Button>
              )}
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  )
}

