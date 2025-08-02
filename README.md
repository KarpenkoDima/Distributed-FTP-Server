# MVP FTP Server - Single Instance

Этот репозиторий содержит реализацию **FTP-сервера на .NET C# с фокусом на будущем масштабировании**. На данном этапе реализован и протестирован **одиночный FTP сервер** для валидации core функциональности перед переходом к распределенной архитектуре.

## 🎯 Текущий статус: MVP Single FTP Server ✅

**Что работает:**
- ✅ Основной FTP сервер с полной функциональностью
- ✅ Docker контейнеризация
- ✅ Пользовательская аутентификация  
- ✅ Файловое хранилище через bind mount
- ✅ Поддержка команд: USER, PASS, PWD, CWD, LIST, STOR, RETR, PASV
- ✅ Passive режим для передачи файлов
- ✅ Пользовательская изоляция (каждый пользователь в своей папке)
- ✅ Тестирование с FileZilla

## 🧩 Компоненты

### Текущие (MVP):
* **FtpServer.Core:** Основное ядро FTP-сервера
* **FtpServer.Auth:** Сервис аутентификации на ASP.NET Core Web API  
* **FtpServer.Commons:** Общая библиотека классов для моделей и интерфейсов

### Планируемые (Distributed):
* **nginx:** Load balancer для FTP серверов
* **Redis:** Управление сессиями между серверами
* **NFS/распределенная ФС:** Shared storage для файлов

## 📂 Структура проекта

```
FtpServer/
├── FtpServer.Core/           # Основной FTP сервер
│   ├── BasicFtpServer.cs     # Core FTP логика
│   ├── Program.cs            # Entry point
│   ├── Dockerfile            # Docker образ
│   └── appsettings.json      # Конфигурация
├── FtpServer.Auth/           # Authentication API  
├── FtpServer.Commons/        # Shared models/interfaces
└── app/
    └── shared_storage/       # Файловое хранилище (bind mount)
        ├── demo/             # Папка пользователя demo
        ├── admin/            # Папка пользователя admin  
        └── test/             # Папка пользователя test
```

## 🚀 Быстрый старт

### Предварительные требования
- Docker установлен
- Порты 21 и 50000-50010 свободны

### 1. Подготовка файлового хранилища

```bash
# Создайте папку для файлов на хосте
mkdir -p /path/to/your/ftp/storage

# Дайте права Docker пользователю (UID 1001)
sudo chown -R 1001:1001 /path/to/your/ftp/storage
```

### 2. Сборка Docker образа

```bash
# Из корневой папки проекта
docker build -f FtpServer.Core/Dockerfile -t ftp-server:mvp .
```

### 3. Запуск FTP сервера

```bash
# Узнайте ваш IP адрес
curl ifconfig.me

# Запустите контейнер (замените YOUR_HOST_IP и /path/to/your/ftp/storage)
docker run -d \
  --name ftp-server-mvp \
  -p 21:21 \
  -p 50000-50010:50000-50010 \
  -e FtpExternalIP=YOUR_HOST_IP \
  -e FtpBehindProxy=false \
  -e port=21 \
  -v /path/to/your/ftp/storage:/app/shared_storage \
  ftp-server:mvp

```

### 4. Проверка работы

```bash
# Проверьте логи
docker logs -f ftp-server-mvp

# Должны увидеть:
# 📁 Using SHARED storage: /app/shared_storage
# 🚀 FTP Server started on port 2122
```

## 🧪 Тестирование

### Командная строка (telnet)

```bash
# Подключение
telnet localhost 2122

# В telnet сессии:
USER demo
PASS any_password
PWD
LIST
QUIT
```

### FileZilla

```
Host: localhost (или ваш IP)
Port: 2122
Protocol: FTP  
User: demo (или admin, test)
Password: любой
Transfer mode: Passive
```

### Тестовые пользователи

| Пользователь | Пароль | Описание |
|-------------|--------|----------|
| demo        | любой  | Тестовый пользователь |
| admin       | любой  | Администратор |
| test        | любой  | Тестовый пользователь |

## 📁 Файловая структура

После запуска в папке хранилища автоматически создаются:

