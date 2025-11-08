# Fix: WishlistApp Docker Build Context Error

## Problem
```
error: failed to solve: failed to compute cache key: failed to calculate checksum of ref ... "/WishlistApp": not found
```

This error occurs because the Docker build context is set to `./WishlistApp` instead of the repository root (`./`).

## Solution

### Option 1: Update Render.com Dashboard (If manually configured)

1. Go to your Render.com dashboard
2. Select the `wishera-app` service
3. Go to **Settings**
4. Scroll down to **Docker** section
5. Find **Docker Context** field
6. Change it from `WishlistApp` to `.` (dot, which means repository root)
7. Click **Save Changes**
8. Trigger a new deployment

### Option 2: Use the Updated render.yaml (If using Blueprint)

The `render.yaml` file has been updated to use `dockerContext: ./` instead of `./WishlistApp`.

If you're using the Render.com Blueprint:
1. The changes are already in the file
2. Push the changes to GitHub
3. Render.com will automatically detect and apply the changes

## Why This Fix Works

The WishlistApp Dockerfile needs access to both:
- `WishlistApp/` directory (the main project)
- `user-service/` directory (for project references and Event DTOs)

When the build context is set to `./WishlistApp`, Docker cannot access files outside that directory (like `user-service/`). Setting the context to `./` (repository root) allows the Dockerfile to copy both directories.

## Verification

After updating, the build should succeed and you should see:
- ✅ Docker build completes successfully
- ✅ All Event DTOs are found
- ✅ Project references resolve correctly

## Current Dockerfile Structure

The Dockerfile expects the build context to be the repository root:
```dockerfile
# Copy project files first
COPY ./user-service/user-service.csproj /src/user-service/user-service.csproj
COPY ./WishlistApp/WishlistApp.csproj /src/WishlistApp/WishlistApp.csproj

# Restore dependencies
RUN dotnet restore /src/WishlistApp/WishlistApp.csproj

# Copy all source files
COPY ./user-service /src/user-service
COPY ./WishlistApp /src/WishlistApp

# Build and publish
RUN dotnet publish /src/WishlistApp/WishlistApp.csproj -c Release -o /out
```

This structure requires the build context to be the repository root.

