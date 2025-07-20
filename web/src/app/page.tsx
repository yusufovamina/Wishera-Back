"use client";
import { motion } from "framer-motion";
import { useState, useEffect } from "react";
import { UserIcon, GiftIcon, LockClosedIcon, ShareIcon, CloudIcon, SparklesIcon, ChevronLeftIcon, ChevronRightIcon, HomeIcon, InformationCircleIcon, ChatBubbleLeftRightIcon } from "@heroicons/react/24/outline";
import Link from "next/link";
import Navbar from "../components/Navbar";
import Footer from "../components/Footer";

const features = [
  {
    title: "User Authentication",
    description: "Register, login, and manage your wishlist securely.",
    benefit: "Your data is always protected and private.",
    icon: UserIcon,
    color: "bg-gradient-to-br from-blue-500 to-indigo-600",
  },
  {
    title: "Wishlist Management",
    description: "Add, edit, and delete gifts from your wishlist.",
    benefit: "Keep your wishes organized and up to date.",
    icon: GiftIcon,
    color: "bg-gradient-to-br from-pink-500 to-rose-500",
  },
  {
    title: "Gift Reservations",
    description: "Friends can reserve gifts (without the owner seeing who reserved them).",
    benefit: "No more duplicate gifts or spoiled surprises!",
    icon: LockClosedIcon,
    color: "bg-gradient-to-br from-green-400 to-emerald-600",
  },
  {
    title: "Wishlist Sharing",
    description: "Share your wishlist with friends via a unique link.",
    benefit: "Easily let others know what you really want.",
    icon: ShareIcon,
    color: "bg-gradient-to-br from-yellow-400 to-orange-500",
  },
  {
    title: "Cloud Image Uploads",
    description: "Upload and display gift images using Cloudinary.",
    benefit: "Show off your wishes with beautiful images.",
    icon: CloudIcon,
    color: "bg-gradient-to-br from-purple-500 to-fuchsia-600",
  },
  {
    title: "Glassmorphism UI",
    description: "A stunning, modern gradient-based design with smooth transparency effects.",
    benefit: "Enjoy a visually immersive and elegant experience.",
    icon: SparklesIcon,
    color: "bg-gradient-to-br from-cyan-400 to-blue-300",
  },
];

const testimonials = [
  {
    avatar: "https://randomuser.me/api/portraits/women/65.jpg",
    quote: "Wishlist App made gift-giving so much easier for my friends and family. I love how simple and fun it is!",
    name: "Emily R.",
  },
  {
    avatar: "https://randomuser.me/api/portraits/men/32.jpg",
    quote: "I never get duplicate gifts anymore. The reservation feature is genius!",
    name: "James P.",
  },
  {
    avatar: "https://randomuser.me/api/portraits/women/44.jpg",
    quote: "The design is beautiful and sharing my wishlist is a breeze.",
    name: "Sophia L.",
  },
  {
    avatar: "https://randomuser.me/api/portraits/men/65.jpg",
    quote: "Cloud image uploads make my wishlist look amazing. Highly recommend!",
    name: "Michael T.",
  },
];

