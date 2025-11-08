# Fix: CORS and 502 Errors for Notifications

## ✅ What I Fixed

### 1. Added Notification Endpoints to API Gateway
- Created `WishlistApp/Controllers/NotificationsController.cs`
- Frontend can now call `https://wishera-app.onrender.com/api/Notifications/...` instead of calling user-service directly
- This avoids CORS issues and provides a single entry point

### 2. Improved CORS Handling in user-service
- Added OPTIONS request handler for CORS preflight
- Registered exception middleware properly
- Ensured CORS headers are always set, even on errors

### 3. Fixed HttpClient Usage
- Updated to use `IHttpClientFactory` for better connection management
- Added timeout handling

## 🔧 What You Need to Do

### Update Frontend API URLs

Change your frontend to use the API Gateway instead of calling services directly:

**Before:**
```javascript
// ❌ Don't call services directly
https://wishera-user-service.onrender.com/api/Notifications/birthdays
https://wishera-user-service.onrender.com/api/Notifications/unread-count
```

**After:**
```javascript
// ✅ Call through API Gateway
https://wishera-app.onrender.com/api/Notifications/birthdays
https://wishera-app.onrender.com/api/Notifications/unread-count
```

### Available Endpoints (through API Gateway)

- `GET /api/Notifications` - Get all notifications
- `GET /api/Notifications/unread-count` - Get unread count
- `GET /api/Notifications/birthdays?daysAhead=365` - Get upcoming birthdays
- `PUT /api/Notifications/{id}/read` - Mark as read
- `DELETE /api/Notifications/{id}` - Delete notification

## 🔍 Troubleshooting 502 Errors

The 502 errors from user-service suggest internal issues. Check:

### 1. Redis Connection
- Verify `ConnectionStrings__Redis` is set correctly in user-service
- Check if Redis instance is running
- Test: `curl https://wishera-user-service.onrender.com/health`

### 2. RabbitMQ Connection
- Verify RabbitMQ credentials are correct
- Check if RabbitMQ instance is running
- Check Render.com logs for connection errors

### 3. Database Connection
- Verify MongoDB connection string is correct
- Check if database is accessible

### 4. Check Render.com Logs
- Go to user-service on Render.com
- Check "Logs" tab for error messages
- Look for:
  - Redis connection errors
  - RabbitMQ connection errors
  - Database connection errors
  - Exception stack traces

## 📝 Next Steps

1. **Update Frontend**: Change API URLs to use API Gateway
2. **Deploy Changes**: Push the updated code to trigger new deployments
3. **Check Logs**: Monitor Render.com logs for any errors
4. **Test Endpoints**: Test the notification endpoints through the API Gateway

## 🎯 Expected Behavior

After updating the frontend:
- ✅ No more CORS errors
- ✅ No more 502 errors (if user-service is working)
- ✅ All requests go through API Gateway
- ✅ Better error handling and logging

## 💡 Why This Helps

1. **Single Entry Point**: All requests go through API Gateway, avoiding CORS issues
2. **Better Error Handling**: API Gateway can handle errors gracefully
3. **Simplified Frontend**: Frontend only needs to know one URL
4. **Better Security**: Services aren't directly exposed to the frontend

## 🚨 If 502 Errors Persist

If you still get 502 errors after updating the frontend:

1. Check user-service logs on Render.com
2. Verify all environment variables are set correctly
3. Check if Redis/RabbitMQ services are running
4. Test the user-service health endpoint directly
5. Check if the service is running (might be sleeping on free tier)

The API Gateway will now return proper error messages even if user-service is down, which should help with debugging.

