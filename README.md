# Distributed FTP Server - Production Ready

Этот репозиторий содержит реализацию **масштабируемого распределенного FTP-сервера на .NET C#** с полной балансировкой нагрузки, управлением сессиями и централизованной аутентификацией.

## 🎯 Текущий статус: Distributed Architecture ✅

**Что работает:**
- ✅ Nginx Load Balancer с проксированием FTP трафика
- ✅ Несколько FTP серверов (горизонтальное масштабирование)
- ✅ Redis для управления сессиями между серверами
- ✅ Централизованная служба аутентификации
- ✅ Shared storage для файлов между серверами
- ✅ Docker networking с изоляцией сервисов
- ✅ Production-ready конфигурация

## 🏗️ Архитектура системы

```
┌─────────────────────────────────────────────────────────────┐
│                    ВНЕШНИЙ МИР                              │
│                  (FTP Clients)                              │
└─────────────────────┬───────────────────────────────────────┘
                      │ Port 21 + 50000-50010
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                 NGINX LOAD BALANCER                         │
│              (nginx-ftp-proxy)                              │
│         • Stream proxy для FTP                             │
│         • Round-robin балансировка                         │
└─────────────────────┬───────────────────────────────────────┘
                      │ ftp-network
    ┌─────────────────┼─────────────────┐
    │                 │                 │
    ▼                 ▼                 ▼
┌─────────┐     ┌─────────┐     ┌─────────┐
│FTP-SRV-1│     │FTP-SRV-2│     │FTP-SRV-3│
│Port 21  │     │Port 21  │     │Port 21  │
└─────────┘     └─────────┘     └─────────┘
    │                 │                 │
    └─────────────────┼─────────────────┘
                      │
                      ▼
    ┌─────────────────────────────────┐
    │         SHARED SERVICES         │
    │  ┌─────────┐   ┌─────────────┐  │
    │  │  REDIS  │   │ AUTH SERVICE│  │
    │  │Sessions │   │   API 5160  │  │
    │  │  6379   │   │             │  │
    │  └─────────┘   └─────────────┘  │
    └─────────────────────────────────┘
                      │
                      ▼
           ┌─────────────────────┐
           │   SHARED STORAGE    │
           │   (Bind Mount)      │
           │   /app/shared_storage│
           └─────────────────────┘
```

## 🧩 Компоненты системы

### Производственные сервисы:
* **nginx-ftp-proxy:** Load balancer с stream proxy для FTP
* **ftp-server-1,2,3:** Множественные экземпляры FTP серверов
* **redis:** Управление сессиями и состоянием
* **auth-service:** Централизованная аутентификация
* **shared-storage:** Общее файловое хранилище

### Сетевая архитектура:
* **ftp-network:** Изолированная Docker сеть для межсервисного общения
* **Port mapping:** 21 (FTP control), 50000-50010 (FTP data)
* **Service discovery:** Контейнеры общаются по именам

## 📂 Структура проекта

```
DistributedFtpServer/
├── FtpServer.Core/           # Основной FTP сервер
│   ├── BasicFtpServer.cs     # Core FTP логика
│   ├── Program.cs            # Entry point
│   ├── Dockerfile            # Docker образ
│   └── appsettings.json      # Конфигурация
├── FtpServer.Auth/           # Authentication API
│   ├── Controllers/          # API контроллеры
│   ├── Dockerfile            # Docker образ
│   └── appsettings.json      # Конфигурация
├── nginx/                    # Load Balancer
│   ├── Dockerfile            # Nginx образ
│   └── nginx.conf            # Конфигурация проксирования
├── redis/                    # Session Storage
│   └── Dockerfile            # Redis образ
├── FtpServer.Commons/        # Shared библиотеки
└── app/
    └── shared_storage/       # Общее файловое хранилище
        ├── demo/             # Пользователи
        ├── admin/            
        └── test/             
```

## 🚀 Развертывание системы

### Предварительные требования
- Docker и Docker Compose установлены
- Порты 21 и 50000-50010 свободны
- Минимум 2GB RAM для всех сервисов

### 1. Создание Docker сети

```bash
# Создаем изолированную сеть для всех сервисов
docker network create ftp-network

# Проверяем создание
docker network ls | grep ftp-network
```

### 2. Подготовка файлового хранилища

```bash
# Создаем общую папку для всех FTP серверов
mkdir -p /path/to/shared/ftp/storage

# Устанавливаем права для Docker пользователя
sudo chown -R 1001:1001 /path/to/shared/ftp/storage
```

### 3. Сборка всех образов

```bash
# Redis (сессии)
docker build -f redis/Dockerfile -t ftp-redis:production .

# Auth Service (аутентификация)  
docker build -f FtpServer.Auth/Dockerfile -t auth-service:production .

# FTP Server (основные серверы)
docker build -f FtpServer.Core/Dockerfile -t ftp-server:production .

# Nginx (load balancer)
docker build -f nginx/Dockerfile -t nginx-ftp:production .
```

