"use client";
import { motion } from "framer-motion";
import Link from "next/link";
import { useState, useCallback, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { resetPassword } from "../api";
import Notification from "../../components/Notification";

function AnimatedBlobs() {
  return (
    <>
      <motion.div className="absolute -top-24 -left-24 w-96 h-96 bg-green-400 opacity-30 rounded-full blur-3xl z-0" animate={{ y: [0, 30, 0], x: [0, 20, 0] }} transition={{ duration: 10, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute top-40 -right-32 w-80 h-80 bg-blue-400 opacity-20 rounded-full blur-3xl z-0" animate={{ y: [0, -20, 0], x: [0, -30, 0] }} transition={{ duration: 12, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute bottom-0 left-1/2 w-72 h-72 bg-purple-400 opacity-20 rounded-full blur-3xl z-0" animate={{ y: [0, 20, 0], x: [0, 10, 0] }} transition={{ duration: 14, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute top-10 left-1/3 w-40 h-40 bg-yellow-300 opacity-20 rounded-full blur-2xl z-0" animate={{ scale: [1, 1.1, 1] }} transition={{ duration: 8, repeat: Infinity, repeatType: "mirror" }} />
      <motion.div className="absolute bottom-10 right-1/4 w-32 h-32 bg-green-300 opacity-20 rounded-full blur-2xl z-0" animate={{ scale: [1, 0.95, 1] }} transition={{ duration: 9, repeat: Infinity, repeatType: "mirror" }} />
    </>
  );
}

export default function ResetPasswordPage() {
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [token, setToken] = useState("");
  const [isValidToken, setIsValidToken] = useState(true);
  const router = useRouter();
  const searchParams = useSearchParams();

  const [notification, setNotification] = useState<{
    type: 'success' | 'error';
    message: string;
    isVisible: boolean;
  }>({
    type: 'success',
    message: '',
    isVisible: false
  });

  useEffect(() => {
    const tokenParam = searchParams.get('token');
    if (!tokenParam) {
      setIsValidToken(false);
      setNotification({
        type: 'error',
        message: 'Invalid or missing reset token',
        isVisible: true
      });
    } else {
      setToken(tokenParam);
    }
  }, [searchParams]);

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (newPassword !== confirmPassword) {
      setNotification({
        type: 'error',
        message: 'Passwords do not match',
        isVisible: true
      });
      return;
    }

    if (newPassword.length < 6) {
      setNotification({
        type: 'error',
        message: 'Password must be at least 6 characters long',
        isVisible: true
      });
      return;
    }

    setLoading(true);
    setNotification({ type: 'success', message: '', isVisible: false });
    
    try {
      await resetPassword(token, newPassword);
      setNotification({
        type: 'success',
        message: 'Password reset successfully! Redirecting to login...',
        isVisible: true
      });
      
      // Redirect to login after 2 seconds
      setTimeout(() => {
        router.push('/login');
      }, 2000);
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err.response?.data?.message || "Failed to reset password",
        isVisible: true
      });
    } finally {
      setLoading(false);
    }
  }, [token, newPassword, confirmPassword, router]);

  const closeNotification = () => {
    setNotification(prev => ({ ...prev, isVisible: false }));
  };

  if (!isValidToken) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-red-50 to-white relative overflow-hidden">
        <AnimatedBlobs />
        <motion.div
          className="relative z-10 bg-white/80 shadow-xl rounded-2xl p-8 w-full max-w-md border border-gray-100 backdrop-blur-lg"
          initial={{ opacity: 0, y: 40 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7 }}
        >
          <h1 className="text-2xl font-bold text-gray-800 mb-2 text-center">Invalid Reset Link</h1>
          <p className="text-gray-600 text-center mb-6">The password reset link is invalid or has expired.</p>
          <div className="text-center">
            <Link href="/forgot-password" className="text-indigo-500 hover:underline font-medium">
              Request a new reset link
            </Link>
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

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-50 to-white relative overflow-hidden">
      <AnimatedBlobs />
      <motion.div
        className="relative z-10 bg-white/80 shadow-xl rounded-2xl p-8 w-full max-w-md border border-gray-100 backdrop-blur-lg"
        initial={{ opacity: 0, y: 40 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.7 }}
      >
        <h1 className="text-2xl font-bold text-gray-800 mb-2 text-center">Reset Your Password</h1>
        <p className="text-gray-600 text-center mb-6">Enter your new password below.</p>
        
        <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
          <input 
            type="password" 
            placeholder="New password" 
            className="px-4 py-3 rounded-lg border border-gray-200 focus:outline-none focus:ring-2 focus:ring-green-400 bg-white/90 text-gray-700" 
            autoComplete="new-password" 
            required 
            value={newPassword} 
            onChange={e => setNewPassword(e.target.value)} 
          />
          <input 
            type="password" 
            placeholder="Confirm new password" 
            className="px-4 py-3 rounded-lg border border-gray-200 focus:outline-none focus:ring-2 focus:ring-green-400 bg-white/90 text-gray-700" 
            autoComplete="new-password" 
            required 
            value={confirmPassword} 
            onChange={e => setConfirmPassword(e.target.value)} 
          />
          <button 
            type="submit" 
            className="mt-2 py-3 rounded-lg bg-gradient-to-r from-green-500 to-blue-500 text-white font-semibold text-lg shadow-md hover:scale-105 transition-transform" 
            disabled={loading}
          >
            {loading ? "Resetting..." : "Reset Password"}
          </button>
        </form>
        
        <div className="mt-6 text-center text-gray-500 text-sm">
          Remember your password?{' '}
          <Link href="/login" className="text-green-500 hover:underline font-medium">Back to Login</Link>
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