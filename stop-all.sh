#!/bin/bash

echo "🛑 Stopping all Wishlist Web App Services..."
echo ""

# Function to stop a service by port
stop_service() {
    local service_name="$1"
    local port="$2"
    
    echo "Stopping $service_name on port $port..."
    
    # Find and kill processes using the port
    local pids=$(lsof -ti:$port)
    if [ ! -z "$pids" ]; then
        echo "$pids" | xargs kill -9
        echo "✅ $service_name stopped"
    else
        echo "ℹ️  $service_name was not running"
    fi
}

# Stop all services
stop_service "Auth Service" "5219"
stop_service "User Service" "5001"
stop_service "Gift Wishlist Service" "5003"
stop_service "Chat Service" "5002"
stop_service "Main Wishlist App" "5155"

echo ""
echo "✅ All services have been stopped"
echo ""
