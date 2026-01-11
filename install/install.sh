#!/bin/bash
#
# K42 Installation Script for Linux
#
# This script:
# 1. Builds K42 from source
# 2. Installs the k42 binary to /usr/local/bin
# 3. Sets up systemd service for auto-start
# 4. Creates necessary directories
#
# Requirements:
# - .NET 8 SDK
# - Docker installed and running
# - Root privileges for installation
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "================================"
echo "  K42 Installation"
echo "================================"
echo ""

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo -e "${YELLOW}Note: Installation requires root privileges.${NC}"
   echo "Run with: sudo $0"
   exit 1
fi

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK is not installed.${NC}"
    echo "Install .NET 8 SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Found .NET SDK: $DOTNET_VERSION"

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed.${NC}"
    echo "Install Docker from: https://docs.docker.com/engine/install/"
    exit 1
fi

echo "Found Docker: $(docker --version)"

# Check if Docker daemon is running
if ! docker info &> /dev/null; then
    echo -e "${RED}Error: Docker daemon is not running.${NC}"
    echo "Start Docker with: sudo systemctl start docker"
    exit 1
fi

echo "Docker daemon is running."
echo ""

# Build K42
echo "Building K42..."
cd "$PROJECT_ROOT"
dotnet publish src/K42/K42.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o ./publish

echo -e "${GREEN}Build complete.${NC}"
echo ""

# Install binary
echo "Installing k42 to /usr/local/bin..."
cp ./publish/k42 /usr/local/bin/k42
chmod +x /usr/local/bin/k42

echo -e "${GREEN}Binary installed.${NC}"

# Create directories
echo "Creating K42 directories..."
mkdir -p /var/lib/k42/registrations
mkdir -p /var/log/k42

echo -e "${GREEN}Directories created.${NC}"

# Install systemd service (optional)
if [ -d /etc/systemd/system ]; then
    echo "Installing systemd service..."
    cp "$SCRIPT_DIR/systemd/k42.service" /etc/systemd/system/k42.service
    
    # Don't enable by default - K42 doesn't need a daemon
    # Individual containers handle their own restarts via Docker
    
    echo -e "${GREEN}Systemd service installed (not enabled by default).${NC}"
    echo "  To enable: sudo systemctl enable k42"
fi

echo ""
echo "================================"
echo -e "${GREEN}  K42 Installation Complete${NC}"
echo "================================"
echo ""
echo "Usage:"
echo "  k42 run <script.k42>     Run a K42 script"
echo "  k42 list                 List all containers"
echo "  k42 status <name>        Show container status"
echo "  k42 stop <name>          Stop a container"
echo "  k42 logs <name>          View container logs"
echo "  k42 unregister <name>    Remove a container"
echo ""
echo "Example:"
echo "  k42 run ./examples/nginx.k42"
echo ""
