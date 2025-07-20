import Link from "next/link";
import { HomeIcon } from "@heroicons/react/24/outline";

export default function Navbar() {
  return (
    <nav className="w-full sticky top-0 z-30 bg-gradient-to-br from-white/70 to-indigo-100/60 backdrop-blur-md border-b border-indigo-100/40 shadow-sm">
      <div className="max-w-6xl mx-auto px-2 sm:px-4 py-2 sm:py-3 flex items-center justify-between w-full">
        {/* Logo and name */}
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 sm:w-8 sm:h-8 rounded-xl bg-gradient-to-br from-indigo-500 to-blue-400 flex items-center justify-center text-white font-extrabold text-lg sm:text-xl shadow">W</div>
          <span className="text-base sm:text-lg font-bold text-gray-800 tracking-tight">Wishlist App</span>
        </div>
        {/* Navigation */}
        <div className="flex gap-3 sm:gap-6 md:gap-8 text-gray-600 font-medium text-sm sm:text-base items-center">
          <Link href="/" className="flex items-center gap-1 hover:text-indigo-500 transition"><HomeIcon className="w-5 h-5" /> Home</Link>
          <a href="#features" className="hover:text-indigo-500 transition">Features</a>
        </div>
        {/* Auth buttons */}
        <div className="flex gap-2">
          <Link href="/login" className="px-4 py-2 rounded-full bg-gradient-to-r from-indigo-500 to-blue-500 text-white font-semibold text-sm shadow hover:scale-105 transition-transform">Login</Link>
          <Link href="/register" className="px-4 py-2 rounded-full bg-gradient-to-r from-yellow-400 to-orange-400 text-white font-semibold text-sm shadow hover:scale-105 transition-transform">Sign Up</Link>
        </div>
      </div>
    </nav>
  );
} 