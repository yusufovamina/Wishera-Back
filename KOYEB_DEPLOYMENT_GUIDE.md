# Koyeb Deployment Guide for Wishera Backend

## 🏗️ Architecture Overview

Your backend consists of 4 microservices:
1. **wishera-app** (Main API) - Port 5155
2. **auth-service** (Authentication) - Port 5219
3. **gift-wishlist-service** (Gifts & Wishlists) - Port 5300
4. **user-service** (User Management) - Port 5400
5. **chat-service** (Chat/WebSocket) - Port 5002

Plus dependencies: MongoDB, Redis, RabbitMQ

## 📋 Prerequisites

1. Push your backend code to GitHub (create a new repository)
2. Create a Koyeb account
3. Have Koyeb secrets ready for environment variables

## 🚀 Step-by-Step Deployment

### Step 1: Deploy Message Queue (RabbitMQ)

1. Go to Koyeb Dashboard → Create Service
2. Select "Deploy from a Docker Hub image"
3. Image: `rabbitmq:3-management`
4. Name: `wishera-rabbitmq`
5. Add environment variables:
   ```
   RABBITMQ_DEFAULT_USER=guest
   RABBITMQ_DEFAULT_PASS=guest
   ```
6. Click "Deploy"
7. Note the service URL (e.g., `wishera-rabbitmq.wishera-1234.koyeb.app`)

### Step 2: Deploy Database (MongoDB Atlas)

1. Create a free MongoDB Atlas account at https://www.mongodb.com/atlas
2. Create a cluster
3. Get your connection string
4. Create a Koyeb Secret:
   - Secret name: `MONGO_CONNECTION_STRING`
   - Value: Your MongoDB connection string

### Step 3: Deploy Redis (Koyeb manages this automatically or use Redis Cloud)

1. Go to https://redis.com/try-free/ (Free tier available)
2. Get connection details
3. Create Koyeb Secret for Redis URL

### Step 4: Deploy Auth Service

1. Create Service → Deploy from a Dockerfile
2. Connect your GitHub repository
3. Configuration:
   - **Service Name**: `wishera-auth-service`
   - **Build Directory**: `/`
   - **Dockerfile Path**: `auth-service/Dockerfile`
   - **Port**: `5219`
4. Environment Variables (Create Koyeb Secrets):
   ```
   ASPNETCORE_URLS=http://+:5219
   RabbitMq__HostName=wishera-rabbitmq.wishera-1234.koyeb.app
   RabbitMq__UserName=guest
   RabbitMq__Password=guest
   RabbitMq__Port=5672
   ```
5. Click "Deploy"

### Step 5: Deploy Gift-Wishlist Service

1. Create Service → Deploy from a Dockerfile
2. **Service Name**: `wishera-gift-service`
3. **Build Directory**: `/`
4. **Dockerfile Path**: `gift-wishlist-service/Dockerfile`
5. **Port**: `5300`
6. Environment Variables:
   ```
   ASPNETCORE_URLS=http://+:5300
   RabbitMq__HostName=wishera-rabbitmq.wishera-1234.koyeb.app
   RabbitMq__UserName=guest
   RabbitMq__Password=guest
   RabbitMq__Port=5672
   ConnectionStrings__Redis=<redis-url>
   ```

### Step 6: Deploy User Service

1. **Service Name**: `wishera-user-service`
2. **Dockerfile Path**: `user-service/Dockerfile`
3. **Port**: `5400`
4. Same environment variables pattern as above

### Step 7: Deploy Main API (WisheraApp)

1. **Service Name**: `wishera-app`
2. **Dockerfile Path**: `WishlistApp/Dockerfile`
3. **Port**: `5155`

### Step 8: Deploy Chat Service

1. **Service Name**: `wishera-chat-service`
2. **Dockerfile Path**: `chat-server-side/Dockerfile`
3. **Port**: `5002`
4. Environment Variables:
   ```
   ASPNETCORE_URLS=http://+:5002
   ChatMongo:ConnectionString=<mongodb-connection-string>
   ```

## 🔗 Update Frontend Environment Variables

Go to Vercel → Your Project → Settings → Environment Variables

Add these variables with your Koyeb service URLs:

```env
NEXT_PUBLIC_API_URL=https://wishera-app.your-domain.koyeb.app
NEXT_PUBLIC_AUTH_API_URL=https://wishera-auth-service.your-domain.koyeb.app
NEXT_PUBLIC_GIFT_API_URL=https://wishera-gift-service.your-domain.koyeb.app
NEXT_PUBLIC_USER_API_URL=https://wishera-user-service.your-domain.koyeb.app
NEXT_PUBLIC_CHAT_API_URL=https://wishera-chat-service.your-domain.koyeb.app
```

## 🎯 Service URLs Pattern

Once deployed, each service will have a URL like:
- `https://wishera-app-XXXXX.koyeb.app`
- `https://wishera-auth-service-XXXXX.koyeb.app`
- etc.

## 🔒 Security Notes

1. Use strong passwords for production
2. Use HTTPS (Koyeb provides this automatically)
3. Store sensitive data in Koyeb Secrets
4. Configure CORS properly in your backend services

## 📊 Monitoring

- Check service logs in Koyeb Dashboard
- Monitor service health
- Set up alerts if needed

## 🐛 Troubleshooting

- Check service logs for errors
- Verify environment variables are set correctly
- Ensure MongoDB connection string is valid
- Test RabbitMQ connectivity

