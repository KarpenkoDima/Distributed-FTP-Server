# MVP FTP Server

Этот репозиторий содержит реализацию **FTP-сервера на .NET C# с фокусом на будущем масштабировании и поддержке FTPS**. Проект сосредоточен на разработке собственного ядра FTP-сервера и сервиса аутентификации, интегрируя их с готовыми проверенными решениями для балансировки нагрузки, хранения сессий и файлов (планируется).

## 📄 Документация

* [Архитектура и системный дизайн](docs/ArchitectureDesign.md)
* [План разработки](docs/DevelopmentPlan.md)

## 🧩 Компоненты

* **FtpServer.Core:** Основное ядро FTP-сервера.
* **FtpServer.Auth:** Сервис аутентификации на ASP.NET Core Web API.
* **FtpServer.Commons:** Общая библиотека классов для моделей (DTO) и интерфейсов, используемых между сервисами.
* **Сторонние решения:** nginx, Redis, распределенная файловая система.

## 📂 Структура проекта

FtpServerMVP/
├── docker/                   # Dockerfiles, conf-files for redis, nginx
├── FtpServer.Core/           # Основной FTP сервер
├── FtpServer.Auth/           # Authentication API  
└── FtpServer.Commons		  # Shared models/interfaces

## Начало работы
### Запуск проектов: Тестируем запуск на разных портах:

	В appsettings.json изменить номер порта 
```bash
	   cd FtpServer.Core 
	   dotnet run
```
	
```bash
	   cd FtpServer.Auth 
	   dotnet run
```
```bash
# К серверу на порту 2121
echo -e "USER demo\nPASS 1\nQUIT" | nc localhost 2121

# К серверу на порту 2122  
echo -e "USER demo\nPASS 1\nQUIT" | nc localhost 2122

# К серверу на порту 2123
echo -e "USER demo\nPASS 1\nQUIT" | nc localhost 2123
```

# Настройка Распределенного FTP-сервера

Инструкции по настройке распределенной среды FTP-сервера с использованием Docker. Настройка включает пользовательскую службу аутентификации (`AuthService`), `FtpServer`, `nginx` и `Redis` для кэширования/управления сессиями.

