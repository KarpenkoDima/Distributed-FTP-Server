# Используем официальный образ HAProxy.
FROM haproxy:2.3

# Копируем наш файл haproxy.cfg в стандартное расположение конфигурации внутри контейнера.
# Убедитесь, что haproxy.cfg находится в той же папке, что и Dockerfile
COPY haproxy.cfg /usr/local/etc/haproxy/haproxy.cfg

# Документируем порты
EXPOSE 21
EXPOSE 50000-50010