### 4. Запуск сервисов (правильная последовательность)

#### Шаг 1: Redis (сессии)
```bash
docker run -d \
  --name redis \
  --network ftp-network \
  -p 6379:6379 \
  ftp-redis:production
```

#### Шаг 2: Auth Service (аутентификация)
```bash
docker run -d \
  --name auth-service \
  --network ftp-network \
  -p 5160:5160 \
  -e RedisConnectionString=redis:6379 \
  auth-service:production
```

#### Шаг 3: FTP Servers (множественные экземпляры)
```bash
# Узнайте ваш внешний IP
EXTERNAL_IP=$(curl -s ifconfig.me)
echo "External IP: $EXTERNAL_IP"

# FTP Server 1
docker run -d \
  --name ftp-server-1 \
  --network ftp-network \
  -e FtpExternalIP=$EXTERNAL_IP \
  -e FtpBehindProxy=true \
  -e port=21 \
  -e FtpPasvMinPort=50000 \
  -e FtpPasvMaxPort=50010 \
  -e RedisConnectionString=redis:6379 \
  -e authservice=http://auth-service:5160 \
  -v /path/to/shared/ftp/storage:/app/shared_storage \
  ftp-server:production

# FTP Server 2
docker run -d \
  --name ftp-server-2 \
  --network ftp-network \
  -e FtpExternalIP=$EXTERNAL_IP \
  -e FtpBehindProxy=true \
  -e port=21 \
  -e FtpPasvMinPort=50000 \
  -e FtpPasvMaxPort=50010 \
  -e RedisConnectionString=redis:6379 \
  -e authservice=http://auth-service:5160 \
  -v /path/to/shared/ftp/storage:/app/shared_storage \
  ftp-server:production

# FTP Server 3
docker run -d \
  --name ftp-server-3 \
  --network ftp-network \
  -e FtpExternalIP=$EXTERNAL_IP \
  -e FtpBehindProxy=true \
  -e port=21 \
  -e FtpPasvMinPort=50000 \
  -e FtpPasvMaxPort=50010 \
  -e RedisConnectionString=redis:6379 \
  -e authservice=http://auth-service:5160 \
  -v /path/to/shared/ftp/storage:/app/shared_storage \
  ftp-server:production
```

#### Шаг 4: Nginx Load Balancer (финал)
```bash
docker run -d \
  --name nginx-ftp-proxy \
  --network ftp-network \
  -p 21:21 \
  -p 50000-50010:50000-50010 \
  -v ./nginx/nginx.conf:/etc/nginx/conf.d/nginx.conf \
  nginx-ftp:production
>>>>>>> develop
```

### 5. Проверка развертывания

```bash
# Проверить все контейнеры
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Проверить сеть
docker network inspect ftp-network

# Проверить логи каждого сервиса
docker logs redis
docker logs auth-service  
docker logs ftp-server-1
docker logs nginx-ftp-proxy
```

## 🧪 Тестирование системы

### Тест 1: Подключение через Load Balancer

```bash
# Подключение к системе (nginx проксирует к одному из FTP серверов)
telnet localhost 21

# Ожидаемый результат:
# 220 Welcome to Basic FTP Server v1.0.
```

### Тест 2: Полный workflow аутентификации

```bash
# В telnet сессии:
USER demo
# 331 Password required

PASS anypassword  
# 230 Login successful

PWD
# 257 "/" is current directory

LIST
# 150 Opening data connection for directory list
# 226 Directory listing completed
```

### Тест 3: FileZilla интеграция

```
Host: localhost (или ваш внешний IP)
Port: 21
Protocol: FTP
User: demo, admin, или test
Password: любой
Transfer mode: Passive
```

### Тест 4: Балансировка нагрузки

```bash
# Остановите один FTP сервер
docker stop ftp-server-1

# Система должна продолжать работать через оставшиеся серверы
telnet localhost 21

# Запустите сервер обратно
docker start ftp-server-1
```

### Тест 5: Shared Storage

```bash
# Загрузите файл через FileZilla к одному серверу
# Подключитесь заново (nginx может направить к другому серверу)
# Файл должен быть виден - это доказывает работу shared storage
```

## ⚙️ Конфигурация системы

### Переменные окружения

| Сервис | Переменная | Описание | Значение |
|--------|------------|----------|----------|
| FTP Server | `FtpExternalIP` | Внешний IP для PASV | Ваш public IP |
| FTP Server | `FtpBehindProxy` | Режим за прокси | true |
| FTP Server | `RedisConnectionString` | Подключение к Redis | redis:6379 |
| FTP Server | `authservice` | URL Auth Service | http://auth-service:5160 |
| Auth Service | `RedisConnectionString` | Подключение к Redis | redis:6379 |

