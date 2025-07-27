# Настройка Распределенного FTP-сервера

Этот файл `README.md` содержит инструкции по настройке распределенной среды FTP-сервера с использованием Docker. Настройка. пока включает пользовательскую службу аутентификации (`AuthService`) и Redis для кэширования/управления сессиями.

## Предварительные условия

  * [Docker](https://docs.docker.com/get-docker/) установлен в системе.

  * `Dockerfile` для Nginx находится в текущем каталоге или подходящий образ Nginx доступен на Docker Hub.

  * `Dockerfile` для `AuthService` находится по адресу `/FtpServer.Auth/Dockerfile`.

-----

## 1\. Настройка Nginx

Этот шаг включает подготовку образа Docker для Nginx.

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

-----

Этот файл `README.md` предоставляет пошаговое руководство по настройке компонентов  `AuthService` && `redis`