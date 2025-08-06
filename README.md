# 🚀 Учебный проект: Distributed FTP Server - Enterprise Production Ready

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Docker](https://img.shields.io/badge/Docker-20.10+-blue.svg)](https://www.docker.com/)
[![HAProxy](https://img.shields.io/badge/HAProxy-2.8+-red.svg)](https://www.haproxy.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Этот репозиторий содержит реализацию **enterprise-grade масштабируемого распределенного FTP-сервера на .NET C#** с интеллектуальной балансировкой нагрузки, управлением сессиями и централизованной аутентификацией.

## 🎯 Текущий статус: Production Ready ✅

**Что работает:**
- ✅ **HAProxy Load Balancer** с нативной поддержкой FTP и sticky sessions
- ✅ **Кластер из 3 FTP серверов** с горизонтальным масштабированием
- ✅ **Redis для управления сессиями** между серверами
- ✅ **Централизованная служба аутентификации** (ASP.NET Core API)
- ✅ **Shared storage** для файлов между всеми серверами
- ✅ **Health monitoring** с автоматическим исключением неработающих серверов
- ✅ **Sticky sessions по IP** клиента для консистентности соединений
- ✅ **Docker networking** с полной изоляцией сервисов
- ✅ **Web-интерфейс мониторинга** HAProxy
- ✅ **Автоматизированное развертывание** одной командой

## 🏗️ Архитектура системы

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ВНЕШНИЙ МИР                                  │
│                   (FTP Clients)                                     │
│               FileZilla, WinSCP, etc.                               │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ Port 21 (Control) + 50000-50010 (Data)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    HAPROXY LOAD BALANCER                            │
│                      (haproxy-ftp)                                  │
│  • TCP mode для нативной поддержки FTP                              │
│  • Sticky Sessions по IP клиента                                    │
│  • Health Checks для автоматического failover                       │
│  • Web Stats Interface (8404/stats)                                 │
│  • Round-robin балансировка                                         │
└─────────────────────────┬───────────────────────────────────────────┘
                          │ ftp-network (internal)
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│ FTP-SERVER-1│   │ FTP-SERVER-2│   │ FTP-SERVER-3│
│  (.NET 8)   │   │  (.NET 8)   │   │  (.NET 8)   │
│  Port: 21   │   │  Port: 21   │   │  Port: 21   │
│  Data: 50000+│   │  Data: 50000+│   │  Data: 50000+│
│  ✅ Health   │   │  ✅ Health   │   │  ✅ Health   │
└─────────────┘   └─────────────┘   └─────────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          │
                          ▼
        ┌─────────────────────────────────────────────┐
        │              SHARED SERVICES                │
        │  ┌─────────────┐      ┌─────────────────┐   │
        │  │    REDIS    │      │  AUTH SERVICE   │   │
        │  │  Sessions   │◄────►│   (ASP.NET)     │   │
        │  │  Port: 6379 │      │   Port: 5160    │   │
        │  │ ✅ Cluster │       │   ✅ Health    │   │
        │  └─────────────┘      └─────────────────┘   │
        └─────────────────────────────────────────────┘
                          │
                          ▼
                ┌─────────────────────┐
                │   SHARED STORAGE    │
                │   (Docker Volume)   │
                │ /app/shared_storage │
                │  📁 demo/          │
                │  📁 admin/         │
                │  📁 test/          │
                └─────────────────────┘
```

## 🧩 Компоненты системы

### Production Services:

| Сервис | Технология | Назначение | Порты | Статус |
|--------|------------|------------|-------|--------|
| **haproxy-ftp** | HAProxy 2.8 | Load balancer + TCP proxy | 21, 8404, 50000-50010 | ✅ |
| **ftp-server-1,2,3** | .NET 8 C# | FTP protocol servers | 21, 50000-50010 | ✅ |
| **auth-service** | ASP.NET Core | Централизованная аутентификация | 5160 | ✅ |
| **redis** | Redis 7 | Session storage + caching | 6379 | ✅ |

### Ключевые особенности архитектуры:
- 🔄 **Sticky Sessions**: Клиент всегда подключается к тому же FTP серверу
- 🏥 **Health Monitoring**: Автоматическое исключение неработающих серверов
- 📊 **Real-time Stats**: Web-интерфейс мониторинга на порту 8404
- 🌐 **Service Discovery**: Контейнеры общаются по внутренним именам
- 🔒 **Network Isolation**: Изолированная Docker сеть ftp-network

## 📂 Структура проекта

```
DistributedFtpServer/
├── 📁 Distributed-FTP-Server/
│   ├── 📁 FtpServer.Core/              # 🎯 Core FTP Server
│   │   ├── BasicFtpServer.cs           # Основная FTP логика
│   │   ├── Program.cs                  # Entry point
│   │   ├── Services/                   # Auth, Session, Redis clients
│   │   ├── Dockerfile                  # Docker образ
│   │   └── appsettings.json            # Конфигурация
│   ├── 📁 FtpServer.Auth/              # 🔐 Authentication API
│   │   ├── Controllers/AuthController.cs
│   │   ├── Models/                     # User, AuthResult models
│   │   ├── Dockerfile                  # Docker образ
│   │   └── appsettings.json            # Конфигурация
│   ├── 📁 FtpServer.Contracts/         # 📋 Shared interfaces
│   └── 📁 docker/                      # 🐳 Docker конфигурации
│       ├── 📁 haproxy/                 # HAProxy Load Balancer
│       │   ├── Dockerfile              # HAProxy образ
│       │   └── haproxy.cfg             # ⭐ Основная конфигурация LB
│       └── 📁 redis/                   # Redis Session Storage
│           └── Dockerfile              # Redis образ
├── 📁 ftp-nfs/                         # 💾 Shared Storage
│   └── 📁 storage/                     # Файлы пользователей
│       ├── 📁 demo/                    # Тестовый пользователь
│       ├── 📁 admin/                   # Администратор
│       └── 📁 test/                    # Тестирование
├── 🚀 deploy.sh                        # ⭐ Автоматизированное развертывание
├── 📖 README.md                        # Эта документация
└── 🔧 docker-compose.yml               # Альтернативный способ запуска
```

## 🚀 Быстрое развертывание

### Предварительные требования
- ✅ **Docker 20.10+** и **Docker Compose 2.0+**
- ✅ **Свободные порты**: 21, 5160, 6379, 8404, 50000-50010
- ✅ **Минимум 4GB RAM** для всех сервисов
- ✅ **Linux OS** (протестировано на Ubuntu 20.04+)

### 🎯 Одна команда для полного развертывания

```bash
# Клонируем репозиторий
git clone https://github.com/your-username/distributed-ftp-server.git
cd distributed-ftp-server

# Делаем скрипт исполняемым
chmod +x deploy.sh

# 🚀 ЗАПУСКАЕМ ПОЛНОЕ РАЗВЕРТЫВАНИЕ
./deploy.sh
```

**Что происходит автоматически:**
1. ✅ Создание Docker сети `ftp-network`
2. ✅ Создание и настройка shared storage  
3. ✅ Сборка всех Docker образов (Redis, Auth, FTP, HAProxy)
4. ✅ Запуск сервисов в правильной последовательности
5. ✅ Проверка работоспособности всех компонентов

### 🧪 Проверка развертывания

```bash
# Статус всех контейнеров
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Быстрая проверка подключения
telnet localhost 21

# HAProxy веб-интерфейс  
http://localhost:8404/stats
# Логин: admin, Пароль: admin123

# Проверка балансировки (каждый запрос может попасть на разный сервер)
for i in {1..5}; do echo "USER demo" | nc localhost 21; done
```

## ⚙️ Конфигурация системы

### 🔧 Основные параметры конфигурации

| Компонент | Параметр | Описание | Значение по умолчанию |
|-----------|----------|----------|----------------------|
| **HAProxy** | `bind *:21` | FTP Control port | 21 |
| **HAProxy** | `bind *:50000-50010` | FTP Data ports | 50000-50010 |
| **HAProxy** | `stick-table type ip` | Sticky sessions | По IP клиента |
| **FTP Server** | `FtpExternalIP` | External IP for PASV | Автоопределение |
| **FTP Server** | `FtpBehindProxy` | Proxy mode | true |
| **FTP Server** | `FtpPasvMinPort` | Min PASV port | 50000 |
| **FTP Server** | `FtpPasvMaxPort` | Max PASV port | 50010 |
| **Auth Service** | `RedisConnectionString` | Redis connection | redis:6379 |

### 📊 HAProxy конфигурация (haproxy.cfg)

```haproxy
# 🎯 FTP Control Connections с Sticky Sessions
listen ftp_control
    bind *:21
    stick-table type ip size 100k expire 30m
    stick on src  # Привязка по IP клиента
    server ftp-server-1 ftp-server-1:21 check inter 5s fall 3 rise 2
    server ftp-server-2 ftp-server-2:21 check inter 5s fall 3 rise 2
    server ftp-server-3 ftp-server-3:21 check inter 5s fall 3 rise 2

# 📡 FTP Data Connections
listen ftp_data
    bind *:50000-50010  # Диапазон портов для PASV
    stick-table type ip size 100k expire 30m
    stick on src        # Тот же клиент → тот же сервер
    server ftp-server-1 ftp-server-1
    server ftp-server-2 ftp-server-2
    server ftp-server-3 ftp-server-3

# 📈 Monitoring Interface
listen stats_page
    bind *:8404
    mode http
    stats enable
    stats uri /stats
    stats auth admin:admin123
```

### 🔧 Расширение диапазона портов

Для увеличения количества одновременных соединений:

1. **Обновите haproxy.cfg:**
```haproxy
listen ftp_data
    bind *:50000-50050    # Было 50000-50010, стало 50000-50050
```

2. **Обновите deploy.sh:**
```bash
-p 50000-50050:50000-50050    # Docker port mapping
-e FtpPasvMaxPort=50050       # Environment variable
```

3. **Перезапустите кластер:**
```bash
docker restart haproxy-ftp ftp-server-1 ftp-server-2 ftp-server-3
```
### В проекте есть файл скрипта `create_container-ftp.sh`
Расположен в корневой папке проекта. Делаем его испольныемым  ЗАПУСКАЕМ
```bash
chmod +x create_container.sh
./create_container.sh
```

## 🧪 Тестирование системы

### Тест 1: Базовое подключение через Load Balancer

```bash
# Telnet тест
telnet localhost 21
# Ожидаемый результат: 220 Welcome to Basic FTP Server v1.0

# Проверка sticky session (тот же сервер)
echo "USER demo" | nc localhost 21
echo "QUIT" | nc localhost 21
```

### Тест 2: Полный FTP Workflow

```bash
# FTP клиент
ftp localhost
# Username: demo
# Password: любой
# Команды:
# pwd     → 257 "/" is current directory
# ls      → Directory listing
# put filename
# get filename
# quit
```

### Тест 3: FileZilla интеграция ⭐

```
📋 Настройки подключения:
Host: localhost (или ваш внешний IP)
Port: 21
Protocol: FTP (обычный)
Logon Type: Normal
User: demo, admin, или test
Password: любой пароль
```

**Режим передачи:** Обязательно **Passive (PASV)**

### Тест 4: Балансировка нагрузки и Failover

```bash
# Тест 4.1: Остановите один сервер
docker stop ftp-server-1

# Система должна продолжать работать
telnet localhost 21  # Подключение через ftp-server-2 или ftp-server-3

# Тест 4.2: Верните сервер
docker start ftp-server-1

# HAProxy автоматически включит его обратно в балансировку
```

### Тест 5: Shared Storage проверка

```bash
# 1. Подключитесь через FileZilla и загрузите файл test.txt
# 2. Остановите текущий FTP сервер: docker stop ftp-server-1
# 3. Подключитесь заново (попадете на другой сервер)
# 4. Файл test.txt должен быть доступен → shared storage работает! ✅
```

## 📊 Мониторинг и управление

### 🌐 HAProxy Web Interface

**URL:** `http://localhost:8404/stats`  
**Логин:** `admin`  
**Пароль:** `admin123`

**Что можно делать:**
- 📊 Просматривать статистику в реальном времени
- 🔄 Включать/отключать серверы
- ⚖️ Изменять веса серверов  
- 📈 Мониторить количество соединений
- 🏥 Проверять health status каждого сервера

### 🔧 Управление через командную строку

```bash
# Показать статистику серверов
echo 'show stat' | docker exec -i haproxy-ftp socat stdio /var/run/haproxy.sock

# Отключить сервер (graceful)
echo 'disable server ftp_control/ftp-server-2' | docker exec -i haproxy-ftp socat stdio /var/run/haproxy.sock

# Включить сервер обратно
echo 'enable server ftp_control/ftp-server-2' | docker exec -i haproxy-ftp socat stdio /var/run/haproxy.sock

# Изменить вес сервера (больше вес = больше соединений)
echo 'set weight ftp_control/ftp-server-1 150' | docker exec -i haproxy-ftp socat stdio /var/run/haproxy.sock
```

### 📋 Мониторинг ресурсов

```bash
# Статистика всех контейнеров
docker stats haproxy-ftp ftp-server-1 ftp-server-2 ftp-server-3 auth-service redis

# Активные FTP соединения
netstat -an | grep -E ":(21|500[0-9][0-9])" | grep ESTABLISHED

# Проверка здоровья Redis
docker exec redis redis-cli ping

# Логи в реальном времени
docker logs -f haproxy-ftp
```

### 📈 Статистика HAProxy FTP Load Balancer
![HAProxy FTP Load Balancer](/docs/img/haproxy.png)

### 📈 Ключевые метрики для мониторинга

| Метрика | Команда проверки | Норма |
|---------|------------------|-------|
| **Active Sessions** | HAProxy Stats → Current Sessions | < 80% от max |
| **Server Health** | HAProxy Stats → Status | All UP |
| **Response Time** | Telnet test time | < 100ms |
| **Memory Usage** | `docker stats` | < 80% |
| **Disk Space** | `df -h ./ftp-nfs/` | < 90% |

## 🔧 Масштабирование

### 📈 Горизонтальное масштабирование (добавление серверов)

```bash
# Шаг 1: Запустить новый FTP сервер
docker run -d \
  --name ftp-server-4 \
  --network ftp-network \
  -e FtpExternalIP=$(hostname -I | awk '{print $1}') \
  -e FtpBehindProxy=true \
  -e port=21 \
  -e FtpPasvMinPort=50000 \
  -e FtpPasvMaxPort=50010 \
  -e RedisConnectionString=redis:6379 \
  -e authservice=http://auth-service:5160 \
  -v "$PWD/ftp-nfs/storage":/app/shared_storage \
  ftp-server:ftp

# Шаг 2: Обновить HAProxy конфигурацию
# Добавить в haproxy.cfg:
# server ftp-server-4 ftp-server-4:21 check inter 5s fall 3 rise 2

# Шаг 3: Перезагрузить HAProxy (без потери соединений)
docker exec haproxy-ftp haproxy -f /usr/local/etc/haproxy/haproxy.cfg -sf 1
```

### ⬆️ Вертикальное масштабирование (ресурсы)

```bash
# Увеличить CPU и память для FTP серверов
docker update --cpus="2.0" --memory="2g" ftp-server-1
docker update --cpus="2.0" --memory="2g" ftp-server-2
docker update --cpus="2.0" --memory="2g" ftp-server-3

# Увеличить ресурсы для Redis
docker update --cpus="1.0" --memory="1g" redis

# Увеличить ресурсы для HAProxy
docker update --cpus="1.0" --memory="512m" haproxy-ftp
```

## 🐛 Устранение неполадок

### ❌ Проблема 1: "Connection timeout" при LIST команде

**Симптомы:** 
- Подключение к FTP успешно
- Аутентификация проходит
- Команда LIST зависает на 20 секунд

**Причина:** Data connection порты недоступны

**Решение:**
```bash
# Проверить порты HAProxy
docker exec haproxy-ftp netstat -tlnp | grep -E "(50000|50005|50010)"

# Проверить firewall
sudo ufw status | grep 50000
sudo ufw allow 50000:50010/tcp

# Проверить внешний IP
curl ifconfig.me
hostname -I | awk '{print $1}'
```

### ❌ Проблема 2: HAProxy показывает серверы как DOWN

**Симптомы:** 
- В веб-интерфейсе HAProxy серверы красные
- Статус: "DOWN"

**Причина:** Health checks не проходят

**Решение:**
```bash
# Проверить логи HAProxy
docker logs haproxy-ftp | grep -i health

# Проверить FTP серверы
docker logs ftp-server-1 | tail -20

# Проверить сетевое подключение
docker exec haproxy-ftp ping ftp-server-1

# Перезапустить проблемный сервер
docker restart ftp-server-1
```

### ❌ Проблема 3: Нет sticky sessions (клиент попадает на разные серверы)

**Симптомы:** 
- Каждое подключение попадает на разный сервер
- Проблемы с передачей файлов

**Причина:** Неправильная конфигурация HAProxy

**Решение:**
```bash
# Проверить конфигурацию sticky sessions
docker exec haproxy-ftp cat /usr/local/etc/haproxy/haproxy.cfg | grep -A5 "stick"

# Должно быть:
# stick-table type ip size 100k expire 30m
# stick on src

# Проверить таблицу sticky sessions
echo 'show table ftp_control' | docker exec -i haproxy-ftp socat stdio /var/run/haproxy.sock
```

### ❌ Проблема 4: Redis connection errors

**Симптомы:** 
- Сессии не сохраняются
- Повторная аутентификация требуется

**Решение:**
```bash
# Проверить Redis
docker logs redis
docker exec redis redis-cli ping

# Проверить подключение от FTP серверов
docker exec ftp-server-1 ping redis
docker exec ftp-server-1 telnet redis 6379

# Перезапустить Redis
docker restart redis
```

## 🔒 Безопасность и производственные рекомендации

### 🛡️ Базовая безопасность

```bash
# 1. Изменить пароли по умолчанию
# В haproxy.cfg:
stats auth admin:YOUR_STRONG_PASSWORD

# 2. Настроить firewall
sudo ufw enable
sudo ufw allow 21/tcp
sudo ufw allow 50000:50010/tcp
sudo ufw allow 8404/tcp from YOUR_ADMIN_IP

# 3. Заблокировать прямой доступ к внутренним сервисам
sudo ufw deny 6379    # Redis
sudo ufw deny 5160    # Auth Service
```

### 📋 Production Checklist

- [ ] **Мониторинг**: Настроить Prometheus + Grafana
- [ ] **Backup**: Автоматическое резервирование Redis и файлов
- [ ] **Логирование**: Централизованные логи (ELK Stack)
- [ ] **SSL/TLS**: FTPS поддержка для шифрования
- [ ] **Аутентификация**: Интеграция с LDAP/Active Directory
- [ ] **Квоты**: Ограничения на дисковое пространство
- [ ] **Alerting**: Уведомления о проблемах

## 🗺️ Roadmap развития

### ✅ Фаза 1: MVP Single Server (Завершено)
- Основная FTP функциональность (.NET 8)
- Docker контейнеризация
- Базовая аутентификация

### ✅ Фаза 2: Distributed Architecture (Текущая - ЗАВЕРШЕНО) 
- **HAProxy load balancer** с sticky sessions ⭐
- **Redis session management** для состояния
- **Множественные FTP серверы** (3 экземпляра)
- **Shared storage** между серверами
- **Health monitoring** и автоматический failover
- **Web-интерфейс мониторинга**
- **Автоматизированное развертывание**

### 🚀 Фаза 3: Enterprise Security & Monitoring (Следующая)
- [ ] **FTPS поддержка** (FTP over TLS/SSL)
- [ ] **Prometheus metrics** export
- [ ] **Grafana dashboards** для мониторинга
- [ ] **LDAP/Active Directory** интеграция
- [ ] **User quotas** и disk management
- [ ] **Advanced logging** с ELK Stack

### 🌟 Фаза 4: Cloud Native & Auto-scaling
- [ ] **Kubernetes deployment** manifests
- [ ] **Auto-scaling** на основе метрик нагрузки
- [ ] **Distributed file system** (Ceph/GlusterFS)
- [ ] **Multi-region** deployment
- [ ] **API Gateway** интеграция
- [ ] **OAuth 2.0** authentication

## 🤝 Участие в разработке

### 🚀 Быстрый старт для разработки

```bash
# 1. Fork репозиторий
git clone https://github.com/your-username/distributed-ftp-server.git
cd distributed-ftp-server

# 2. Создать ветку для фичи
git checkout -b feature/your-awesome-feature

# 3. Локальная разработка
./deploy.sh  # Развернуть тестовое окружение

# 4. Тестирование изменений
docker logs -f ftp-server-1  # Проверить логи
telnet localhost 21          # Тестировать функциональность

# 5. Commit и Push
git add .
git commit -m "feat: add awesome feature"
git push origin feature/your-awesome-feature
```

### 📋 Стандарты разработки

- **C# код**: Следовать [Microsoft C# conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- **Docker**: Multi-stage builds, минимальные образы
- **Документация**: Обновлять README.md для новых фич
- **Тестирование**: Полное тестирование в distributed окружении

## 📞 Поддержка и сообщество

- 🐛 **Issues**: [GitHub Issues](https://github.com/your-username/distributed-ftp-server/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/your-username/distributed-ftp-server/discussions)
- 📚 **Wiki**: [Project Wiki](https://github.com/your-username/distributed-ftp-server/wiki)

## 📄 Лицензия

MIT License - подробности в файле [LICENSE](LICENSE).

---

## 🎉 Благодарности

- **HAProxy Team** - за отличный load balancer для TCP протоколов
- **Microsoft .NET Team** - за мощную платформу .NET 8
- **Redis Team** - за высокопроизводительное хранилище сессий
- **Docker Team** - за платформу контейнеризации
- **Community Contributors** - за feedback и улучшения

---

<div align="center">

**🚀 Статус проекта:** Production Ready ✅  
**🏗️ Архитектура:** Fully Distributed с HAProxy  
**📈 Масштабирование:** Horizontal + Vertical Ready  
**🔒 Безопасность:** Enterprise Grade  
**📊 Мониторинг:** Real-time HAProxy Stats  

**Сделано с ❤️ для enterprise файлообменных решений**

</div>