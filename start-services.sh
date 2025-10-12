#!/bin/bash

echo "Starting Wishlist Web App Services..."
echo ""

echo "Starting Auth Service on port 5219..."
(cd auth-service && dotnet run --urls "http://localhost:5219") &

echo "Starting User Service on port 5220..."
(cd user-service && dotnet run --urls "http://localhost:5220") &

echo "Starting Gift Wishlist Service on port 5221..."
(cd gift-wishlist-service && dotnet run --urls "http://localhost:5221") &

echo "Starting Chat Service (PresentationLayer) on port 5162..."
(cd chat-server-side/PresentationLayer && dotnet run --urls "http://localhost:5162") &

echo "Starting Main Wishlist App on port 5155..."
(cd WishlistApp && dotnet run --urls "http://localhost:5155") &

echo ""
echo "All services are starting..."
echo ""
echo "Service URLs:"
echo "- Auth Service: http://localhost:5219"
echo "- User Service: http://localhost:5220"
echo "- Gift Wishlist Service: http://localhost:5221"
echo "- Chat Service: http://localhost:5162"
echo "- Main Wishlist App: http://localhost:5155"
echo ""

read -n 1 -s -r -p "Press any key to exit..."
echo ""
