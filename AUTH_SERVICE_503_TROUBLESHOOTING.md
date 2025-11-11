# Auth Service 503 Error Troubleshooting

## Issue: GET https://wishera-auth-service.onrender.com/api/externalauth/login/Google 503 (Service Unavailable)

A 503 Service Unavailable error typically means the service is not running or crashed during startup.

## Recent Fixes Applied

1. **Registered GlobalExceptionMiddleware** - Added exception handling to prevent unhandled exceptions from crashing the service
2. **Fixed middleware ordering** - Properly ordered middleware pipeline (Routing → CORS → Exception Handling → Auth)
3. **Added startup logging** - Enhanced logging to diagnose startup issues
4. **Added error handling** - Wrapped app.Run() in try-catch to log fatal errors

## Troubleshooting Steps

### 1. Check Service Status on Render.com

1. Go to your Render.com dashboard
2. Navigate to the `wishera-auth-service` service
3. Check the **Logs** tab for any error messages
4. Check the **Events** tab for deployment status
5. Verify the service shows as "Live" (not "Failed" or "Building")

### 2. Check Health Endpoint

Try accessing the health endpoint:
```
GET https://wishera-auth-service.onrender.com/health
```

Expected response:
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

If this also returns 503, the service is not running.

### 3. Common Causes of 503 Errors

#### A. Service Crash on Startup

**Check Render logs for:**
- Missing configuration (JWT key, MongoDB connection string)
- Port binding issues
- Missing dependencies
- Compilation errors

**Common startup errors:**
```
JWT key is not configured
MongoDB connection failed
Port already in use
```

#### B. Health Check Failing

Render.com uses health checks to determine if a service is ready. If the health check fails, Render returns 503.

**Check:**
- Health check path is correct: `/health`
- Health endpoint returns 200 OK
- Service starts within the health check timeout (usually 60 seconds)

#### C. Environment Variables Missing

**Required environment variables:**
- `Jwt__Key` - JWT signing key (required)
- `ConnectionStrings__MongoDB` - MongoDB connection string
- `Authentication__Google__ClientId` - Google OAuth client ID
- `Authentication__Google__ClientSecret` - Google OAuth client secret
- `Authentication__Google__RedirectUri` - OAuth redirect URI (optional, will be constructed if missing)

#### D. Service Still Starting

Free tier services on Render.com may take 30-60 seconds to start. Wait a few minutes and try again.

### 4. Verify Route

The correct route for Google OAuth login is:
```
GET https://wishera-auth-service.onrender.com/api/externalauth/login/Google
```

Alternative routes that should also work:
```
GET https://wishera-auth-service.onrender.com/api/ExternalAuth/login/Google
GET https://wishera-auth-service.onrender.com/api/externalauth/login/google
```

### 5. Check Render.com Service Configuration

1. **Service Type**: Should be "Web Service"
2. **Dockerfile Path**: `./auth-service/Dockerfile`
3. **Docker Context**: `./`
4. **Health Check Path**: `/health`
5. **Port**: Should be auto-detected or set to `5219`

### 6. Review Recent Changes

Recent changes that might affect startup:
- Added `OAuthStateManager` class - ensure it compiles correctly
- Added exception middleware registration
- Added forwarded headers configuration

**Verify compilation:**
```bash
cd auth-service
dotnet build
```

### 7. Check Dependencies

Ensure all required packages are installed:
- MongoDB.Driver
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt
- RabbitMQ.Client (optional, for RPC)

### 8. Test Locally

Before deploying, test locally:
```bash
cd auth-service
dotnet run
```

Then test the endpoint:
```bash
curl http://localhost:5219/health
curl http://localhost:5219/api/externalauth/login/Google
```

## Quick Fixes

### Fix 1: Restart Service on Render.com

1. Go to Render.com dashboard
2. Navigate to `wishera-auth-service`
3. Click "Manual Deploy" → "Clear build cache & deploy"

### Fix 2: Check Environment Variables

1. Go to Render.com dashboard
2. Navigate to `wishera-auth-service` → Environment
3. Verify all required environment variables are set:
   - `Jwt__Key`
   - `ConnectionStrings__MongoDB`
   - `Authentication__Google__ClientId`
   - `Authentication__Google__ClientSecret`

### Fix 3: Verify Dockerfile

Ensure the Dockerfile is correct:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5219

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./auth-service/auth-service.csproj /src/auth-service.csproj
RUN dotnet restore /src/auth-service.csproj
COPY ./auth-service /src
RUN dotnet publish /src/auth-service.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "auth-service.dll"]
```

## Next Steps

1. Check Render.com logs for specific error messages
2. Verify all environment variables are set
3. Test the health endpoint
4. Review service events for deployment issues
5. If issues persist, check Render.com status page for platform issues

## Contact Support

If the service continues to return 503 after checking all of the above:
1. Check Render.com status page
2. Review Render.com service logs for detailed error messages
3. Verify the service is not hitting resource limits (free tier has limits)

