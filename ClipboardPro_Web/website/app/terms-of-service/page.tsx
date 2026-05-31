import type { Metadata } from 'next'
import { Navbar } from "@/components/sections/navbar"
import { Footer } from "@/components/sections/footer"
import { ArrowLeft, FileText } from "lucide-react"
import Link from "next/link"

export const metadata: Metadata = {
  title: 'Terms of Service - ClipboardPro',
  description: 'Read the terms of service governing the download and usage of ClipboardPro.',
}

export default function TermsOfService() {
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
              <FileText className="w-6 h-6" />
            </div>
            <div>
              <h1 className="text-4xl font-extrabold bg-gradient-to-r from-white via-indigo-100 to-indigo-400 bg-clip-text text-transparent">
                Terms of Service
              </h1>
              <p className="text-sm text-muted-foreground mt-1">Last updated: May 2026</p>
            </div>
          </div>

          {/* Content Card */}
          <div className="glass-card p-8 md:p-10 rounded-3xl border border-white/5 bg-white/[0.02] backdrop-blur-xl space-y-8 text-muted-foreground leading-relaxed">
            <p className="text-foreground/90 font-medium">
              By downloading and using ClipboardPro ({' '}
              <a href="https://clipboardpro.vercel.app/" className="text-indigo-400 hover:underline">
                https://clipboardpro.vercel.app/
              </a>
              ), you agree to comply with and be bound by the following Terms of Service.
            </p>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">1.</span> License Usage
              </h2>
              <p>
                Upon purchase, you are granted a non-exclusive, non-transferable lifetime or yearly license to use ClipboardPro for personal or professional productivity.
              </p>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">2.</span> Restrictions
              </h2>
              <p>
                You agree not to:
              </p>
              <ul className="list-disc pl-6 space-y-2">
                <li>
                  Decompile, reverse engineer, or attempt to extract the source code of the software executable (.exe).
                </li>
                <li>
                  Redistribute, resell, or share your automated license key with unauthorized users.
                </li>
              </ul>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">3.</span> Disclaimer of Warranty
              </h2>
              <p>
                ClipboardPro is provided "as is" without any express or implied warranties. While we strive to provide a highly optimized and stable productivity tool, we are not liable for any temporary performance disruptions on your operating system.
              </p>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">4.</span> Product Delivery & Fulfillment
              </h2>
              <p>
                Upon a successful payment via Lemon Squeezy, your digital license key will be generated automatically and delivered instantly to the email address you provided during checkout. You can use this key to activate the Pro features in the ClipboardPro application.
              </p>
            </div>

            <div className="border-t border-white/5 pt-8 space-y-4">
              <h2 className="text-xl font-bold text-foreground flex items-center gap-2">
                <span className="text-indigo-500">5.</span> Contact Us
              </h2>
              <p>
                If you have any questions or support queries regarding these Terms, please contact us via email at{' '}
                <a href="mailto:hasinalmasmitul@gmail.com" className="text-indigo-400 hover:underline">
                  hasinalmasmitul@gmail.com
                </a>.
              </p>
            </div>
          </div>
        </div>
      </main>
      <Footer />
    </>
  )
}
