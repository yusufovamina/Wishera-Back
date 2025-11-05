# Frontend Configuration Guide

## Issue: Mixed Content Error

The frontend is trying to call `http://localhost:5219/api/auth/login` directly, which causes a mixed content error when deployed on HTTPS (Vercel).

## Solution

The frontend should **NOT** call auth-service directly. Instead, it should call the **API Gateway (WishlistApp)**.

### Frontend Environment Variables (Vercel)

Set these environment variables in your Vercel project:

```env
NEXT_PUBLIC_API_URL=https://wishera-app.onrender.com
```

**DO NOT** set:
- `NEXT_PUBLIC_AUTH_API_URL` (should not exist)
- Direct calls to auth-service URLs

### Frontend API Calls

The frontend should make requests to:
- ✅ `https://wishera-app.onrender.com/api/auth/login` (via API Gateway)
- ❌ `http://localhost:5219/api/auth/login` (direct to auth-service - WRONG)

### API Gateway Routes

The API Gateway (WishlistApp) provides these endpoints:
- `/api/auth/login` → forwards to auth-service
- `/api/auth/register` → forwards to auth-service
- `/api/auth/forgot-password` → forwards to auth-service
- `/api/auth/reset-password` → forwards to auth-service
- `/api/auth/check-email` → forwards to auth-service
- `/api/auth/check-username` → forwards to auth-service
- `/api/users/*` → forwards to user-service
- `/api/wishlists/*` → forwards to gift-wishlist-service
- `/api/gifts/*` → forwards to gift-wishlist-service

### Example Frontend Code

```typescript
// ✅ CORRECT - Use API Gateway
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://wishera-app.onrender.com';
const response = await fetch(`${API_URL}/api/auth/login`, { ... });

// ❌ WRONG - Direct to auth-service
const response = await fetch('http://localhost:5219/api/auth/login', { ... });
```

## Backend Service URLs (Internal)

These are for backend-to-backend communication only:

- `AuthServiceUrl`: `https://wishera-auth-service.onrender.com`
- `UserServiceUrl`: `https://wishera-user-service.onrender.com`
- `ChatServiceUrl`: `https://wishera-chat-service.onrender.com`

The frontend should **never** call these directly.

