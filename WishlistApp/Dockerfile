FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5155

ENV ASPNETCORE_URLS=http://+:5155

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore WishlistApp.csproj
RUN dotnet publish WishlistApp.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "WishlistApp.dll"]

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
