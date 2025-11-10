# 🚀 Wishera Deployment Checklist

## ✅ Code Changes Completed

All code fixes have been applied to both backend and frontend:

### Backend Changes
- ✅ Fixed `NotificationsController` - added lowercase route alias
- ✅ Fixed `UsersController` - added HTTP fallback and lowercase routes
- ✅ Fixed `WishlistsController` - added lowercase routes
- ✅ Fixed `GiftController` - correct `CreateGiftAsync` signature
- ✅ Fixed Redis connection - circuit breaker pattern, optional caching
- ✅ Fixed CORS - comprehensive OPTIONS handler
- ✅ Fixed Docker builds - correct project references
- ✅ Fixed `gift-wishlist-service` - extern alias configuration
- ✅ All controllers have lowercase route aliases (`/api/notifications`, `/api/users`, `/api/wishlists`)

### Frontend Changes (Web)
- ✅ Updated `api.ts` - all calls go through API Gateway
- ✅ Fixed `getGiftApiUrl()` and `getuserApiUrl()` to return API Gateway URL
- ✅ Fixed `ensureHttps()` fallback logic

### Frontend Changes (Mobile)
- ✅ Updated `client.ts` - uses production API Gateway by default
- ✅ Added `USE_LOCAL_BACKEND` flag for easy switching
- ✅ Proper platform detection (iOS, Android, Web)
- ✅ No localhost dependencies by default

## 📋 Deployment Steps

### Step 1: Deploy Backend Services on Render.com

Go to https://render.com/dashboard and trigger manual deploys for:

#### 1.1 Deploy API Gateway (WishlistApp) ⭐ **CRITICAL**
- **Service Name**: `wishera-app`
- **URL**: `https://wishera-app.onrender.com`
- **Action**: Click "Manual Deploy" → Select latest commit
- **Expected**: Build succeeds, service starts on port 5000

#### 1.2 Deploy User Service
- **Service Name**: `wishera-user-service`
- **URL**: `https://wishera-user-service.onrender.com`
- **Action**: Click "Manual Deploy" → Select latest commit
- **Expected**: Build succeeds, connects to MongoDB and RabbitMQ

#### 1.3 Deploy Gift/Wishlist Service
- **Service Name**: `wishera-gift-service`
- **URL**: `https://wishera-gift-service.onrender.com`
- **Action**: Click "Manual Deploy" → Select latest commit
- **Expected**: Build succeeds, connects to MongoDB and RabbitMQ

#### 1.4 Deploy Auth Service
- **Service Name**: `wishera-auth-service`
- **URL**: `https://wishera-auth-service.onrender.com`
- **Action**: Click "Manual Deploy" → Select latest commit
- **Expected**: Build succeeds, connects to MongoDB and RabbitMQ

#### 1.5 Deploy Chat Service
- **Service Name**: `wishera-chat-service`
- **URL**: `https://wishera-chat-service.onrender.com`
- **Action**: Click "Manual Deploy" → Select latest commit
- **Expected**: Build succeeds, SignalR hub running

### Step 2: Verify Backend Environment Variables

For each service on Render.com, verify these environment variables are set:

#### All Services Need:
```bash
MONGODB_CONNECTION_STRING=<your-mongodb-atlas-connection-string>
JWT_SECRET=<your-jwt-secret>
```

#### Services Using RabbitMQ:
```bash
RABBITMQ_HOST=<your-cloudamqp-host>
RABBITMQ_USERNAME=<your-cloudamqp-username>
RABBITMQ_PASSWORD=<your-cloudamqp-password>
```

#### User Service Specifically:
```bash
ConnectionStrings__Redis=<your-upstash-redis-connection>
# Format: rediss://default:password@host:6380
```

#### Gift/Wishlist Service:
```bash
CLOUDINARY_CLOUD_NAME=<your-cloudinary-name>
CLOUDINARY_API_KEY=<your-cloudinary-api-key>
CLOUDINARY_API_SECRET=<your-cloudinary-api-secret>
USER_SERVICE_URL=https://wishera-user-service.onrender.com
```

