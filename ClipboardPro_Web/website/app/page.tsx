"use client"

import { Navbar } from "@/components/sections/navbar"
import { HeroSection } from "@/components/sections/hero-section"
import { FeaturesSection } from "@/components/sections/features-section"
import { AdvancedSection } from "@/components/sections/advanced-section"
import { ScreenshotsSection } from "@/components/sections/screenshots-section"
import { WhySection } from "@/components/sections/why-section"
import { SpecsSection } from "@/components/sections/specs-section"
import { PricingSection } from "@/components/sections/pricing-section"
import { TestimonialsSection } from "@/components/sections/testimonials-section"
import { UseCasesSection } from "@/components/sections/use-cases-section"
import { DownloadSection } from "@/components/sections/download-section"
import { Footer } from "@/components/sections/footer"

export default function ClipboardProLanding() {
      return (
        <>
          <Navbar />
      <main className="min-h-screen overflow-x-hidden">
        <HeroSection />
        <FeaturesSection />
        <SpecsSection />
        <AdvancedSection />
        <ScreenshotsSection />
        <WhySection />
        <PricingSection />
        <TestimonialsSection />
        <UseCasesSection />
        <DownloadSection />
      </main>
      <Footer />
    </>
  )
}
