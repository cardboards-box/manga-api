#!/bin/sh

set -eu

app_name="mangabox"
root_dir="temp-env"
local_build=true

clean_env() {
  if [ ! -d "./$root_dir" ]; then
    echo "Directory ./$root_dir does not exist. Nothing to clean."
    return
  fi

  if [ -f "./$root_dir/docker-compose.yml" ]; then
    (
      cd "./$root_dir"
      docker compose down --remove-orphans || true
    )
  fi

  find "./$root_dir" -mindepth 1 \
    ! -name "logs" \
    ! -name "jwt-key" \
    -exec rm -rf {} +

  mkdir -p "./$root_dir/logs" "./$root_dir/jwt-key"
  echo "Cleaned ./$root_dir (preserved logs and jwt-key)."
}

create_dirs() {
  mkdir -p "./$root_dir"
  mkdir -p "./$root_dir/setup"
  mkdir -p "./$root_dir/redis"
  mkdir -p "./$root_dir/postgres"
  mkdir -p "./$root_dir/file-cache"
  mkdir -p "./$root_dir/jwt-key"
  mkdir -p "./$root_dir/logs"
}

create_env() {
  filename="./$root_dir/.env"
  if [ -e "$filename" ]; then
    echo "File $filename already exists. Skipping creation."
    return
  fi

  echo "Creating $filename file..."
  starting_port=10100
  api_port=$((starting_port + 1))
  db_port=$((starting_port + 2))
  redis_port=$((starting_port + 3))
  flare_port=$((starting_port + 4))

  postgres_username=$(LC_ALL=C tr -dc 'a-z' < /dev/urandom | head -c 10)
  postgres_password=$(LC_ALL=C tr -dc 'A-Za-z0-9!?%=' < /dev/urandom | head -c 64)
  redis_password=$(LC_ALL=C tr -dc 'A-Za-z0-9!?%=' < /dev/urandom | head -c 64)

  cat > "$filename" <<EOF
POSTGRES_USERNAME=$postgres_username
POSTGRES_PASSWORD=$postgres_password
POSTGRES_SCHEMA=$app_name

REDIS_PASSWORD=$redis_password

PORT_API=$api_port
PORT_DB=$db_port
PORT_REDIS=$redis_port
PORT_FLARE=$flare_port

OAUTH_APPID=
OAUTH_SECRET=
MATCH_URL=
FLARE_URL=
SAUCE_TOKEN=
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
    restart: unless-stopped
    ports:
      - ${PORT_REDIS}:6379
    environment:
      - REDIS_ARGS=--requirepass ${REDIS_PASSWORD}
    volumes:
      - ./redis:/data
    networks:
      - app-network

  app-db:
    image: postgres:17
    restart: unless-stopped
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

  app-solver:
    image: ghcr.io/flaresolverr/flaresolverr:latest
    restart: unless-stopped
    ports:
      - "${PORT_FLARE}:8191"
    networks:
      - app-network

  app-cli:
EOF

  if [ "$local_build" = "true" ]; then
    cat >> "$filename" <<'EOF'
    build:
      context: ..
      dockerfile: cli.Dockerfile
EOF
  else
    cat >> "$filename" <<'EOF'
    image: ghcr.io/cardboards-box/manga-api/cli:latest
EOF
  fi

  cat >> "$filename" <<'EOF'
    command: ["setup"]
    restart: "no"
    environment:
      - Database:ConnectionString=User ID=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD};Host=app-db;Database=${POSTGRES_SCHEMA};
    networks:
      - app-network
    depends_on:
      app-db:
        condition: service_healthy
  
  app-api:
EOF

  if [ "$local_build" = "true" ]; then
    cat >> "$filename" <<'EOF'
    build:
      context: ..
      dockerfile: api.Dockerfile
EOF
  else
    cat >> "$filename" <<'EOF'
    image: ghcr.io/cardboards-box/manga-api/api:latest
EOF
  fi

  cat >> "$filename" <<'EOF'
    restart: unless-stopped
    ports:
      - ${PORT_API}:8080
    volumes:
      - ./file-cache:/app/file-cache
      - ./jwt-key:/app/jwt-key
      - ./creds.json:/app/creds.json
      - ./logs:/app/logs
    environment:
      - Database:ConnectionString=User ID=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD};Host=app-db;Database=${POSTGRES_SCHEMA};
      - Redis:Connection=app-redis,password=${REDIS_PASSWORD}
      - FlareSolver:Url=http://app-solver:8191
      - OAuth:Jwt:KeyPath=./jwt-key/key.pem
      - OAuth:AppId=${OAUTH_APPID}
      - OAuth:Secret=${OAUTH_SECRET}
      - Match:Url=${MATCH_URL}
      - Match:SauceToken=${SAUCE_TOKEN}
      - GOOGLE_APPLICATION_CREDENTIALS=creds.json
      - Imaging:CacheDir=./file-cache
    networks:
      - app-network
    depends_on:
      app-cli:
        condition: service_completed_successfully
      app-db:
        condition: service_healthy
      app-redis:
        condition: service_started
      app-solver:
        condition: service_started
EOF
  echo "$filename created"
}


if [ "${1:-}" = "clean" ]; then
  clean_env
fi

create_dirs
create_env
create_compose

chmod -R 777 "./$root_dir"

cd "./$root_dir"

if [ "$local_build" != "true" ]; then
  docker pull ghcr.io/cardboards-box/manga-api/api:latest
  docker pull ghcr.io/cardboards-box/manga-api/cli:latest
fi
docker compose up -d

cd ..