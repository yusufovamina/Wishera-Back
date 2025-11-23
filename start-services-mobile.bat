@echo off
echo Starting Wishlist Web App Services for Mobile Development...
echo.
echo NOTE: Services will bind to 0.0.0.0 to allow access from physical devices
echo Make sure your phone and computer are on the same WiFi network!
echo.

echo Starting Auth Service on port 5219...
start "Auth Service" cmd /k "cd auth-service && dotnet run --urls http://0.0.0.0:5219"

echo Starting User Service on port 5001...
start "User Service" cmd /k "cd user-service && dotnet run --urls http://0.0.0.0:5001"

echo Starting Gift Wishlist Service on port 5003...
start "Gift Wishlist Service" cmd /k "cd gift-wishlist-service && dotnet run --urls http://0.0.0.0:5003"

echo Starting Chat Service (API & SignalR) on port 5002...
start "Chat Service" cmd /k "cd chat-server-side\PresentationLayer && dotnet run --urls http://0.0.0.0:5002"

echo Starting Main Wishlist App on port 5155...
start "Main Wishlist App" cmd /k "cd WishlistApp && dotnet run --urls http://0.0.0.0:5155"

echo.
echo All services are starting...
echo.
echo Service URLs (accessible from network):
echo - Auth Service: http://0.0.0.0:5219 (or http://YOUR_IP:5219 from mobile)
echo - User Service: http://0.0.0.0:5001 (or http://YOUR_IP:5001 from mobile)
echo - Gift Wishlist Service: http://0.0.0.0:5003 (or http://YOUR_IP:5003 from mobile)
echo - Chat Service (API & SignalR): http://0.0.0.0:5002 (or http://YOUR_IP:5002 from mobile)
echo - Main Wishlist App: http://0.0.0.0:5155 (or http://YOUR_IP:5155 from mobile)
echo.
echo To find your IP address, run: ipconfig
echo Look for "IPv4 Address" under your active network adapter
echo.
echo Press any key to exit...
pause >nul

