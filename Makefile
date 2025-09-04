# SMO Online Server Makefile

.PHONY: help build run run-release build-release clean restore test docker-build docker-run docker-logs docker-stop docker-up docker-down docker-restart docker-ps docker-exec

# Default target
help:
	@echo "Available targets:"
	@echo "  build        - Build the project in Debug mode"
	@echo "  build-release - Build the project in Release mode"
	@echo "  run          - Run the server in Debug mode"
	@echo "  run-release  - Run the server in Release mode"
	@echo "  test-client  - Run the test client (requires SERVER_IP=<ip>)"
	@echo "  clean        - Clean build artifacts"
	@echo "  restore      - Restore NuGet packages"
	@echo "  docker-build - Build Docker images for all architectures"
	@echo "  docker-run   - Start server with docker compose"
	@echo "  docker-up    - Start containers in detached mode"
	@echo "  docker-down  - Stop and remove containers"
	@echo "  docker-restart - Restart Docker containers"
	@echo "  docker-logs  - View Docker container logs"
	@echo "  docker-stop  - Stop Docker containers"
	@echo "  docker-ps    - Show running containers"
	@echo "  docker-exec  - Execute shell in running container"

# Build targets
build:
	dotnet build

build-release:
	dotnet build -c Release

build-server:
	dotnet build Server/Server.csproj

# Run targets
run:
	dotnet run --project Server/Server.csproj

run-release:
	dotnet run --project Server/Server.csproj -c Release

# Test client (usage: make test-client SERVER_IP=127.0.0.1)
test-client:
	@if [ -z "$(SERVER_IP)" ]; then \
		echo "Usage: make test-client SERVER_IP=<server-ip>"; \
		echo "Example: make test-client SERVER_IP=127.0.0.1"; \
		exit 1; \
	fi
	dotnet run --project TestClient/TestClient.csproj $(SERVER_IP)

# Package management
restore:
	dotnet restore

clean:
	dotnet clean

# Docker targets
docker-build:
	./docker-build.sh all

docker-build-x64:
	./docker-build.sh x64

docker-build-arm:
	./docker-build.sh arm

docker-build-arm64:
	./docker-build.sh arm64

docker-build-win64:
	./docker-build.sh win64

docker-run:
	docker compose up --build -d

docker-up:
	docker compose up -d

docker-down:
	docker compose down

docker-restart:
	docker compose restart

docker-logs:
	docker compose logs --tail=20 --follow

docker-stop:
	docker compose stop

docker-ps:
	docker compose ps

docker-exec:
	docker compose exec smo-online-server /bin/bash

# Development workflow
dev: restore build run

release: restore build-release run-release

# Full clean and rebuild
rebuild: clean restore build

rebuild-release: clean restore build-release