# Docker file for Redis

# 1. Basic Image Linux
FROM alpine:3.18

# 2. Update and installing Redis
RUN apk update && \
    apk add --no-cache redis && \
    rm -rf /var/cache/apk/*
    
# 3. Create user Redis (secure)
#RUN addgroup -g 1000 redis && \ 
#    adduser -D -s /bin/sh -u 1000 -G redis redis
    
# 4. Create directory for data and config
RUN mkdir -p /data /etc/redis && \
    chown -R redis:redis /data /etc/redis

# 5. Copy custom's config Redis
COPY redis.conf /etc/redis/redis.conf

# 6. Install rules on the config
RUN chown redis:redis /etc/redis/redis.conf

# 7. Open port 6379 (standard Redis port)
EXPOSE 6379

# 8. Switch on user of Redis
USER redis

# 9. Work directory for data
WORKDIR /data

# 10. Entry point - starting Redis server
ENTRYPOINT ["redis-server", "/etc/redis/redis.conf"]

# 11. Command by default (may be redefinition
CMD []