```
your_storage_folder/
├── demo/
│   ├── uploads/          # Папка для загрузки файлов
│   └── downloads/        # Папка с тестовыми файлами
│       ├── readme.txt
│       └── sample.txt
├── admin/
│   ├── uploads/
│   └── downloads/
└── test/
    ├── uploads/
    └── downloads/
```

## ⚙️ Конфигурация

### Переменные окружения

| Переменная | Описание | По умолчанию |
|------------|----------|--------------|
| `FtpExternalIP` | Внешний IP для PASV режима | 127.0.0.1 |
| `FtpBehindProxy` | Режим работы за прокси | false |
| `port` | Порт FTP сервера | 2122 |
| `FtpPasvMinPort` | Минимальный порт для данных | 50000 |
| `FtpPasvMaxPort` | Максимальный порт для данных | 50010 |
| `SharedStoragePath` | Путь к хранилищу файлов | /app/shared_storage |


### appsettings.json

```json
{
  "port": 2122,
  "authservice": "http://localhost:5160",
  "RedisConnectionString": "localhost:6379",
  "FtpExternalIP": "127.0.0.1",
  "FtpBehindProxy": false,
  "FtpPasvMinPort": 50000,
  "FtpPasvMaxPort": 50010,
  "SharedStoragePath": "/app/shared_storage"
}
```

## 🔧 Полезные команды

### Управление контейнером

```bash
# Остановить
docker stop ftp-server-mvp

# Перезапустить  
docker restart ftp-server-mvp

# Удалить
docker stop ftp-server-mvp && docker rm ftp-server-mvp

# Логи в реальном времени
docker logs -f ftp-server-mvp

# Зайти в контейнер
docker exec -it ftp-server-mvp bash
```

### Отладка

```bash
# Проверить файлы в контейнере
docker exec ftp-server-mvp ls -la /app/shared_storage

# Проверить файлы на хосте
ls -la /path/to/your/ftp/storage

# Проверить сетевые подключения
netstat -tlnp | grep 2122
netstat -tlnp | grep 50000
```

## 🐛 Устранение неполадок

### Проблема: Access denied при создании папок

```bash
# Решение: исправить права на папку хранилища
sudo chown -R 1001:1001 /path/to/your/ftp/storage
```

### Проблема: FileZilla не может передавать файлы

```bash
# Проверьте что внешний IP правильный
curl ifconfig.me

# Убедитесь что порты данных открыты
docker run ... -e FtpExternalIP=YOUR_REAL_IP ...
```

### Проблема: Порты заняты

```bash
# Найти процессы использующие порты
sudo netstat -tlnp | grep 21
sudo netstat -tlnp | grep 50000

# Или использовать другие порты
docker run ... -p 2121:21 -p 50100-50110:50000-50010 ...
```

## 🗺️ Roadmap

### ✅ Фаза 1: MVP Single Server (текущая)
- Основная FTP функциональность
- Docker контейнеризация
- Файловое хранилище
- Базовая аутентификация

### 🔄 Фаза 2: Distributed Architecture (в разработке)
- nginx load balancer
- Redis session management  
- Множественные FTP серверы
- NFS shared storage

### 🚀 Фаза 3: Production Ready
- FTPS поддержка
- Monitoring и логирование
- Health checks
- Auto-scaling

## 📝 Логи и мониторинг

### Типичные логи при нормальной работе:

```
📁 Using SHARED storage: /app/shared_storage
📁 FTP Root directory: /app/shared_storage
🚀 FTP Server started on port 21
📞 New client connected: 172.17.0.1:54321
👤 [abc12345] User: demo
✅ [abc12345] Authentication successful for demo
🔧 [abc12345] PASV Config: Proxy=false, IP=192.168.1.100, Range=50000-50010
📡 [abc12345] Data connection prepared on port 50000
```

## 🤝 Участие в разработке

1. Fork репозиторий
2. Создайте feature branch
3. Зафиксируйте изменения
4. Создайте Pull Request

## 📄 Лицензия

MIT License - см. файл LICENSE для деталей.

---

**Статус проекта:** MVP Ready ✅ | **Следующий этап:** Distributed Architecture 🔄