# Запуск всех сервисов Wishlist Web App
Write-Host "🚀 Запускаю все сервисы..." -ForegroundColor Green

# Auth Service
Write-Host "Запускаю Auth Service на порту 5219..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\yusufova_a\WishlistWebApp\auth-service'; dotnet run --urls http://localhost:5219"

Start-Sleep -Seconds 2

# User Service
Write-Host "Запускаю User Service на порту 5220..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\yusufova_a\WishlistWebApp\user-service'; dotnet run --urls http://localhost:5220"

Start-Sleep -Seconds 2

# Gift Wishlist Service
Write-Host "Запускаю Gift Wishlist Service на порту 5221..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\yusufova_a\WishlistWebApp\gift-wishlist-service'; dotnet run --urls http://localhost:5221"

Start-Sleep -Seconds 2

# Main Wishlist App
Write-Host "Запускаю Main Wishlist App на порту 5155..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd 'C:\Users\yusufova_a\WishlistWebApp\WishlistApp'; dotnet run --urls http://localhost:5155"

Write-Host ""
Write-Host "✅ Все сервисы запущены!" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 URL адреса:" -ForegroundColor Cyan
Write-Host "Auth Service: http://localhost:5219" -ForegroundColor White
Write-Host "User Service: http://localhost:5220" -ForegroundColor White
Write-Host "Gift Wishlist Service: http://localhost:5221" -ForegroundColor White
Write-Host "Main App: http://localhost:5155" -ForegroundColor White
Write-Host ""
Write-Host "Нажмите любую клавишу для выхода..." -ForegroundColor Yellow
Read-Host
