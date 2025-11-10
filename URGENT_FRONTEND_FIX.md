# 🔴 URGENT: Frontend Fix Required

## The Problem

Your frontend at `https://wishera.vercel.app/login` is trying to call:
```
http://localhost:5219/api/auth/login
```

This causes:
1. ❌ **Mixed Content Error** - HTTPS page trying to load HTTP content
2. ❌ **Blocked by Browser** - Browsers block insecure content on secure pages
3. ❌ **Won't Work in Production** - localhost doesn't exist in production

## ✅ The Solution

### Step 1: Add Environment Variable in Vercel

1. Go to your Vercel project: https://vercel.com/dashboard
2. Click on your `wishera` project
3. Go to **Settings** → **Environment Variables**
4. Add this variable:

```
Name: NEXT_PUBLIC_API_URL
Value: https://wishera-app.onrender.com
Environment: Production, Preview, Development (select all)
```

5. Click **Save**

### Step 2: Find and Fix Frontend Code

Search your frontend codebase for these patterns:

**Search for:**
- `localhost:5219`
- `http://localhost:5219`
- `5219/api/auth`
- Any hardcoded auth service URLs

**Replace with:**
```typescript
// Use environment variable
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://wishera-app.onrender.com';
```

### Step 3: Update API Calls

**❌ WRONG (Current Code):**
```typescript
// DON'T DO THIS
const response = await fetch('http://localhost:5219/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});
```

**✅ CORRECT (Fixed Code):**
```typescript
// DO THIS INSTEAD
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://wishera-app.onrender.com';
const response = await fetch(`${API_URL}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});
```

### Step 4: Common Files to Check

Look for these files in your frontend:

1. **API/Service files:**
   - `src/api/auth.ts` or `api/auth.js`
   - `src/services/authService.ts`
   - `src/lib/api.ts`
   - `src/utils/api.ts`

2. **Login/Register pages:**
   - `src/app/login/page.tsx` (or `pages/login.tsx`)
   - `src/app/register/page.tsx`
   - `src/components/LoginForm.tsx`

3. **Environment/config files:**
   - `.env.local`
   - `.env.production`
   - `next.config.js`

### Step 5: Example Fix Pattern

**Before:**
```typescript
// ❌ Hardcoded localhost
const login = async (email: string, password: string) => {
  const res = await fetch('http://localhost:5219/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  return res.json();
};
```

**After:**
```typescript
// ✅ Using environment variable
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'https://wishera-app.onrender.com';

const login = async (email: string, password: string) => {
  const res = await fetch(`${API_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });
  return res.json();
};
```

### Step 6: Redeploy

After making changes:
1. Commit your changes
2. Push to GitHub
3. Vercel will automatically redeploy
4. The new deployment will use the environment variable

## 🔍 Quick Search Commands

If you have access to your frontend repo, run these commands:

```bash
# Find all occurrences of localhost:5219
grep -r "localhost:5219" .

# Find all API calls
grep -r "api/auth/login" .

# Find fetch calls
grep -r "fetch.*5219" .
```

## ✅ Verification

After fixing, the frontend should:
- ✅ Call `https://wishera-app.onrender.com/api/auth/login`
- ✅ Not show mixed content errors
- ✅ Work correctly in production

## 📝 Summary

**Backend is correctly configured** - CORS allows `https://wishera.vercel.app`

**Frontend needs to be fixed** - Change from:
- `http://localhost:5219/api/auth/login` ❌
- To: `https://wishera-app.onrender.com/api/auth/login` ✅