#### WishlistApp (API Gateway):
```bash
USER_SERVICE_URL=https://wishera-user-service.onrender.com
GIFT_SERVICE_URL=https://wishera-gift-service.onrender.com
AUTH_SERVICE_URL=https://wishera-auth-service.onrender.com
RABBITMQ_HOST=<your-cloudamqp-host>
RABBITMQ_USERNAME=<your-cloudamqp-username>
RABBITMQ_PASSWORD=<your-cloudamqp-password>
```

### Step 3: Fix Vercel Environment Variable (Web Frontend)

1. Go to https://vercel.com/dashboard
2. Select your Wishera project
3. Go to **Settings** → **Environment Variables**
4. Find or add `NEXT_PUBLIC_API_URL`
5. **Set value to**: `https://wishera-app.onrender.com/api` ⚠️ **MUST include `/api`**
6. Click **Save**

**OR** delete the variable to use the default value (which already includes `/api`)

### Step 4: Deploy Web Frontend to Vercel

```bash
cd /Users/amina/Downloads/Wishera-Front-master

# Commit the latest changes
git add .
git commit -m "Fix: Route all API calls through API Gateway"

# Push to trigger Vercel deployment
git push origin main
```

**OR** manually trigger deployment on Vercel:
1. Go to https://vercel.com/dashboard
2. Select your project
3. Go to **Deployments** tab
4. Click "Redeploy"

### Step 5: Mobile App (Optional - No deployment needed for testing)

The mobile app is already configured to use the production API Gateway.

**To test locally:**
```bash
cd /Users/amina/Wishera-Mobile

# Start Expo dev server
npx expo start

# Then press:
# - 'i' for iOS Simulator
# - 'a' for Android Emulator
```

**To deploy to App Store/Play Store:**
```bash
# Build for iOS
npx expo build:ios

# Build for Android
npx expo build:android
```

## 🧪 Testing After Deployment

### Test Web Frontend (https://wishera.vercel.app)

1. **Login/Register**
   - ✅ Can create new account
   - ✅ Can login with existing account
   - ✅ JWT token stored in localStorage

2. **User Profile**
   - ✅ Can view own profile at `/profile`
   - ✅ Can view other user's profile at `/profile/[userId]`
   - ✅ Can edit profile (name, bio, birthday)
   - ✅ Can upload avatar image

3. **Feed**
   - ✅ Dashboard shows wishlist feed at `/`
   - ✅ Can scroll and paginate
   - ✅ Can like/unlike wishlists

4. **Wishlists**
   - ✅ Can create new wishlist
   - ✅ Can view wishlist details
   - ✅ Can edit/delete own wishlists
   - ✅ Can add gifts to wishlist

5. **Notifications**
   - ✅ Bell icon shows unread count
   - ✅ `/notifications` page loads
   - ✅ Birthday reminders show
   - ✅ Can mark notifications as read

6. **Search & Social**
   - ✅ Can search for users
   - ✅ Can follow/unfollow users
   - ✅ Can view followers/following lists

7. **Events**
   - ✅ Can create events
   - ✅ Can invite users to events
   - ✅ Can respond to invitations

### Test Mobile App

1. **Run on iOS Simulator**
   ```bash
   cd /Users/amina/Wishera-Mobile
   npx expo start
   # Press 'i' for iOS
   ```

2. **Verify API Logs**
   Check Metro console for:
   ```
   === Wishera Mobile API Configuration ===
   Platform: ios
   Environment: Development
   Using API Gateway: true
   Auth Service URL: https://wishera-app.onrender.com/api
   User Service URL: https://wishera-app.onrender.com/api
   Wishlist Service URL: https://wishera-app.onrender.com/api
   Chat Service URL: https://wishera-app.onrender.com/api
   =====================================
   ```

