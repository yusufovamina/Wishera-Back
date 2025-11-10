FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5219

ENV ASPNETCORE_URLS=http://+:5219

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./auth-service/auth-service.csproj /src/auth-service.csproj
RUN dotnet restore /src/auth-service.csproj
COPY ./auth-service /src
RUN dotnet publish /src/auth-service.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "auth-service.dll"]


