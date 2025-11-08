# Complete Render.com Deployment Guide

## Problem: "After Login, Nothing Works"

**Root Cause:** The `gift-wishlist-service` is not deployed, so all API calls that require wishlist/gift data fail. The API Gateway (WishlistApp) tries to communicate with the gift-wishlist-service via RabbitMQ, but since it's not running, all RPC calls timeout.

## Required Services

You need to deploy **5 services** on Render.com:

1. ✅ **auth-service** - Authentication (already deployed)
2. ✅ **user-service** - User profiles (already deployed)  
3. ❌ **gift-wishlist-service** - Wishlists and gifts (**NOT DEPLOYED - THIS IS THE PROBLEM**)
4. ✅ **WishlistApp** - API Gateway (already deployed)
5. ⚠️ **chat-server-side** - Chat service (optional)

## Step 1: Deploy gift-wishlist-service

### 1.1 Create New Web Service on Render.com

1. Go to Render.com Dashboard
2. Click **"New +"** → **"Web Service"**
3. Connect your GitHub repository: `yusufovamina/Wishera-Back`
4. Configure:
   - **Name**: `wishera-gift-wishlist-service`
   - **Environment**: `Docker`
   - **Dockerfile Path**: `gift-wishlist-service/Dockerfile`
   - **Docker Context**: `.` (root directory)

### 1.2 Set Environment Variables

Add these environment variables in Render.com:

```env
# Port (Render automatically sets PORT, but set it explicitly)
PORT=5003

# MongoDB Connection
ConnectionStrings__MongoDB=mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster

# JWT Configuration (must match other services)
Jwt__Key=pVmLpajKuCFDq59YgjiYRZaOuywYlSe
Jwt__Issuer=WisheraApp

# Cloudinary (for image uploads)
Cloudinary__CloudName=dg6mdf9cv
Cloudinary__ApiKey=358343523359213
Cloudinary__ApiSecret=b53qJ_JBN8DdKYae3WcyJVfHPvg

# Redis (your Render Redis instance)
ConnectionStrings__Redis=redis://red-d4557s95pdvs73fi4d5g:6379

# RabbitMQ (from CloudAMQP)
RABBITMQ_HOSTNAME=your-rabbitmq-hostname
RABBITMQ_USERNAME=your-rabbitmq-username
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_PORT=5672
RABBITMQ_VIRTUALHOST=your-username  # Usually same as username on CloudAMQP

# User Service URL (for user lookups)
Services__UserService=https://wishera-user-service.onrender.com
```

### 1.3 Deploy

Click **"Create Web Service"** and wait for deployment.

### 1.4 Verify Deployment

```bash
curl https://wishera-gift-wishlist-service.onrender.com/health
```

Should return: `"Healthy"`

## Step 2: Verify All Services Are Running

### 2.1 Check Service Health

```bash
# Auth Service
curl https://wishera-auth-service.onrender.com/health

# User Service  
curl https://wishera-user-service.onrender.com/health

# Gift Wishlist Service
curl https://wishera-gift-wishlist-service.onrender.com/health

# API Gateway
curl https://wishera-app.onrender.com/health
```

All should return `"Healthy"`.

### 2.2 Check RabbitMQ Configuration

**IMPORTANT:** All services must use the **SAME** RabbitMQ credentials:

- `RABBITMQ_HOSTNAME`
- `RABBITMQ_USERNAME`
- `RABBITMQ_PASSWORD`
- `RABBITMQ_PORT`
- `RABBITMQ_VIRTUALHOST`

**On CloudAMQP/Render.com:**
- VirtualHost is usually the same as the username
- If `VirtualHost` is `"/"` and `UserName` is not `"guest"`, the code automatically uses the username as the virtual host

## Step 3: Update Environment Variables for All Services

### 3.1 WishlistApp (API Gateway)

Make sure these are set:

```env
PORT=5000
Jwt__Key=pVmLpajKuCFDq59YgjiYRZaOuywYlSe
Jwt__Issuer=WisheraApp
AuthServiceUrl=https://wishera-auth-service.onrender.com
UserServiceUrl=https://wishera-user-service.onrender.com
ChatServiceUrl=https://wishera-chat-service.onrender.com
RABBITMQ_HOSTNAME=your-rabbitmq-hostname
RABBITMQ_USERNAME=your-rabbitmq-username
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_PORT=5672
RABBITMQ_VIRTUALHOST=your-username
```

### 3.2 auth-service

