import type { Metadata } from 'next'
import { Navbar } from "@/components/sections/navbar"
import { Footer } from "@/components/sections/footer"
import { ArrowLeft, RefreshCw } from "lucide-react"
import Link from "next/link"

export const metadata: Metadata = {
  title: 'Refund Policy - ClipboardPro',
  description: 'Learn about our 14-day money-back guarantee and refund request process.',
}

export default function RefundPolicy() {
  return (
    <>
      <Navbar />
      <main className="min-h-screen pt-32 pb-20 px-4 relative overflow-hidden">
        {/* Background Gradients */}
        <div className="absolute inset-0">
          <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[500px] bg-indigo-600/10 rounded-full blur-[120px]" />
          <div className="absolute bottom-0 inset-0 bg-background" />
        </div>

        <div className="relative z-10 max-w-4xl mx-auto">
          {/* Back Button */}
          <Link 
            href="/"
            className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-indigo-400 transition-colors mb-8 group"
          >
            <ArrowLeft className="w-4 h-4 group-hover:-translate-x-1 transition-transform" />
            Back to Home
          </Link>

          {/* Page Header */}
          <div className="flex items-center gap-4 mb-8">
            <div className="w-12 h-12 rounded-xl bg-indigo-500/10 border border-indigo-500/30 flex items-center justify-center text-indigo-400">
              <RefreshCw className="w-6 h-6 animate-spin-slow" />
            </div>
            <div>
              <h1 className="text-4xl font-extrabold bg-gradient-to-r from-white via-indigo-100 to-indigo-400 bg-clip-text text-transparent">
                Refund Policy
              </h1>
              <p className="text-sm text-muted-foreground mt-1">Last updated: May 2026</p>
            </div>
          </div>

          {/* Content Card */}
          <div className="glass-card p-8 md:p-10 rounded-3xl border border-white/5 bg-white/[0.02] backdrop-blur-xl space-y-8 text-muted-foreground leading-relaxed">
            <p className="text-foreground/90 font-medium">
              Thank you for purchasing ClipboardPro! We want to ensure you have a seamless experience with our desktop utility software.
            </p>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">1.</span> 14-Day Money-Back Guarantee
              </h2>
              <p>
                We offer a 14-day money-back guarantee for ClipboardPro. If the software fails to function properly on your Windows machine, or if you encounter technical errors that we cannot resolve, you are eligible for a full refund within 14 days of your original purchase date.
              </p>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">2.</span> How to Request a Refund
              </h2>
              <p>
                To request a refund, please contact us via email at{' '}
                <a href="mailto:hasinalmasmitul@gmail.com" className="text-indigo-400 hover:underline">
                  hasinalmasmitul@gmail.com
                </a>{' '}
                with:
              </p>
              <ul className="list-disc pl-6 space-y-2">
                <li>
                  Your purchase email address.
                </li>
                <li>
                  Your Lemon Squeezy order ID.
                </li>
                <li>
                  A brief description of the technical issue you are facing.
                </li>
              </ul>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">3.</span> License Deactivation
              </h2>
              <p>
                Upon an approved refund, your automated license key will be permanently deactivated, and the Pro features of the software will no longer be accessible.
              </p>
            </div>
          </div>
        </div>
      </main>
      <Footer />
    </>
  )
}
