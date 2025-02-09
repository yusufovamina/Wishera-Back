# Используем официальный .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app  # Меняем рабочую директорию

# Копируем файл проекта и восстанавливаем зависимости
COPY ./WishlistApp.csproj .
RUN dotnet restore WishlistApp.csproj

# Копируем все файлы и собираем приложение
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Используем .NET Runtime для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "WishlistApp.dll"]
