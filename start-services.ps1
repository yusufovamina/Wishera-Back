# Wishlist Web App Services Launcher
Write-Host "Starting Wishlist Web App Services..." -ForegroundColor Green
Write-Host ""

# Function to start a service
function Start-Service {
    param(
        [string]$ServiceName,
        [string]$Directory,
        [string]$Port
    )
    
    Write-Host "Starting $ServiceName on port $Port..." -ForegroundColor Yellow
    
    # Start the service in a new window
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$Directory'; dotnet run --urls http://localhost:$Port"
    
    # Wait a bit before starting the next service
    Start-Sleep -Seconds 3
}

# Start all services
Start-Service -ServiceName "Auth Service" -Directory "auth-service" -Port "5219"
Start-Service -ServiceName "User Service" -Directory "user-service" -Port "5220"
Start-Service -ServiceName "Gift Wishlist Service" -Directory "gift-wishlist-service" -Port "5221"
Start-Service -ServiceName "Chat Service" -Directory "chat-service-dotnet" -Port "5000"
Start-Service -ServiceName "Main Wishlist App" -Directory "WishlistApp" -Port "5155"

Write-Host ""
Write-Host "All services are starting..." -ForegroundColor Green
Write-Host ""
Write-Host "Service URLs:" -ForegroundColor Cyan
Write-Host "- Auth Service: http://localhost:5219" -ForegroundColor White
Write-Host "- User Service: http://localhost:5220" -ForegroundColor White
Write-Host "- Gift Wishlist Service: http://localhost:5221" -ForegroundColor White
Write-Host "- Chat Service: http://localhost:5000" -ForegroundColor White
Write-Host "- Main Wishlist App: http://localhost:5155" -ForegroundColor White
Write-Host ""
Write-Host "Services are running in separate windows." -ForegroundColor Green
Write-Host "Press any key to exit this launcher..." -ForegroundColor Yellow
Read-Host