function AnimatedBlobs() {
  return (
    <>
      <motion.div
        className="absolute -top-24 -left-24 w-96 h-96 bg-blue-400 opacity-30 rounded-full blur-3xl z-0"
        animate={{ y: [0, 30, 0], x: [0, 20, 0] }}
        transition={{ duration: 10, repeat: Infinity, repeatType: "mirror" }}
      />
      <motion.div
        className="absolute top-40 -right-32 w-80 h-80 bg-pink-400 opacity-20 rounded-full blur-3xl z-0"
        animate={{ y: [0, -20, 0], x: [0, -30, 0] }}
        transition={{ duration: 12, repeat: Infinity, repeatType: "mirror" }}
      />
      <motion.div
        className="absolute bottom-0 left-1/2 w-72 h-72 bg-purple-400 opacity-20 rounded-full blur-3xl z-0"
        animate={{ y: [0, 20, 0], x: [0, 10, 0] }}
        transition={{ duration: 14, repeat: Infinity, repeatType: "mirror" }}
      />
      {/* New blobs */}
      <motion.div
        className="absolute top-10 left-1/3 w-40 h-40 bg-yellow-300 opacity-20 rounded-full blur-2xl z-0"
        animate={{ scale: [1, 1.1, 1] }}
        transition={{ duration: 8, repeat: Infinity, repeatType: "mirror" }}
      />
      <motion.div
        className="absolute bottom-10 right-1/4 w-32 h-32 bg-green-300 opacity-20 rounded-full blur-2xl z-0"
        animate={{ scale: [1, 0.95, 1] }}
        transition={{ duration: 9, repeat: Infinity, repeatType: "mirror" }}
      />
      {/* Dotted pattern */}
      <div className="absolute top-0 right-0 w-32 h-32 z-0 opacity-10 pointer-events-none select-none">
        <svg width="100%" height="100%" viewBox="0 0 100 100" fill="none">
          <defs>
            <pattern id="dots" x="0" y="0" width="10" height="10" patternUnits="userSpaceOnUse">
              <circle cx="1.5" cy="1.5" r="1.5" fill="#6366f1" />
            </pattern>
          </defs>
          <rect width="100" height="100" fill="url(#dots)" />
        </svg>
      </div>
    </>
  );
}

function TrustedBy() {
  const avatars = [
    "https://randomuser.me/api/portraits/men/32.jpg",
    "https://randomuser.me/api/portraits/women/44.jpg",
    "https://randomuser.me/api/portraits/men/65.jpg",
    "https://randomuser.me/api/portraits/women/68.jpg",
    "https://randomuser.me/api/portraits/men/12.jpg",
  ];
  return (
    <motion.div
      className="flex flex-col items-center mt-12 mb-4"
      initial={{ opacity: 0, y: 20 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.5 }}
      transition={{ duration: 0.8 }}
    >
      <div className="text-gray-400 text-sm mb-3 font-medium tracking-wide">Trusted by</div>
      <div className="flex gap-2 sm:gap-4">
        {avatars.map((src, idx) => (
          <motion.div
            key={src}
            className="w-8 h-8 sm:w-10 sm:h-10 rounded-full border-2 border-white shadow-md overflow-hidden flex items-center justify-center bg-gray-100"
            whileHover={{ scale: 1.1 }}
            transition={{ type: "spring", stiffness: 300 }}
          >
            <img src={src} alt={`User ${idx + 1}`} className="w-full h-full object-cover" />
          </motion.div>
        ))}
      </div>
    </motion.div>
  );
}

function TestimonialCarousel() {
  const [index, setIndex] = useState(0);
  useEffect(() => {
    const timer = setTimeout(() => setIndex((i) => (i + 1) % testimonials.length), 4000);
    return () => clearTimeout(timer);
  }, [index]);
  const prev = () => setIndex((i) => (i - 1 + testimonials.length) % testimonials.length);
  const next = () => setIndex((i) => (i + 1) % testimonials.length);
  return (
    <div className="relative flex flex-col items-center bg-white/80 rounded-2xl shadow-lg px-6 py-6 mt-8 mb-2 max-w-xs mx-auto border border-gray-100 backdrop-blur-sm min-h-[220px]">
      <div className="absolute left-2 top-1/2 -translate-y-1/2 z-10">
        <button onClick={prev} className="p-1 rounded-full bg-white/70 hover:bg-indigo-100 transition">
          <ChevronLeftIcon className="w-5 h-5 text-indigo-400" />
        </button>
      </div>
      <div className="absolute right-2 top-1/2 -translate-y-1/2 z-10">
        <button onClick={next} className="p-1 rounded-full bg-white/70 hover:bg-indigo-100 transition">
          <ChevronRightIcon className="w-5 h-5 text-indigo-400" />
        </button>
      </div>
      <motion.div
        key={index}
        initial={{ opacity: 0, x: 40 }}
        animate={{ opacity: 1, x: 0 }}
        exit={{ opacity: 0, x: -40 }}
        transition={{ duration: 0.5 }}
        className="flex flex-col items-center w-full"
      >
        <img
          src={testimonials[index].avatar}
          alt={testimonials[index].name}
          className="w-14 h-14 rounded-full mb-3 shadow-md object-cover"
        />
        <blockquote className="italic text-gray-700 text-center mb-2 text-base sm:text-lg">
          “{testimonials[index].quote}”
        </blockquote>
        <div className="text-sm text-gray-500 font-semibold">{testimonials[index].name}</div>
      </motion.div>
      <div className="flex gap-1 mt-3 justify-center">
        {testimonials.map((_, i) => (
          <button
            key={i}
            onClick={() => setIndex(i)}
            className={`inline-block w-2 h-2 rounded-full focus:outline-none transition-colors duration-200 ${i === index ? "bg-indigo-400" : "bg-gray-300"}`}
            aria-label={`Go to testimonial ${i + 1}`}
          />
        ))}
      </div>
    </div>
  );
}

