# Frontend Endpoint Fix - CORS & 404 Errors

## Problem

The frontend is calling microservices directly instead of using the API Gateway, causing:
1. **404 errors** on `/feed` endpoint
2. **500 errors** with CORS failures on `/search` and `/following`
3. Direct calls bypassing the API Gateway

## Root Cause

- Frontend is configured to call `user-service` directly: `https://wishera-user-service.onrender.com/api/users/...`
- It should call the API Gateway instead: `https://wishera-app.onrender.com/api/users/...`

## Solution

### Backend Changes (Completed)

1. **Added HTTP fallbacks** to all user endpoints in `WishlistApp/Controllers/UsersController.cs`:
   - `/api/users/search` - now has HTTP fallback
   - `/api/users/{id}/following` - now has HTTP fallback
   - `/api/users/{id}/followers` - now has HTTP fallback
   - `/api/users/{id}` - already had HTTP fallback

2. **Fixed route case sensitivity**:
   - Added lowercase route aliases: `[Route("api/users")]`
   - Both `/api/Users` and `/api/users` now work

3. **Fixed `/feed` endpoint**:
   - Endpoint exists at `/api/wishlists/feed` (lowercase works)
   - Missing `currentUserId` variable was causing the issue

### Frontend Changes Required

The frontend needs to update API URLs to use the API Gateway:

**Current (Incorrect):**
```javascript
// ❌ BAD - Calling microservice directly
const response = await axios.get('https://wishera-user-service.onrender.com/api/users/search?query=...');
```

**Correct:**
```javascript
// ✅ GOOD - Calling through API Gateway
const response = await axios.get('https://wishera-app.onrender.com/api/users/search?query=...');
```

### Frontend Environment Variables (Vercel)

Update these environment variables on Vercel:

```bash
# API Gateway (main backend URL)
NEXT_PUBLIC_API_URL=https://wishera-app.onrender.com

# Auth Service
NEXT_PUBLIC_AUTH_URL=https://wishera-auth-service.onrender.com
```

### Frontend Code Changes

Replace all direct microservice calls with API Gateway calls:

1. **User Search:**
   ```javascript
   // Before
   axios.get(`${process.env.NEXT_PUBLIC_USER_SERVICE_URL}/api/users/search`)
   
   // After
   axios.get(`${process.env.NEXT_PUBLIC_API_URL}/api/users/search`)
   ```

2. **Following/Followers:**
   ```javascript
   // Before
   axios.get(`${process.env.NEXT_PUBLIC_USER_SERVICE_URL}/api/users/${userId}/following`)
   
   // After
   axios.get(`${process.env.NEXT_PUBLIC_API_URL}/api/users/${userId}/following`)
   ```

3. **Feed:**
   ```javascript
   // Before
   axios.get(`${process.env.NEXT_PUBLIC_GIFT_SERVICE_URL}/api/wishlists/feed`)
   
   // After
   axios.get(`${process.env.NEXT_PUBLIC_API_URL}/api/wishlists/feed`)
   ```

4. **Notifications:**
   ```javascript
   // Before
   axios.get(`${process.env.NEXT_PUBLIC_USER_SERVICE_URL}/api/notifications/unread-count`)
   
   // After
   axios.get(`${process.env.NEXT_PUBLIC_API_URL}/api/notifications/unread-count`)
   ```

## Why This Matters

1. **API Gateway benefits:**
   - Single entry point for all API calls
   - Built-in fallback mechanisms (RabbitMQ → HTTP)
   - Better error handling and logging
   - Consistent authentication

2. **CORS issues resolved:**
   - API Gateway has proper CORS configuration
   - All requests go through one domain
   - No cross-origin issues

3. **Resilience:**
   - If RabbitMQ fails, HTTP fallback kicks in automatically
   - Services can still communicate even if message queue is down

## Testing

After deploying backend changes and updating frontend:

1. Check `/feed` endpoint: `https://wishera-app.onrender.com/api/wishlists/feed`
2. Check search: `https://wishera-app.onrender.com/api/users/search?query=test`
3. Check following: `https://wishera-app.onrender.com/api/users/{userId}/following`

All should return proper responses with CORS headers.

## Deployment Steps

1. **Backend (Done):**
   - Deploy updated `WishlistApp` to Render.com
   - Deploy updated `user-service` to Render.com  
   - Deploy updated `gift-wishlist-service` to Render.com

2. **Frontend (Required):**
   - Update environment variables on Vercel
   - Update all API calls to use `NEXT_PUBLIC_API_URL`
   - Remove direct microservice URL references
   - Deploy to Vercel

## Current Status

✅ Backend is ready and deployed
❌ Frontend needs to be updated to use API Gateway
❌ Frontend environment variables need to be updated on Vercel

**Once the frontend is updated, all endpoints will work without CORS or 404 errors.**

