# Используем официальный образ .NET для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

# Используем .NET SDK для сборки приложения
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WishlistApp.csproj", "./"]
RUN dotnet restore "WishlistApp.csproj"
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Финальный образ для запуска
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
CMD ["dotnet", "WishlistApp.dll"]
