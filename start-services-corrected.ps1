# Corrected Wishlist Web App Services Launcher
# This script starts services on the ports expected by the frontend
Write-Host "Starting Wishlist Web App Services with correct ports..." -ForegroundColor Green
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
    $envCmd = ""
    if ($ServiceName -eq "Chat Service") {
        # Ensure chat service uses MongoDB from env if configured
        if ($env:MONGO_URL) { $envCmd = "$env:MONGO_URL=$($env:MONGO_URL); " }
        elseif ($env:MONGODB_URI) { $envCmd = "$env:MONGODB_URI=$($env:MONGODB_URI); " }
    }
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$Directory'; $envCmd dotnet run --urls http://localhost:$Port"
    
    # Wait a bit before starting the next service
    Start-Sleep -Seconds 3
}

# Start all services with correct ports matching frontend expectations
Start-Service -ServiceName "Auth Service" -Directory "auth-service" -Port "5219"
Start-Service -ServiceName "User Service" -Directory "user-service" -Port "5001"
Start-Service -ServiceName "Gift Wishlist Service" -Directory "gift-wishlist-service" -Port "5003"
Start-Service -ServiceName "Chat Service" -Directory "chat-server-side/PresentationLayer" -Port "5002"
Start-Service -ServiceName "Main Wishlist App" -Directory "WishlistApp" -Port "5155"

Write-Host ""
Write-Host "All services are starting..." -ForegroundColor Green
Write-Host ""
Write-Host "Service URLs (matching frontend expectations):" -ForegroundColor Cyan
Write-Host "- Auth Service: http://localhost:5219" -ForegroundColor White
Write-Host "- User Service: http://localhost:5001" -ForegroundColor White
Write-Host "- Gift Wishlist Service: http://localhost:5003" -ForegroundColor White
Write-Host "- Chat Service: http://localhost:5002" -ForegroundColor White
Write-Host "- Main Wishlist App: http://localhost:5155" -ForegroundColor White
Write-Host ""
Write-Host "Services are running in separate windows." -ForegroundColor Green
Write-Host "Press any key to exit this launcher..." -ForegroundColor Yellow
Read-Host
