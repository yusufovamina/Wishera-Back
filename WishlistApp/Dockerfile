# Используем .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /WishlistApp  # Теперь работаем прямо в WishlistApp

# Копируем файл проекта и восстанавливаем зависимости
COPY WishlistApp.csproj .
RUN dotnet restore

# Копируем все файлы и собираем проект
COPY . .
RUN dotnet publish -c Release -o /WishlistApp/publish

# Используем .NET Runtime для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /WishlistApp
COPY --from=build /WishlistApp/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "WishlistApp.dll"]
