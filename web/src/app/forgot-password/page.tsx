"use client";
import { motion } from "framer-motion";
import Link from "next/link";
import { useState, useCallback } from "react";
import { forgotPassword } from "../api";
import Notification from "../../components/Notification";

function AnimatedBlobs() {
  return (
    <>
      <motion.div className="absolute -top-24 -left-24 w-96 h-96 bg-blue-400 opacity-30 rounded-full blur-3xl z-0" animate={{ y: [0, 30, 0], x: [0, 20, 0] }} transition={{ duration: 10, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute top-40 -right-32 w-80 h-80 bg-pink-400 opacity-20 rounded-full blur-3xl z-0" animate={{ y: [0, -20, 0], x: [0, -30, 0] }} transition={{ duration: 12, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute bottom-0 left-1/2 w-72 h-72 bg-purple-400 opacity-20 rounded-full blur-3xl z-0" animate={{ y: [0, 20, 0], x: [0, 10, 0] }} transition={{ duration: 14, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute top-10 left-1/3 w-40 h-40 bg-yellow-300 opacity-20 rounded-full blur-2xl z-0" animate={{ scale: [1, 1.1, 1] }} transition={{ duration: 8, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute bottom-10 right-1/4 w-32 h-32 bg-green-300 opacity-20 rounded-full blur-2xl z-0" animate={{ scale: [1, 0.95, 1] }} transition={{ duration: 9, repeat: Infinity, repeatType: "mirror" }} />
    </>
  );
}

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [notification, setNotification] = useState<{
    type: 'success' | 'error';
    message: string;
    isVisible: boolean;
  }>({
    type: 'success',
    message: '',
    isVisible: false
  });

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setNotification({ type: 'success', message: '', isVisible: false });
    
          try {
        await forgotPassword(email);
        setNotification({
          type: 'success',
          message: 'Password reset link sent to your email! Check your inbox (and spam folder).',
          isVisible: true
        });
        setEmail("");
      } catch (err: any) {
        setNotification({
          type: 'error',
          message: err.response?.data?.message || "Failed to send reset email. Please check your email address and try again.",
          isVisible: true
        });
      } finally {
        setLoading(false);
      }
  }, [email]);

  const closeNotification = () => {
    setNotification(prev => ({ ...prev, isVisible: false }));
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-indigo-50 to-white relative overflow-hidden">
      <AnimatedBlobs />
      <motion.div
        className="relative z-10 bg-white/80 shadow-xl rounded-2xl p-8 w-full max-w-md border border-gray-100 backdrop-blur-lg"
        initial={{ opacity: 0, y: 40 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.7 }}
      >
        <h1 className="text-2xl font-bold text-gray-800 mb-2 text-center">Forgot Password?</h1>
        <p className="text-gray-600 text-center mb-6">Enter your email address and we'll send you a link to reset your password.</p>
        
        <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
          <input 
            type="email" 
            placeholder="Enter your email" 
            className="px-4 py-3 rounded-lg border border-gray-200 focus:outline-none focus:ring-2 focus:ring-indigo-400 bg-white/90 text-gray-700" 
            autoComplete="email" 
            required 
            value={email} 
            onChange={e => setEmail(e.target.value)} 
          />
          <button 
            type="submit" 
            className="mt-2 py-3 rounded-lg bg-gradient-to-r from-indigo-500 to-blue-500 text-white font-semibold text-lg shadow-md hover:scale-105 transition-transform" 
            disabled={loading}
          >
            {loading ? "Sending..." : "Send Reset Link"}
          </button>
        </form>
        
        <div className="mt-6 text-center text-gray-500 text-sm">
          Remember your password?{' '}
          <Link href="/login" className="text-indigo-500 hover:underline font-medium">Back to Login</Link>
        </div>
      </motion.div>
      
      <Notification
        type={notification.type}
        message={notification.message}
        isVisible={notification.isVisible}
        onClose={closeNotification}
      />
    </div>
  );
} 