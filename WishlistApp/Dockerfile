# Используем официальное изображение .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app

# Копируем проект и устанавливаем зависимости
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Используем ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/out .

# Указываем порт для работы (Render автоматически подставит его)
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Запуск приложения
CMD ["dotnet", "Wishlist.dll"]
