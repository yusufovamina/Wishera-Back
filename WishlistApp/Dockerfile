# Используем .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app  # Устанавливаем рабочую директорию

# Копируем ВСЕ файлы проекта (важно!)
COPY . /app/

# Переходим в папку с проектом (если проект в папке WishlistApp)
WORKDIR /app/WishlistApp

# Восстанавливаем зависимости
RUN dotnet restore

# Собираем проект
RUN dotnet publish -c Release -o /app/publish

# Используем .NET Runtime для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "WishlistApp.dll"]