```env
PORT=5219
ConnectionStrings__MongoDB=mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster
Jwt__Key=pVmLpajKuCFDq59YgjiYRZaOuywYlSe
Jwt__Issuer=WisheraApp
Email__SmtpServer=smtp.gmail.com
Email__SmtpPort=587
Email__Username=yusufovamina@gmail.com
Email__Password=your-gmail-app-password
Email__FromEmail=yusufovamina@gmail.com
Email__FromName=Wishera
Frontend__BaseUrl=https://your-frontend-url.vercel.app
RABBITMQ_HOSTNAME=your-rabbitmq-hostname
RABBITMQ_USERNAME=your-rabbitmq-username
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_PORT=5672
RABBITMQ_VIRTUALHOST=your-username
```

### 3.3 user-service

```env
PORT=5001
ConnectionStrings__MongoDB=mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster
Jwt__Key=pVmLpajKuCFDq59YgjiYRZaOuywYlSe
Jwt__Issuer=WisheraApp
Services__AuthService=https://wishera-auth-service.onrender.com
RABBITMQ_HOSTNAME=your-rabbitmq-hostname
RABBITMQ_USERNAME=your-rabbitmq-username
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_PORT=5672
RABBITMQ_VIRTUALHOST=your-username
```

### 3.4 gift-wishlist-service

```env
PORT=5003
ConnectionStrings__MongoDB=mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster
Jwt__Key=pVmLpajKuCFDq59YgjiYRZaOuywYlSe
Jwt__Issuer=WisheraApp
Cloudinary__CloudName=dg6mdf9cv
Cloudinary__ApiKey=358343523359213
Cloudinary__ApiSecret=b53qJ_JBN8DdKYae3WcyJVfHPvg
ConnectionStrings__Redis=redis://red-d4557s95pdvs73fi4d5g:6379
Services__UserService=https://wishera-user-service.onrender.com
RABBITMQ_HOSTNAME=your-rabbitmq-hostname
RABBITMQ_USERNAME=your-rabbitmq-username
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_PORT=5672
RABBITMQ_VIRTUALHOST=your-username
```

## Step 4: Test the Fix

### 4.1 Test Login

```bash
curl -X POST https://wishera-app.onrender.com/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"your-email@example.com","password":"your-password"}'
```

Should return a JWT token.

### 4.2 Test Feed (After Login)

```bash
# Replace YOUR_TOKEN with the JWT from login
curl -X GET https://wishera-app.onrender.com/api/wishlists/feed \
  -H "Authorization: Bearer YOUR_TOKEN"
```

Should return a list of wishlists (even if empty), not a 404 or timeout.

### 4.3 Test Other Endpoints

```bash
# Get user wishlists
curl -X GET https://wishera-app.onrender.com/api/wishlists/user/USER_ID \
  -H "Authorization: Bearer YOUR_TOKEN"

# Get categories
curl -X GET https://wishera-app.onrender.com/api/wishlists/categories \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Step 5: Update Frontend

### 5.1 Vercel Environment Variables

Make sure your frontend (Vercel) has:

```env
NEXT_PUBLIC_API_URL=https://wishera-app.onrender.com
```

**Important:** Use the API Gateway URL (`wishera-app.onrender.com`), not individual service URLs.

### 5.2 Verify Frontend API Calls

Check your browser console. After login, API calls should go to:
- `https://wishera-app.onrender.com/api/wishlists/feed`
- `https://wishera-app.onrender.com/api/wishlists/user/...`
- etc.

## Troubleshooting

### Error: "RPC call timed out after 30 seconds"

**Cause:** gift-wishlist-service is not running or RabbitMQ is misconfigured.

**Fix:**
1. Verify gift-wishlist-service is deployed: `curl https://wishera-gift-wishlist-service.onrender.com/health`
2. Check RabbitMQ credentials are identical across all services
3. Check Render.com logs for gift-wishlist-service for connection errors

### Error: "404 Not Found" on `/feed`

**Cause:** Frontend is calling the wrong URL or the endpoint doesn't exist.

**Fix:**
1. Verify the endpoint exists: `GET /api/wishlists/feed`
2. Check frontend is using: `https://wishera-app.onrender.com/api/wishlists/feed`
3. Make sure you're sending the JWT token in the `Authorization` header

### Error: "401 Unauthorized"

**Cause:** JWT token is missing, invalid, or expired.

**Fix:**
1. Login again to get a new token
2. Make sure the token is sent in the `Authorization: Bearer TOKEN` header
3. Verify `Jwt__Key` is identical across all services

### Error: "502 Bad Gateway" or "Service temporarily unavailable"

**Cause:** RabbitMQ connection failed or service is not responding.

**Fix:**
1. Check Render.com logs for the service
2. Verify RabbitMQ credentials
3. Check if the service is running: `curl https://service-url.onrender.com/health`

## Summary

**The main issue:** gift-wishlist-service is not deployed, causing all post-login API calls to fail.

**The fix:**
1. Deploy gift-wishlist-service on Render.com
2. Configure RabbitMQ credentials correctly
3. Verify all services are healthy
4. Test the API endpoints

After deploying gift-wishlist-service, your app should work after login! 🎉

