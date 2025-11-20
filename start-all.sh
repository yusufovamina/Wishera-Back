#!/bin/bash

echo "🚀 Starting Wishlist Web App Services..."
echo ""

# Function to start a service
start_service() {
    local service_name="$1"
    local directory="$2"
    local port="$3"
    
    echo "Starting $service_name on port $port..."
    
    # Start the service in a new terminal window
    osascript -e "tell application \"Terminal\" to do script \"cd '$PWD/$directory' && dotnet run --urls http://localhost:$port\""
    
    # Wait a bit before starting the next service
    sleep 3
}

# Start all services
start_service "Auth Service" "auth-service" "5219"
start_service "User Service" "user-service" "5001"
start_service "Gift Wishlist Service" "gift-wishlist-service" "5003"
start_service "Chat Service" "chat-server-side/PresentationLayer" "5002"
start_service "Main Wishlist App" "WishlistApp" "5155"

echo ""
echo "✅ All services are starting..."
echo ""
echo "🌐 Service URLs:"
echo "- Auth Service: http://localhost:5219"
echo "- User Service: http://localhost:5001"
echo "- Gift Wishlist Service: http://localhost:5003"
echo "- Chat Service: http://localhost:5002"
echo "- Main Wishlist App: http://localhost:5155"
echo ""
echo "Services are running in separate Terminal windows."
echo "Press any key to exit this launcher..."
read -n 1 -s -r
echo ""