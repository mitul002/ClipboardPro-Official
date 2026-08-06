"use client"

import { motion } from "framer-motion"
import Image from "next/image"
import { Github, Twitter, Mail, Heart, ExternalLink, Globe } from "lucide-react"

const footerLinks = {
  product: [
    { label: "Features", href: "#features" },
    { label: "Download", href: "#download" },
  ],
  resources: [
    { label: "FAQ", href: "#faq" },
    { label: "Support", href: "mailto:hasinalmasmitul@gmail.com" },
    { label: "GitHub", href: "https://github.com/mitul002/ClipboardPro-Official" },
  ],
  legal: [
    { label: "Privacy Policy", href: "/privacy" },
    { label: "Refund Policy", href: "/refund" },
    { label: "Terms of Service", href: "/terms-of-service" },
  ],
}

const socialLinks = [
  { icon: Github, href: "https://github.com/mitul002/ClipboardPro-Official", label: "GitHub" },
  { icon: Twitter, href: "https://twitter.com", label: "Twitter" },
  { icon: Mail, href: "mailto:hasinalmasmitul@gmail.com", label: "Email" },
]

export function Footer() {
  return (
    <footer className="relative pt-24 pb-8 px-4 overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-t from-indigo-950/20 via-background to-background" />
        <div className="absolute bottom-0 left-1/2 -translate-x-1/2 w-[1200px] h-[400px] bg-indigo-600/5 rounded-full blur-[150px]" />
      </div>

      {/* Top border glow */}
      <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-indigo-500/30 to-transparent" />

      <div className="relative z-10 max-w-7xl mx-auto">
        <div className="grid grid-cols-2 md:grid-cols-2 lg:grid-cols-5 gap-x-8 gap-y-12 mb-16">
          {/* Brand column */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6 }}
            className="col-span-2 lg:col-span-2"
          >
            {/* Logo */}
            <div className="flex items-center gap-3 mb-6">
              <div className="w-10 h-10 flex items-center justify-center overflow-hidden">
                <Image
                  src="/logo.png"
                  alt="ClipboardPro"
                  width={36}
                  height={36}
                  className="object-contain"
                />
              </div>
              <div>
                <div className="font-bold text-xl text-slate-100 tracking-tight">Clipboard<span className="text-indigo-400">Pro</span></div>
                <div className="text-xs text-indigo-400 font-mono font-bold">v1.4.2</div>
              </div>
            </div>

            {/* Description */}
            <p className="text-muted-foreground leading-relaxed mb-6 max-w-sm text-pretty">
              High-performance native Windows clipboard manager and instant multi-device sync. Keep your workspaces connected with zero lag.
            </p>

            {/* Social links */}
            <div className="flex items-center gap-3">
              {socialLinks.map((social) => (
                <a
                  key={social.label}
                  href={social.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-10 h-10 rounded-xl glass-card flex items-center justify-center text-muted-foreground hover:text-indigo-400 hover:border-indigo-500/30 transition-all duration-300"
                  aria-label={social.label}
                >
                  <social.icon className="w-5 h-5" />
                </a>
              ))}
            </div>
          </motion.div>

          {/* Link columns */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.1 }}
          >
            <h4 className="font-semibold text-foreground mb-4">Product</h4>
            <ul className="space-y-3">
              {footerLinks.product.map((link) => (
                <li key={link.label}>
                  <a
                    href={link.href}
                    className="text-muted-foreground hover:text-indigo-400 transition-colors text-sm"
                  >
                    {link.label}
                  </a>
                </li>
              ))}
            </ul>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.2 }}
          >
            <h4 className="font-semibold text-foreground mb-4">Resources</h4>
            <ul className="space-y-3">
              {footerLinks.resources.map((link) => (
                <li key={link.label}>
                  <a
                    href={link.href}
                    className="text-muted-foreground hover:text-indigo-400 transition-colors text-sm inline-flex items-center gap-1"
                    {...(link.href.startsWith("http") ? { target: "_blank", rel: "noopener noreferrer" } : {})}
                  >
                    {link.label}
                    {link.href.startsWith("http") && <ExternalLink className="w-3 h-3" />}
                  </a>
                </li>
              ))}
            </ul>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.6, delay: 0.3 }}
          >
            <h4 className="font-semibold text-foreground mb-4">Legal</h4>
            <ul className="space-y-3">
              {footerLinks.legal.map((link) => (
                <li key={link.label}>
                  <a
                    href={link.href}
                    className="text-muted-foreground hover:text-indigo-400 transition-colors text-sm"
                  >
                    {link.label}
                  </a>
                </li>
              ))}
            </ul>
          </motion.div>
        </div>

        {/* Divider */}
        <div className="h-px bg-gradient-to-r from-transparent via-indigo-500/20 to-transparent mb-8" />

        {/* Bottom row */}
        <motion.div
          initial={{ opacity: 0 }}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true }}
          transition={{ duration: 0.6, delay: 0.4 }}
          className="flex flex-col md:flex-row items-center justify-between gap-6"
        >
          {/* Copyright */}
          <div className="flex flex-col sm:flex-row items-center gap-2 text-sm text-muted-foreground">
            <span>&copy; {new Date().getFullYear()} ClipboardPro. All rights reserved.</span>
          </div>

          {/* Developer credit */}
          <p className="text-sm text-muted-foreground text-center md:text-right leading-relaxed">
            Developed by{" "}
            <span className="font-bold text-foreground whitespace-nowrap">Cross Tech</span>{" "}
            by{" "}
            <span className="font-bold bg-gradient-to-r from-indigo-400 via-violet-400 to-indigo-500 bg-clip-text text-transparent whitespace-nowrap">
              Magnetieght EU
            </span>{" "}
            <Globe className="w-4 h-4 text-indigo-400 inline-block align-middle mx-0.5 shrink-0" />
          </p>
        </motion.div>
      </div>
    </footer>
  )
}