### Nginx конфигурация (nginx.conf)

```nginx
stream {
    upstream ftp_backend {
        server ftp-server-1:21;
        server ftp-server-2:21;
        server ftp-server-3:21;
    }
    
    server {
        listen 21;
        proxy_pass ftp_backend;
        proxy_timeout 1s;
        proxy_responses 1;
    }
}

events {
    worker_connections 1024;
}
```

## 📊 Мониторинг и логирование

### Проверка статуса всех сервисов

```bash
# Одной командой проверить все
for service in redis auth-service ftp-server-1 ftp-server-2 ftp-server-3 nginx-ftp-proxy; do
  echo "=== $service ==="
  docker logs --tail 5 $service
done
```

### Мониторинг производительности

```bash
# Статистика контейнеров
docker stats redis auth-service ftp-server-1 ftp-server-2 ftp-server-3 nginx-ftp-proxy

# Сетевая активность
docker exec nginx-ftp-proxy netstat -tlnp

# Активные FTP сессии
docker exec ftp-server-1 netstat -an | grep :21
```

### Типичные логи

**Успешная балансировка:**
```
# nginx-ftp-proxy
[nginx] client connected to upstream ftp-server-2:21

# ftp-server-2  
📞 New client connected: 172.18.0.5:45234
✅ Authentication successful for demo
```

## 🔧 Масштабирование

### Добавление нового FTP сервера

```bash
# Запуск FTP Server 4
docker run -d \
  --name ftp-server-4 \
  --network ftp-network \
  -e FtpExternalIP=$EXTERNAL_IP \
  -e FtpBehindProxy=true \
  -e port=21 \
  -e FtpPasvMinPort=50000 \
  -e FtpPasvMaxPort=50010 \
  -e RedisConnectionString=redis:6379 \
  -e authservice=http://auth-service:5160 \
  -v /path/to/shared/ftp/storage:/app/shared_storage \
  ftp-server:production

# Обновить nginx конфигурацию
# Добавить server ftp-server-4:21; в upstream
# Перезагрузить nginx
docker exec nginx-ftp-proxy nginx -s reload
```

### Вертикальное масштабирование

```bash
# Увеличить ресурсы контейнеров
docker update --memory 2g --cpus 2 ftp-server-1
docker update --memory 1g --cpus 1 redis
```

## 🐛 Устранение неполадок

### Проблема: Клиенты не могут подключиться

```bash
# Проверить nginx
docker logs nginx-ftp-proxy

# Проверить доступность upstream серверов
docker exec nginx-ftp-proxy ping ftp-server-1
docker exec nginx-ftp-proxy ping ftp-server-2

# Проверить порты
netstat -tlnp | grep 21
```

### Проблема: Нет балансировки

```bash
# Проверить nginx конфигурацию
docker exec nginx-ftp-proxy cat /etc/nginx/conf.d/nginx.conf

# Проверить upstream серверы
docker exec nginx-ftp-proxy nslookup ftp-server-1
```

### Проблема: Сессии не сохраняются

```bash
# Проверить Redis
docker logs redis
docker exec redis redis-cli ping

# Проверить подключение FTP серверов к Redis
docker exec ftp-server-1 ping redis
```

## 🔒 Безопасность

### Рекомендации для продакшена

1. **Изолированная сеть:** Используйте custom bridge network
2. **Firewall правила:** Ограничьте доступ к портам управления
3. **TLS шифрование:** Добавьте FTPS поддержку
4. **Мониторинг:** Логирование всех подключений
5. **Backup:** Регулярное резервирование Redis и файлов

### Пример firewall настройки

```bash
# Разрешить только FTP порты
ufw allow 21
ufw allow 50000:50010/tcp

# Заблокировать прямой доступ к внутренним сервисам
ufw deny 6379  # Redis
ufw deny 5160  # Auth Service
```

## 🗺️ Roadmap

### ✅ Фаза 1: MVP Single Server 
- Основная FTP функциональность
- Docker контейнеризация

### ✅ Фаза 2: Distributed Architecture (текущая)
- nginx load balancer
- Redis session management  
- Множественные FTP серверы
- Shared storage

### 🚀 Фаза 3: Enterprise Ready
- FTPS поддержка (TLS/SSL)
- Prometheus мониторинг
- Auto-scaling с Kubernetes
- Distributed file system (Ceph/GlusterFS)

## 🤝 Участие в разработке

1. Fork репозиторий
2. Создайте feature branch
3. Протестируйте в distributed окружении
4. Создайте Pull Request

## 📄 Лицензия

MIT License - см. файл LICENSE для деталей.

---

**Статус проекта:** Production Ready ✅ | **Архитектура:** Fully Distributed 🏗️ | **Масштабирование:** Horizontal + Vertical 📈