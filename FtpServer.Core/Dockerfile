# Этап 1: Сборка приложения
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Устанавливаем рабочий каталог
WORKDIR /src

# Копируем все файлы проекта
COPY . .

# Публикуем КОНКРЕТНЫЙ проект FtpServer.Core
RUN dotnet publish FtpServer.Core/FtpServer.Core.csproj -c Release -o /app

# Этап 2: Создание конечного образа
FROM mcr.microsoft.com/dotnet/runtime:8.0

# Создаем пользователя
RUN addgroup --system --gid 1001 ftpuser && \
    adduser --system --uid 1001 --ingroup ftpuser ftpuser

# Устанавливаем рабочий каталог
WORKDIR /app

# Копируем опубликованные файлы и сразу назначаем владельца
COPY --from=build --chown=ftpuser:ftpuser /app .

# Копируем appsettings.json
COPY --from=build --chown=ftpuser:ftpuser /src/FtpServer.Core/appsettings*.json .

# Переключаемся на пользователя
USER ftpuser


# Точка входа
ENTRYPOINT ["dotnet", "FtpServer.Core.dll"]