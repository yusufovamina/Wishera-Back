@echo off
echo Starting Wishlist Web App Services with correct ports...
echo.

echo Starting Auth Service on port 5219...
start "Auth Service" cmd /k "cd auth-service && dotnet run --urls http://localhost:5219"

echo Starting User Service on port 5001...
start "User Service" cmd /k "cd user-service && dotnet run --urls http://localhost:5001"

echo Starting Gift Wishlist Service on port 5003...
start "Gift Wishlist Service" cmd /k "cd gift-wishlist-service && dotnet run --urls http://localhost:5003"

echo Starting Chat Service (API & SignalR) on port 5002...
start "Chat Service" cmd /k "cd chat-server-side\PresentationLayer && dotnet run --urls http://localhost:5002"

echo Starting Main Wishlist App on port 5155...
start "Main Wishlist App" cmd /k "cd WishlistApp && dotnet run --urls http://localhost:5155"

echo.
echo All services are starting...
echo.
echo Service URLs (matching frontend expectations):
echo - Auth Service: http://localhost:5219
echo - User Service: http://localhost:5001
echo - Gift Wishlist Service: http://localhost:5003
echo - Chat Service (API & SignalR): http://localhost:5002
echo - Main Wishlist App: http://localhost:5155
echo.
echo Press any key to exit...
pause >nul
