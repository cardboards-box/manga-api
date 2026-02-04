#!/bin/sh

app_name="mangabox"
root_dir="temp-env"

create_dirs() {
  mkdir -p "./$root_dir"
  mkdir -p "./$root_dir/setup"
  mkdir -p "./$root_dir/redis"
  mkdir -p "./$root_dir/postgres"
  mkdir -p "./$root_dir/file-cache"
  mkdir -p "./$root_dir/jwt-key"
}

create_env() {
  filename="./$root_dir/.env"
  if [ -e "$filename" ]; then
    echo "File $filename already exists. Skipping creation."
    return
  fi

  echo "Creating $filename file..."
  starting_port=9991
  api_port=$((starting_port + 1))
  db_port=$((starting_port + 2))
  redis_port=$((starting_port + 3))

  postgres_username=$(tr -dc 'a-z' < /dev/urandom | head -c 10)
  postgres_password=$(tr -dc 'A-Za-z0-9!?%=' < /dev/urandom | head -c 64)
  redis_password=$(tr -dc 'A-Za-z0-9!?%=' < /dev/urandom | head -c 64)

  cat > "$filename" <<EOF

POSTGRES_USERNAME=$postgres_username
POSTGRES_PASSWORD=$postgres_password
POSTGRES_SCHEMA=$app_name

REDIS_PASSWORD=$redis_password

PORT_API=$api_port
PORT_DB=$db_port
PORT_REDIS=$redis_port

OAUTH_APPID=
OAUTH_SECRET=
EOF
  echo "$filename created"
}

create_compose() {
  filename="./$root_dir/docker-compose.yml"
  if [ -e "$filename" ]; then
    echo "File $filename already exists. Skipping creation."
    return
  fi

  cat > "$filename" <<'EOF'
networks:
  app-network:
    driver: bridge

services:

  app-redis:
    image: redis/redis-stack-server:latest
    restart: always
    ports:
      - ${PORT_REDIS}:6379
    environment:
      - REDIS_ARGS=--requirepass ${REDIS_PASSWORD}
    volumes:
      - ./redis:/data
    networks:
      - app-network

  app-db:
    image: postgres:latest
    restart: always
    ports:
      - ${PORT_DB}:5432
    volumes:
      - ./postgres:/var/lib/postgresql/data
    environment:
      - POSTGRES_USER=${POSTGRES_USERNAME}
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
      - POSTGRES_DB=${POSTGRES_SCHEMA}
    networks:
      - app-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USERNAME} -d ${POSTGRES_SCHEMA}"]
      interval: 5s
      timeout: 5s
      retries: 5
  
  app-api:
    image: ghcr.io/cardboards-box/manga-api/api:latest
    restart: always
    ports:
      - ${PORT_API}:8080
    volumes:
      - ./file-cache:/app/file-cache
      - ./jwt-key:/app/jwt-key
    environment:
      - Database:ConnectionString=User ID=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD};Host=app-db;Database=${POSTGRES_SCHEMA};
      - Redis:Connection=app-redis,password=${REDIS_PASSWORD}
      - Imaging:CacheDir=./file-cache
      - OAuth:Jwt:KeyPath=./jwt-key/key.pem
      - OAuth:AppId=${OAUTH_APPID}
      - OAuth:Secret=${OAUTH_SECRET}
    networks:
      - app-network
    depends_on:
      - app-db
      - app-redis
EOF
  echo "$filename created"
}


create_dirs
create_env
create_compose

chmod 777 -R "./$root_dir"

cd "./$root_dir"

docker pull ghcr.io/cardboards-box/manga-api/api:latest
docker compose up -d

cd ..