3. **Test Core Features**
   - ✅ Login/Register
   - ✅ View feed
   - ✅ View profile
   - ✅ Like wishlists
   - ✅ Search users
   - ✅ Notifications

### Check Browser Console

Open DevTools (F12) on https://wishera.vercel.app and verify:

1. **No 404 errors** for:
   - `/api/notifications/birthdays`
   - `/api/notifications/unread-count`
   - `/api/wishlists/feed`
   - `/api/users/[id]`

2. **No CORS errors**

3. **All API calls use correct URL format:**
   ```
   https://wishera-app.onrender.com/api/...
   ```

   **NOT:**
   ```
   https://wishera-app.onrender.com/notifications/...  ❌ Missing /api
   http://localhost:5001/...  ❌ Wrong host
   ```

## 🐛 Troubleshooting

### Issue: 404 errors for `/api/notifications`, `/api/wishlists/feed`

**Cause**: Backend not deployed or Vercel env var missing `/api`

**Fix**:
1. Deploy backend services on Render
2. Update Vercel env var: `NEXT_PUBLIC_API_URL=https://wishera-app.onrender.com/api`
3. Redeploy frontend on Vercel

### Issue: CORS errors

**Cause**: Backend CORS middleware not configured

**Fix**:
1. Backend already has CORS fixes applied
2. Redeploy backend services on Render
3. Ensure frontend calls API Gateway, not microservices directly

### Issue: 502 Bad Gateway or Service Unavailable

**Cause**: Backend services not running or RabbitMQ/MongoDB connection failed

**Fix**:
1. Check Render dashboard - all services should show "Live" status
2. Check service logs on Render for connection errors
3. Verify environment variables (MongoDB, RabbitMQ, Redis)
4. Services on free tier spin down after inactivity - first request will be slow

### Issue: Mobile app can't connect

**Cause**: `USE_LOCAL_BACKEND` set to `true` but no local backend running

**Fix**:
1. Open `/Users/amina/Wishera-Mobile/src/api/client.ts`
2. Set `const USE_LOCAL_BACKEND = false;`
3. Restart Expo dev server

### Issue: Authentication fails

**Cause**: JWT secret mismatch between services

**Fix**:
1. All services must use the same `JWT_SECRET` environment variable
2. Update on Render.com for all services
3. Redeploy all services

### Issue: Images not uploading

**Cause**: Cloudinary credentials missing

**Fix**:
1. Add Cloudinary env vars to `gift-wishlist-service` on Render
2. Redeploy `gift-wishlist-service`

## ✅ Success Criteria

Your deployment is successful when:

1. ✅ All 5 backend services show "Live" on Render dashboard
2. ✅ Web frontend loads at https://wishera.vercel.app without errors
3. ✅ Can login/register successfully
4. ✅ Dashboard shows wishlist feed
5. ✅ Notifications load without 404 errors
6. ✅ User profile loads without timeout errors
7. ✅ Browser console shows no CORS errors
8. ✅ All API calls use `https://wishera-app.onrender.com/api/...` format
9. ✅ Mobile app connects to production API (if testing mobile)

## 🎯 Priority Order

If you're short on time, deploy in this order:

1. **WishlistApp (API Gateway)** - Most critical, everything depends on this
2. **user-service** - Required for authentication and profiles
3. **gift-wishlist-service** - Required for feed and wishlists
4. **Vercel frontend** - Update env var and redeploy
5. auth-service - Can use existing deployment if working
6. chat-service - Can be deployed later for chat functionality

## 📞 Need Help?

- **Render Dashboard**: https://render.com/dashboard
- **Vercel Dashboard**: https://vercel.com/dashboard
- **Frontend Repo**: Check for `NEXT_PUBLIC_API_URL` in `.env.local`
- **Backend Logs**: Click on service in Render → Logs tab
- **Frontend Logs**: Vercel → Deployments → Click deployment → View Function Logs

---

**After following all steps, all services should be live and the app should work end-to-end!** 🎉

