import { InformationCircleIcon, ChatBubbleLeftRightIcon } from "@heroicons/react/24/outline";
import Link from "next/link";

export default function Footer() {
  return (
    <footer className="w-full bg-gradient-to-br from-indigo-100/60 to-white/60 backdrop-blur-md border-t border-indigo-100/40 mt-auto">
      <div className="max-w-6xl mx-auto px-2 sm:px-4 py-8 sm:py-10 flex flex-col md:flex-row items-center md:items-start justify-between gap-6 sm:gap-8 w-full">
        {/* Navigation */}
        <nav className="flex flex-col md:flex-row gap-3 sm:gap-6 md:gap-8 text-gray-600 font-medium text-sm sm:text-base items-center">
          <a href="#features" className="hover:text-indigo-500 transition">Features</a>
          <a href="#about" className="flex items-center gap-1 hover:text-indigo-500 transition"><InformationCircleIcon className="w-5 h-5" /> About</a>
          <a href="#contact" className="flex items-center gap-1 hover:text-indigo-500 transition"><ChatBubbleLeftRightIcon className="w-5 h-5" /> Contact</a>
        </nav>
        {/* Socials */}
        <div className="flex gap-3 sm:gap-4 mt-4 md:mt-0">
          <a href="#" aria-label="Twitter" className="hover:text-indigo-500 transition"><svg className="w-5 h-5 sm:w-6 sm:h-6" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path d="M8 19c11 0 13-9 13-13v-.6A9.3 9.3 0 0 0 23 3a9.1 9.1 0 0 1-2.6.7A4.5 4.5 0 0 0 22.4.4a9.1 9.1 0 0 1-2.9 1.1A4.5 4.5 0 0 0 16.1 0c-2.5 0-4.5 2-4.5 4.5 0 .4 0 .8.1 1.2A12.8 12.8 0 0 1 3 1.1a4.5 4.5 0 0 0-.6 2.3c0 1.6.8 3 2.1 3.8A4.5 4.5 0 0 1 2 6.1v.1c0 2.2 1.6 4 3.7 4.4a4.5 4.5 0 0 1-2 .1c.6 1.8 2.3 3.1 4.3 3.1A9.1 9.1 0 0 1 2 17.5c-.6 0-1.2-.1-1.7-.2A12.8 12.8 0 0 0 8 19" /></svg></a>
          <a href="#" aria-label="GitHub" className="hover:text-indigo-500 transition"><svg className="w-5 h-5 sm:w-6 sm:h-6" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path d="M12 2C6.5 2 2 6.5 2 12c0 4.4 2.9 8.1 6.8 9.4.5.1.7-.2.7-.5v-1.7c-2.8.6-3.4-1.2-3.4-1.2-.4-1-1-1.3-1-1.3-.8-.6.1-.6.1-.6.9.1 1.4.9 1.4.9.8 1.4 2.1 1 2.6.8.1-.6.3-1 .5-1.2-2.2-.2-4.5-1.1-4.5-4.8 0-1.1.4-2 1-2.7-.1-.2-.4-1.2.1-2.5 0 0 .8-.3 2.7 1a9.3 9.3 0 0 1 5 0c1.9-1.3 2.7-1 2.7-1 .5 1.3.2 2.3.1 2.5.6.7 1 1.6 1 2.7 0 3.7-2.3 4.6-4.5 4.8.3.3.6.8.6 1.7v2.5c0 .3.2.6.7.5A10 10 0 0 0 22 12c0-5.5-4.5-10-10-10z" /></svg></a>
          <a href="#" aria-label="Instagram" className="hover:text-indigo-500 transition"><svg className="w-5 h-5 sm:w-6 sm:h-6" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><rect width="20" height="20" x="2" y="2" rx="5" ry="5" /><path d="M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z" /><line x1="17.5" y1="6.5" x2="17.5" y2="6.5" /></svg></a>
        </div>
      </div>
      <div className="text-center text-xs sm:text-sm text-gray-400 py-3 sm:py-4 border-t border-indigo-100/40 mt-4 sm:mt-6 w-full">
        &copy; {new Date().getFullYear()} Wishlist App. All rights reserved.
      </div>
    </footer>
  );
} 