function ScrollIndicator() {
  return (
    <motion.div
      className="absolute left-1/2 -translate-x-1/2 bottom-4 z-20 flex flex-col items-center"
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: [10, 0, 10] }}
      transition={{ duration: 2, repeat: Infinity, repeatType: "loop" }}
    >
      <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#6366f1" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="animate-bounce">
        <polyline points="6 9 12 15 18 9" />
      </svg>
      <span className="text-xs text-indigo-400 mt-1">Scroll</span>
    </motion.div>
  );
}

function AppMockup() {
  return (
    <motion.div
      className="relative z-10 bg-white rounded-3xl shadow-2xl border border-gray-100 w-full max-w-xs sm:max-w-sm md:max-w-md lg:max-w-lg p-4 sm:p-6 flex flex-col gap-4 transition-transform duration-300 hover:-translate-y-2 hover:shadow-3xl"
      initial={{ opacity: 0, y: 40 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 1, delay: 0.5 }}
      whileHover={{ scale: 1.04 }}
    >
      <div className="flex items-center gap-2 mb-2">
        <div className="w-8 h-8 sm:w-10 sm:h-10 rounded-full bg-gradient-to-br from-blue-400 to-indigo-500 flex items-center justify-center text-white font-bold text-base sm:text-lg">A</div>
        <div>
          <div className="font-semibold text-gray-800 text-sm sm:text-base">Alex&apos;s Wishlist</div>
          <div className="text-xs text-gray-400">Public</div>
        </div>
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-3 p-2 rounded-xl bg-gray-50">
          <GiftIcon className="w-5 h-5 sm:w-6 sm:h-6 text-pink-400" />
          <div className="flex-1">
            <div className="font-medium text-gray-700 text-sm sm:text-base">Nike Air Max 270</div>
            <div className="text-xs text-gray-400">$150</div>
          </div>
          <span className="text-xs text-green-500 font-semibold">Available</span>
        </div>
        <div className="flex items-center gap-3 p-2 rounded-xl bg-gray-50">
          <GiftIcon className="w-5 h-5 sm:w-6 sm:h-6 text-yellow-400" />
          <div className="flex-1">
            <div className="font-medium text-gray-700 text-sm sm:text-base">Apple Watch SE</div>
            <div className="text-xs text-gray-400">$249</div>
          </div>
          <span className="text-xs text-gray-400 font-semibold">Reserved</span>
        </div>
        <div className="flex items-center gap-3 p-2 rounded-xl bg-gray-50">
          <GiftIcon className="w-5 h-5 sm:w-6 sm:h-6 text-purple-400" />
          <div className="flex-1">
            <div className="font-medium text-gray-700 text-sm sm:text-base">Cloud Pillow</div>
            <div className="text-xs text-gray-400">$39</div>
          </div>
          <span className="text-xs text-green-500 font-semibold">Available</span>
        </div>
      </div>
      <div className="mt-4 flex gap-2">
        <button className="flex-1 py-2 rounded-xl bg-gradient-to-r from-indigo-500 to-blue-500 text-white font-semibold shadow text-sm sm:text-base">Share</button>
        <button className="flex-1 py-2 rounded-xl bg-gray-100 text-gray-600 font-semibold shadow text-sm sm:text-base">Reserve</button>
      </div>
    </motion.div>
  );
}