## Предварительные условия

  * [Docker](https://docs.docker.com/get-docker/) установлен в системе.

  * `Dockerfile` для `Nginx` находится в текущем каталоге или подходящий образ `Nginx` доступен на Docker Hub.

  * `Dockerfile` для `AuthService` находится по адресу `/FtpServer.Auth/Dockerfile`.
  
  * Запускаем контейнеры в правильном порядке 
        
    ### Последовательность запуска:
    * `Redis` (нет зависимостей)
    * `Auth Service` (зависит от `Redis`)
    * `FTP Servers` (зависят от `Redis` + `Auth Service`)
    * `nginx` (зависит от `FTP Servers`)
 
-----

## 1\. Настройка Nginx

Этот шаг включает подготовку образа Docker для `Nginx`.

### Вариант A: Сборка образа Docker для Nginx (если у вас есть пользовательский Dockerfile)

Если у вас есть пользовательский `Dockerfile` для Nginx в текущем каталоге, вы можете собрать свой образ:

```bash
sudo docker build -f Dockerfile -t nginx-ftp:docker-ready .
```

### Вариант B: Загрузка официального образа Nginx (если используется стандартный образ)

В качестве альтернативы вы можете загрузить стандартный образ Nginx из Docker Hub:

```bash
docker pull nginx
```

-----

## 2\. Создание сети Docker для распределенного FTP-сервера

Выделенная мостовая сеть необходима для обеспечения связи между контейнерами по их именам служб.

### Создание сети

```bash
docker network create ftp-network
```

### Проверка создания сети

Убедитесь, что сеть `ftp-network` успешно создана:

```bash
docker network ls | grep ftp-network
```

**Ожидаемый результат:**

```
NETWORK ID        NAME          DRIVER    SCOPE
<ваш_id>          ftp-network   bridge    local
```

### Просмотр деталей сети (необязательно)

Чтобы увидеть более подробную информацию о созданной сети:

```bash
docker network inspect ftp-network
```

-----

## 3\. Настройка Redis

Этот раздел посвящен сборке образа Redis (если пользовательский) и запуску контейнера Redis.

### Сборка образа Docker для Redis (если пользовательский, или используйте `redis:latest`)


```bash

sudo docker build -f redis/Dockerfile -t redis-master:docker-ready .
```

### Запуск контейнера Redis

Запустите контейнер Redis, открыв порт 6379 и подключив его к `ftp-network`.

```bash
sudo docker run -d --name redis -p 6379:6379 --network ftp-network redis:docker-ready
```

### Проверка статуса контейнера Redis

Проверьте логи, чтобы убедиться, что Redis успешно запущен:

```bash
docker logs redis
```

-----

## 4\. Настройка службы аутентификации (Auth Service)

Этот раздел подробно описывает сборку и запуск пользовательской службы аутентификации.

### Сборка образа Docker для службы аутентификации

Соберите образ Docker для `auth-service` из его `Dockerfile`.

```bash
sudo docker build -f FtpServer.Auth/Dockerfile -t auth-service:fixed-port .
```

### Запуск контейнера службы аутентификации

Запустите контейнер `auth-service`, открыв порт 5160 и подключив его к `ftp-network`.

```bash
sudo docker run -d \
--name auth-service \
--network ftp-network \
-p 5160:5160 \
auth-service:fixed-port
```

### Проверка статуса контейнера службы аутентификации

Проверьте логи, чтобы убедиться, что служба аутентификации успешно запущена:

```bash
docker logs auth-service
```

-----

## 5\. Проверка подключения службы аутентификации к Redis

Крайне важно убедиться, что `auth-service` может связываться с контейнером `redis`.

### Проверка подключения с помощью `ping` (первая попытка)

Попробуйте выполнить `ping` контейнера `redis` изнутри контейнера `auth-service`.

```bash
sudo docker exec auth-service ping redis
```

**Ожидаемый успешный вывод:**

```
PING redis 56(84) bytes of data.
64 bytes from redis.ftp-network (172.18.0.2): icmp_seq=1 ttl=64 time=0.039 ms
64 bytes from redis.ftp-network (172.18.0.2): icmp_seq=2 ttl=64 time=0.049 ms
64 bytes from redis.ftp-network (172.18.0.2): icmp_seq=3 ttl=64 time=0.137 ms
...
```

### Устранение неполадок: команда `ping` не найдена

Если вы столкнулись с ошибкой `exec: "ping": executable file not found in $PATH`, это означает, что утилита `ping` не установлена в контейнере `auth-service`. Вы можете установить ее для целей отладки:

1.  **Получите доступ к оболочке контейнера `auth-service`:**

    ```bash
    sudo docker exec -it auth-service bash
    ```

2.  **Установите `iputils-ping` (для образов на основе Debian/Ubuntu):**

    ```bash
    # Внутри контейнера:
    apt-get update
    apt-get install -y iputils-ping
    ```

    (Если ваш образ основан на Alpine, используйте `apk add --no-cache iputils` вместо команд `apt-get`).

3.  **Выйдите из оболочки контейнера:**

    ```bash
    exit
    ```

4.  **Повторно запустите тест `ping`:**

    ```bash
    sudo docker exec auth-service ping redis
    ```

    Теперь вы должны увидеть успешные ответы `ping`, как показано выше в разделе "Ожидаемый успешный вывод".

Отлично! 🚀 Продолжаем с FTP серверами.

## 📝 **Шаг Запускаем FTP Servers с environment variables**

### **Останавливаем старые FTP серверы:**

```bash
# Останавливаем все старые FTP серверы
docker stop ftp-server-1 2>/dev/null || true
docker rm ftp-server-1 2>/dev/null || true
```

### **Запускаем FTP Server 1:**

```bash
docker run -d \
  --name ftp-server-1 \
  --network ftp-network \
  -e RedisConnectionString=redis:6379 \  # для пременных конфигурации в appsettings.json
  -e authservice=http://auth-service:5160 \ 
  -e port=2121 \
  ftp-server:fixed

# Проверяем логи
docker logs ftp-server-1
```
### Повторим для остальных серверов ftp-server-2, ftp-server-3 ....


### **Проверяем connectivity между сервисами:**

```bash
# FTP Server должен видеть Redis
docker exec ftp-server-1 ping redis

# FTP Server должен видеть Auth Service  
docker exec ftp-server-1 ping auth-service

# Проверяем что все контейнеры в одной сети
docker network inspect ftp-network
```

**Ожидаемый результат в логах FTP серверов:**
```
✅ Redis connected: redis:6379
🚀 FTP Server started on port 2121
```
Отлично! 🎉 Переходим к финальному шагу - nginx load balancer.

## 📝 **Шаг: Запускаем nginx Load Balancer (финал)**

### **Останавливаем старый nginx:**

```bash
# Останавливаем старый nginx если есть
docker stop nginx-ftp 2>/dev/null || true
docker rm nginx-ftp 2>/dev/null || true
```

### **Запускаем nginx в ftp-network:**

```bash
docker run -d \
  --name nginx-lb \
  --network ftp-network \
  -p 21:21 \
  nginx-ftp:docker-ready

# Проверяем логи nginx
docker logs nginx-lb
```

### **Проверяем что nginx видит все FTP серверы:**

```bash
# nginx должен видеть все FTP серверы
docker exec nginx-lb ping ftp-server-1
docker exec nginx-lb ping ftp-server-2  
docker exec nginx-lb ping ftp-server-3

# Смотрим статус всех контейнеров
docker ps
```

### **Финальная проверка всей системы:**

```bash
# Смотрим все контейнеры в нашей сети
docker network inspect ftp-network

# Должны видеть 6 контейнеров:
# - redis
# - auth-service  
# - ftp-server-1, ftp-server-2, ftp-server-3
# - nginx-lb
```

**Ожидаемый результат:**
```
CONTAINER ID   IMAGE                      STATUS
xxx            ftp-redis:latest          Up
xxx            auth-service:port-fixed   Up  
xxx            ftp-server:fixed          Up
xxx            ftp-server:fixed          Up
xxx            ftp-server:fixed          Up
xxx            nginx-ftp:docker-ready    Up
```

## 🧪 **Шаг : Тестируем distributed FTP систему**

### **Тест 1: Простое подключение**

```bash
# Тестируем подключение через nginx load balancer
telnet localhost 21
```

**Ожидаемый результат:**
```
220 Welcome to Basic FTP Server v1.0.
Input "USER demo" or "USER test" and any password
```

### **Тест 2: Аутентификация**

```
USER demo
331 Password required  
PASS 1
230 Login successful
```
✅ Что мы создали:
Полнофункциональная Distributed FTP Server Architecture:

🔄 Load Balancing Layer: nginx распределяет нагрузку между FTP серверами

🔐 Session Management: Redis сохраняет сессии между серверами

🛡️ Authentication: Централизованный Auth Service

📁 User Isolation: Каждый пользователь в изолированной папке

🐳 Containerized: Все сервисы в Docker контейнерах

🌐 Docker Networking: Сервисы общаются по именам

 ## Готовы к облачному тестированию!
Теперь у нас есть полностью рабочая система которую можно развернуть на Play With Docker для тестирования в реальном cloud окружении.