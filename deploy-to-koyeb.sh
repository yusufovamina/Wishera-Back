#!/bin/bash

# Build and push Docker images to Docker Hub
# Replace 'your-dockerhub-username' with your Docker Hub username

DOCKER_USERNAME="your-dockerhub-username"

echo "Building WisheraApp..."
cd WishlistApp
docker build -t $DOCKER_USERNAME/wishera-app:latest .
docker push $DOCKER_USERNAME/wishera-app:latest
cd ..

echo "Building AuthService..."
docker build -t $DOCKER_USERNAME/wishera-auth-service:latest -f auth-service/Dockerfile .
docker push $DOCKER_USERNAME/wishera-auth-service:latest

echo "Building GiftWishlistService..."
docker build -t $DOCKER_USERNAME/wishera-gift-service:latest -f gift-wishlist-service/Dockerfile .
docker push $DOCKER_USERNAME/wishera-gift-service:latest

echo "Building UserService..."
docker build -t $DOCKER_USERNAME/wishera-user-service:latest -f user-service/Dockerfile .
docker push $DOCKER_USERNAME/wishera-user-service:latest

echo "Building ChatService..."
docker build -t $DOCKER_USERNAME/wishera-chat-service:latest -f chat-server-side/Dockerfile chat-server-side/
docker push $DOCKER_USERNAME/wishera-chat-service:latest

echo "All images pushed successfully!"

