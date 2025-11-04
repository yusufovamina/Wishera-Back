# Deploy Wishera Backend to Docker Hub (Windows PowerShell)
# This script builds and pushes all Docker images to Docker Hub

# Configuration
$DOCKER_USERNAME = "your-dockerhub-username"  # CHANGE THIS!

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Wishera Backend Docker Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
Write-Host "Checking Docker..." -ForegroundColor Yellow
docker version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker is not running. Please start Docker Desktop." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Docker is running" -ForegroundColor Green
Write-Host ""

# Login to Docker Hub
Write-Host "Logging into Docker Hub..." -ForegroundColor Yellow
docker login
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Docker login failed." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Logged in successfully" -ForegroundColor Green
Write-Host ""

# Function to build and push
function Build-And-Push {
    param (
        [string]$ServiceName,
        [string]$DockerfilePath,
        [string]$Context = "."
    )
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Building $ServiceName..." -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan
    
    $imageName = "$DOCKER_USERNAME/wishera-$ServiceName`:latest"
    
    # Build
    docker build -t $imageName -f $DockerfilePath $Context
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to build $ServiceName" -ForegroundColor Red
        return $false
    }
    Write-Host "✓ Built $ServiceName" -ForegroundColor Green
    
    # Push
    Write-Host "Pushing $imageName..." -ForegroundColor Yellow
    docker push $imageName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to push $ServiceName" -ForegroundColor Red
        return $false
    }
    Write-Host "✓ Pushed $ServiceName" -ForegroundColor Green
    Write-Host ""
    
    return $true
}

# Build and push all services
$services = @(
    @{Name="auth-service"; Dockerfile="auth-service/Dockerfile"; Context="."},
    @{Name="gift-service"; Dockerfile="gift-wishlist-service/Dockerfile"; Context="."},
    @{Name="user-service"; Dockerfile="user-service/Dockerfile"; Context="."},
    @{Name="app"; Dockerfile="WishlistApp/Dockerfile"; Context="WishlistApp"},
    @{Name="chat-service"; Dockerfile="chat-server-side/Dockerfile"; Context="chat-server-side"}
)

$successCount = 0
$failedServices = @()

foreach ($service in $services) {
    $result = Build-And-Push -ServiceName $service.Name -DockerfilePath $service.Dockerfile -Context $service.Context
    if ($result) {
        $successCount++
    } else {
        $failedServices += $service.Name
    }
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Successfully deployed: $successCount/$($services.Count) services" -ForegroundColor Green

if ($failedServices.Count -gt 0) {
    Write-Host "Failed services:" -ForegroundColor Red
    foreach ($failed in $failedServices) {
        Write-Host "  - $failed" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host ""
    Write-Host "🎉 All images pushed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Go to Koyeb/Render/Railway dashboard" -ForegroundColor White
    Write-Host "2. Create services using these images:" -ForegroundColor White
    foreach ($service in $services) {
        Write-Host "   - $DOCKER_USERNAME/wishera-$($service.Name):latest" -ForegroundColor Cyan
    }
    Write-Host ""
}

