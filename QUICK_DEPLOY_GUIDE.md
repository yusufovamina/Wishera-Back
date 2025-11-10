# 🚀 Quick Deploy Guide

## TL;DR - 3 Simple Steps

### 1️⃣ Fix Vercel Environment Variable (2 minutes)

Go to https://vercel.com/dashboard:
1. Select your Wishera project
2. Settings → Environment Variables
3. Find `NEXT_PUBLIC_API_URL`
4. **Change to**: `https://wishera-app.onrender.com/api` (with `/api` at the end!)
5. Save

### 2️⃣ Deploy Backend on Render.com (5 minutes)

Go to https://render.com/dashboard and click "Manual Deploy" for:
1. **wishera-app** (API Gateway) ⭐ **START HERE**
2. **wishera-user-service**
3. **wishera-gift-service**

Wait for all builds to complete (10-15 minutes).

### 3️⃣ Deploy Frontend on Vercel (2 minutes)

**Option A - Git Push:**
```bash
cd /Users/amina/Downloads/Wishera-Front-master
git add .
git commit -m "Fix: Use API Gateway for all requests"
git push
```

**Option B - Manual Redeploy:**
1. Go to https://vercel.com/dashboard
2. Select your project
3. Deployments → Redeploy

---

## That's It! 🎉

After deployment completes:
- ✅ Web app: https://wishera.vercel.app
- ✅ API: https://wishera-app.onrender.com/api
- ✅ Mobile app: Already configured to use production API

---

## What Was Fixed?

### The Problem
- Frontend was calling URLs **without** `/api`: `https://wishera-app.onrender.com/notifications/birthdays` ❌
- Should be: `https://wishera-app.onrender.com/api/notifications/birthdays` ✅

### The Solution
1. ✅ Added lowercase route aliases to all backend controllers
2. ✅ Frontend now calls API Gateway for everything
3. ✅ Mobile app uses production API Gateway by default
4. ✅ Fixed Redis timeouts with circuit breaker
5. ✅ Fixed CORS issues
6. ✅ Fixed Docker builds

---

## Test After Deployment

Open https://wishera.vercel.app and check:
- ✅ Can login/register
- ✅ Dashboard shows feed
- ✅ Notifications load (no 404 errors)
- ✅ User profile loads (no timeouts)
- ✅ No CORS errors in browser console

Open browser DevTools (F12) and verify all API calls use:
```
https://wishera-app.onrender.com/api/...
```

---

## Troubleshooting

**Still seeing 404 errors?**
1. Check Vercel env var includes `/api`
2. Redeploy frontend after changing env var
3. Hard refresh browser (Cmd+Shift+R on Mac, Ctrl+Shift+R on Windows)

**Services showing offline on Render?**
- Free tier services spin down after 15 min of inactivity
- First request will wake them up (takes ~30 seconds)

**Mobile app not connecting?**
1. Open `/Users/amina/Wishera-Mobile/src/api/client.ts`
2. Check `const USE_LOCAL_BACKEND = false;`
3. Restart Expo dev server

---

## Quick Links

- **Render Dashboard**: https://render.com/dashboard
- **Vercel Dashboard**: https://vercel.com/dashboard
- **Web App**: https://wishera.vercel.app
- **API Gateway**: https://wishera-app.onrender.com/api/health

---

**Need detailed steps?** See `DEPLOY_CHECKLIST.md`

**Need help?** Ask in this chat! 💬

