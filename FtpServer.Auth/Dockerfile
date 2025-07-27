# Простой Dockerfile для Auth Service (ASP.NET Core Web API)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY . .

# Собираем Auth Service
WORKDIR /src/FtpServer.Auth
RUN dotnet publish -c Release -o /app

# Runtime для ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Key fixed - asp.net listen port 5160 
ENV ASPNETCORE_URLS=http://+:5160
ENV ASPNETCORE_HTTP_PORTS=5160

EXPOSE 5160
ENTRYPOINT ["dotnet", "FtpServer.Auth.dll"]
