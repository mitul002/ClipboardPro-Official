import type { Metadata, Viewport } from 'next'
import { Inter } from 'next/font/google'
import { Analytics } from '@vercel/analytics/next'
import './globals.css'

const inter = Inter({ 
  subsets: ["latin"],
  variable: '--font-inter'
})

export const metadata: Metadata = {
  title: 'ClipboardPro - The secure copy space',
  description: 'ClipboardPro is an ultra-fast, native C# WPF desktop client and peer-to-peer secure sync engine that mirrors your clipboard history across Windows devices in under 100ms.',
  keywords: ['Windows', 'clipboard manager', 'zero-trust sync', 'WPF client', 'AES-GCM-256', 'secure mirroring'],
  authors: [{ name: 'ClipboardPro Team' }],
  icons: {
    icon: '/logo.png',
  },
  openGraph: {
    title: 'ClipboardPro - The secure copy space',
    description: 'A native C# WPF clipboard sync utility utilizing secure end-to-end client-side encryption and motherboard HWID device binding.',
    type: 'website',
  },
}

export const viewport: Viewport = {
  themeColor: '#6366f1',
  width: 'device-width',
  initialScale: 1,
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html lang="en" className="bg-background">
      <body className={`${inter.variable} font-sans antialiased`}>
        {children}
        {process.env.NODE_ENV === 'production' && <Analytics />}
      </body>
    </html>
  )
}
