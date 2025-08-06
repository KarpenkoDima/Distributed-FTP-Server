# Exit immediately if a command exits with a non-zero status.
set -e

# Define a variable for the project root to make the script more robust
# and easier to read.
PROJECT_ROOT=$(pwd)

# Stop and remove old containers gracefully.
# The '2>/dev/null || true' part suppresses errors if the containers don't exist
# and ensures the script continues to run.
docker stop haproxy-ftp ftp-server-1 ftp-server-2 ftp-server-3 auth-service redis 2>/dev/null || true
docker rm haproxy-ftp ftp-server-1 ftp-server-2 ftp-server-3 auth-service redis 2>/dev/null || true

# --- Network Setup ---
# Check if the Docker network 'ftp-network' already exists.
# The '!' inverts the check, so the 'then' block runs only if the network does NOT exist.
if ! docker network inspect ftp-network &>/dev/null; then
    echo "Docker network 'ftp-network' not found. Creating it..."
    docker network create ftp-network
else
    echo "Docker network 'ftp-network' already exists."
fi

# Print network status for verification
echo "Network 'ftp-network' status:"
docker network ls | grep ftp-network

# --- Shared Storage Setup ---
# Create a shared folder for all FTP servers
mkdir -p "$PROJECT_ROOT/ftp-nfs/storage"

# Set the correct permissions for the Docker user (ID 1001)
echo "Setting ownership of shared storage to user 1001..."
sudo chown -R 1001:1001 "$PROJECT_ROOT/ftp-nfs/storage"

# --- Docker Image Builds ---
echo "Building Docker images..."

# Redis (session store)
cd "$PROJECT_ROOT/Distributed-FTP-Server/docker/redis/"
docker build -f Dockerfile -t redis:ftp .

# Auth Service (authentication)
cd "$PROJECT_ROOT/Distributed-FTP-Server"
docker build -f FtpServer.Auth/Dockerfile -t auth-service:ftp .

# FTP Server (core servers)
docker build -f FtpServer.Core/Dockerfile -t ftp-server:ftp .

# Nginx (load balancer)
cd "$PROJECT_ROOT/Distributed-FTP-Server/docker/haproxy/"
docker build -f Dockerfile -t haproxy:ftp .

# --- Container Deployment ---
echo "Deploying containers..."

# Redis
docker run -d \
  --name redis \
  --network ftp-network \
  -p 6379:6379 \
  redis:ftp

sleep 5

# Auth Service
docker run -d \
  --name auth-service \
  --network ftp-network \
  -p 5160:5160 \
  -e RedisConnectionString=redis:6379 \
  auth-service:ftp

# Get your external IP address
EXTERNAL_IP=$(hostname -I | awk '{print $1}')
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
  -v "$PROJECT_ROOT/ftp-nfs/storage":/app/shared_storage \
  ftp-server:ftp

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
  -v "$PROJECT_ROOT/ftp-nfs/storage":/app/shared_storage \
  ftp-server:ftp

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
  -v "$PROJECT_ROOT/ftp-nfs/storage":/app/shared_storage \
  ftp-server:ftp

sleep 3

## Nginx Proxy
#docker run -d \
#  --name nginx-ftp-proxy \
#  --network ftp-network \
#  -p 21:21 \
#  -p 50001:50001 \
#  -p 50002:50002 \
#  -p 50003:50003 \
#  -p 8080:8080 \
#  -v "$PROJECT_ROOT/Distributed-FTP-Server/docker/nginx/nginx.conf":/etc/nginx/conf.d/nginx.conf \
#  nginx-ftp:ftp
# HAProxy
docker run -d \
  --name haproxy-ftp \
  --network ftp-network \
  -p 21:21 \
  -p 50000-50010:50000-50010 \
  -p 8404:8404 \
  --sysctl net.ipv4.ip_unprivileged_port_start=0 \
  haproxy:ftp

# --- Verification ---
echo "--- Deployment Complete ---"
echo "Verifying running containers..."
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo "Verifying network configuration..."
docker network inspect ftp-network

echo "Displaying logs for each service..."
docker logs redis
docker logs auth-service
docker logs ftp-server-1
docker logs haproxy-ftp