function FeaturesBackground() {
  return (
    <div className="absolute inset-0 z-0 flex items-center justify-center pointer-events-none select-none">
      <svg width="100%" height="100%" viewBox="0 0 1200 200" fill="none" className="hidden md:block">
        <path d="M100 100 Q 300 0 500 100 T 900 100 T 1100 100" stroke="#c7d2fe" strokeWidth="3" fill="none" opacity="0.25" />
      </svg>
      <svg width="100%" height="100%" viewBox="0 0 400 200" fill="none" className="block md:hidden">
        <path d="M20 100 Q 100 20 200 100 T 380 100" stroke="#c7d2fe" strokeWidth="2" fill="none" opacity="0.18" />
      </svg>
    </div>
  );
}

export default function Home() {
  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-200 flex flex-col">
      <Navbar />
      {/* Hero Section */}
      <section className="relative flex flex-col-reverse md:flex-row items-center justify-center py-16 sm:py-24 px-2 sm:px-8 bg-gradient-to-br from-indigo-50 to-white overflow-hidden w-full">
        <AnimatedBlobs />
        <div className="relative z-10 flex-1 flex flex-col items-center md:items-start text-center md:text-left w-full max-w-xl">
          <motion.h1
            initial={{ opacity: 0, y: -40 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.7 }}
            className="text-3xl sm:text-5xl lg:text-6xl font-extrabold text-gray-900 mb-6 tracking-tight drop-shadow-lg w-full"
          >
            Wishlist App
          </motion.h1>
          <motion.p
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.2 }}
            className="text-lg sm:text-2xl text-gray-600 max-w-2xl mb-8 w-full"
          >
            The Ultimate Gift Wishlist Manager
          </motion.p>
          <motion.p
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.8, delay: 0.4 }}
            className="text-base sm:text-lg text-gray-500 max-w-xl mb-10 w-full"
          >
            A sleek, modern wishlist app, featuring authentication, gift reservations, and more.
          </motion.p>
          {/* Removed login/sign up buttons from hero section, now in Navbar */}
          <TrustedBy />
          <TestimonialCarousel />
          <motion.a
            href="#features"
            whileHover={{ scale: 1.04 }}
            className="inline-block mt-6 px-6 py-3 rounded-full bg-gradient-to-r from-yellow-400 to-orange-400 text-white font-semibold text-base shadow-md transition-transform"
          >
            See how it works
          </motion.a>
        </div>
        <div className="relative z-10 flex-1 flex justify-center mb-8 md:mb-0 w-full max-w-md">
          <AppMockup />
        </div>
        <ScrollIndicator />
      </section>
      {/* Features Section */}
      <section id="features" className="relative flex flex-col gap-16 sm:gap-20 py-12 sm:py-24 px-2 sm:px-8 max-w-5xl mx-auto w-full">
        <FeaturesBackground />
        <div className="relative z-10 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {features.map((feature, idx) => {
            const Icon = feature.icon;
            return (
              <motion.div
                key={feature.title}
                className="backdrop-blur-lg bg-white/60 border border-gray-100 rounded-3xl shadow-xl flex flex-col items-center text-center p-8 min-h-[320px] w-full max-w-md mx-auto relative overflow-hidden"
                initial={{ opacity: 0, y: 40 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true, amount: 0.5 }}
                transition={{ duration: 0.7, delay: idx * 0.15 }}
              >
                <motion.div
                  className={`w-20 h-20 mb-6 rounded-full ${feature.color} flex items-center justify-center shadow-lg border-4 border-white/40`}
                  whileHover={{ scale: 1.1, rotate: 6 }}
                  transition={{ type: "spring", stiffness: 300 }}
                >
                  <Icon className="w-10 h-10 text-white drop-shadow-lg" />
                </motion.div>
                <h2 className="text-xl font-semibold text-gray-800 mb-2">{feature.title}</h2>
                <p className="text-gray-500 text-base mb-2">{feature.description}</p>
                <div className="text-indigo-500 font-medium text-sm italic mb-2">{feature.benefit}</div>
                {/* Subtle divider */}
                <div className="w-12 h-1 rounded-full bg-gradient-to-r from-indigo-200 to-indigo-400 opacity-30 mx-auto mt-2" />
              </motion.div>
            );
          })}
        </div>
      </section>
      <Footer />
    </div>
  );
}
