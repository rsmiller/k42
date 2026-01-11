# K42

**Run containers. Keep them running. Understand exactly what they do.**

K42 is a single-node container execution system. It exists as an explicit rejection of Kubernetes-style orchestration complexity. K stands for Kosmos, 42 is the meaning of all existance.

K42 (Kosmos42) is an independent container execution system and is not affiliated with, endorsed by, or compatible with Kubernetes or the Linux Foundation.

## Philosophy

K42 answers one question only:

> "How do I run a container, keep it running, and understand exactly what it is doing?"

K42 is **not**:
- A scheduler
- An orchestrator  
- A platform
- A control plane
- An ecosystem

K42 **is**:
- A containerized executable for YAML files

K42 **purpose**:
- To run a few containers, expose those containers by default, give persistent storage by default, and to ensure those containers restart when something occures of a system reboot happens.
- To create a container system written in C# so that businesses executives and .NET developers alike can extend this system for internal tooling.

## Quick Start

```bash
# Install
sudo ./install/install.sh

# Run a container
k42 run ./examples/nginx.k42

# Check status
k42 status nginx-hello

# View logs
k42 logs nginx-hello

# List all containers
k42 list

# Stop a container
k42 stop nginx-hello

# Remove completely
k42 unregister nginx-hello
```

## How It Works

### The File Model

K42 consumes a single executable script file containing YAML:

```bash
#!/usr/bin/env k42
# ---K42-START---
# name: my-service
# image: nginx:alpine
# container-port: 80
# host-port: 8080
# public-network: true
# storage-size: 1GB
# environment:
#   MY_VAR: my-value
# ---K42-END---
```

Or a plain YAML file:

```yaml
name: my-service
image: nginx:alpine
container-port: 80
host-port: 8080
```

**Rules:**
- The file is the sole source of truth
- No includes, no references, no secondary files
- Fully self-contained
- Comments allowed

### Execution Behavior

```bash
k42 run ./my-service.k42
```

- If container already exists and is running → do nothing
- If container exists but stopped → recreate from YAML
- If container doesn't exist → create from YAML

### One Container Per File

- One file = one container
- Impossible to scale
- Impossible to replicate
- Multiple files run multiple containers

## YAML Specification

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `name` | Yes | - | Container name (unique on host) |
| `image` | Yes | - | Container image to run |
| `container-port` | No | 80 | Port inside container |
| `host-port` | No | 80 | Port on host (auto-increments if taken) |
| `public-network` | No | true | `false` = bind to 127.0.0.1 only |
| `storage-size` | No | 1GB | Persistent storage (e.g., 500MB, 10GB) |
| `environment` | No | - | Key-value environment variables |
| `command` | No | - | Override container command |
| `workdir` | No | - | Working directory in container |

### YAML Rules

- Strictly validated
- Refused if invalid
- No interpolation
- No file references
- No base64 encoding
- All secrets are plain text values (by design)

```yaml
# This is correct
environment:
  DATABASE_PASSWORD: supersecret123
  API_KEY: sk-abcdef123456

# NOT this (no external references)
environment:
  DATABASE_PASSWORD: ${ENV_VAR}  # INVALID
  API_KEY: file:/secrets/key     # INVALID
```

## Networking

### Host Networking

K42 uses host networking. Always.

- Containers bind directly to the host
- No overlays
- No NAT abstractions
- No virtual services

### Port Behavior

- Default: container port 80 → host port 80
- If port 80 is taken: tries 81, 82, 83...
- Override with `host-port` in YAML

### Public vs Private

```yaml
# Public (default) - binds to 0.0.0.0
public-network: true

# Private - binds to 127.0.0.1 only
public-network: false
```

Use `public-network: false` for databases and internal services.

## Storage

Every container automatically receives persistent storage:

- Default: 1GB
- Mounted at `/data` inside the container
- Survives container restarts
- Removed on `k42 unregister`

```yaml
storage-size: 10GB
```

**Implementation:** Docker volumes named `k42-{name}-data`

## Restart & Failure

Containers always restart on failure:

1. Maximum 5 retry attempts
2. Each retry waits longer than the previous
3. No exponential chaos
4. Failures are visible and obvious

**Philosophy:** "It keeps trying until a human notices."

### No Health Checks

- No probes
- No liveness/readiness
- Only process exit status matters

## Commands

| Command | Description |
|---------|-------------|
| `k42 run <file>` | Execute a K42 script |
| `k42 status <name>` | Show container status |
| `k42 list` | List all K42 containers |
| `k42 logs <name>` | View container logs |
| `k42 stop <name>` | Stop a container |
| `k42 unregister <name>` | Remove container and data |

### Status Output

```
Container: my-service
────────────────────────────────────────
Status: RUNNING ✓
Image: nginx:alpine
Container ID: a1b2c3d4e5f6
Port: 8080
Started: 2025-01-10 14:30:00 UTC
Uptime: 2h 15m
Restarts: 0

Registration:
  Script: /home/user/my-service.k42
  Registered: 2025-01-10 14:30:00 UTC
```

## Logging

Logs go to:
- **Linux:** syslog/journald + `/var/log/k42.log`
- **Windows:** Event Log + `C:\ProgramData\K42\logs\`
- **Console:** Always (for debugging)

View container logs:
```bash
k42 logs my-service
k42 logs my-service --tail 50
```

For live logs:
```bash
docker logs -f k42-my-service
```

## Installation

### Requirements

- Linux (Ubuntu 20.04+, Debian 11+, RHEL 8+, or similar)
- .NET 10 Runtime or SDK
- Docker installed and running

---

### Complete Bare-Metal Linux Installation

This guide walks through installing K42 on a fresh Linux server.

#### Step 1: Install Docker

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin

# Start and enable Docker
sudo systemctl start docker
sudo systemctl enable docker

# Verify Docker works
sudo docker run hello-world
```

For RHEL/Fedora:
```bash
sudo dnf install -y dnf-plugins-core
sudo dnf config-manager --add-repo https://download.docker.com/linux/fedora/docker-ce.repo
sudo dnf install -y docker-ce docker-ce-cli containerd.io
sudo systemctl start docker
sudo systemctl enable docker
```

#### Step 2: Install .NET 10

```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet

# Verify
dotnet --version
```

Alternative (Ubuntu):
```bash
sudo apt install -y dotnet-sdk-10.0
```

#### Step 3: Install Git

```bash
# Ubuntu/Debian
sudo apt install -y git

# RHEL/Fedora
sudo dnf install -y git

# Verify
git --version
```

#### Step 4: Clone and Build K42

```bash
# Clone the repository
git clone https://github.com/rsmiller/k42.git
cd k42

# Build
dotnet publish src/K42/K42.csproj -c Release -r linux-x64 --self-contained -o ./publish

# Verify build
./publish/k42 --help
```

#### Step 5: Install K42

```bash
# Copy binary to system path
sudo cp ./publish/k42 /usr/local/bin/k42
sudo chmod +x /usr/local/bin/k42

# Create K42 directories
sudo mkdir -p /var/lib/k42/registrations
sudo mkdir -p /var/log/k42

# Verify installation
k42 --help
```

#### Step 6: Install Systemd Service (Optional)

This enables K42 containers to auto-restart on boot:

```bash
# Copy service file
sudo cp ./install/systemd/k42.service /etc/systemd/system/k42.service

# Reload systemd
sudo systemctl daemon-reload

# Enable on boot (optional)
sudo systemctl enable k42

# Start now (optional)
sudo systemctl start k42

# Check status
sudo systemctl status k42
```

#### Step 7: Run Your First Container

```bash
# Run the nginx example
k42 run ./examples/nginx.k42

# Check it's running
k42 list

# View status
k42 status nginx-hello

# Access it
curl http://localhost:8080

# View logs
k42 logs nginx-hello
```

#### Step 8: Verify Auto-Restart (Optional)

```bash
# Reboot the server
sudo reboot

# After reboot, check containers are running
k42 list
```

---

### Quick Install (If Prerequisites Met)

If Docker and .NET 10 are already installed:

```bash
git clone https://github.com/rsmiller/k42.git
cd k42
sudo ./install/install.sh
```

### Manual Build

```bash
dotnet publish src/K42/K42.csproj -c Release -r linux-x64 --self-contained
sudo cp publish/k42 /usr/local/bin/
```

### Uninstall

```bash
# Stop all K42 containers
k42 list | grep -v NAME | awk '{print $1}' | xargs -I {} k42 unregister {} -f

# Remove binary
sudo rm /usr/local/bin/k42

# Remove service
sudo systemctl stop k42
sudo systemctl disable k42
sudo rm /etc/systemd/system/k42.service
sudo systemctl daemon-reload

# Remove data (WARNING: deletes all container data)
sudo rm -rf /var/lib/k42
sudo rm -rf /var/log/k42
```

## How It Fails

1. **Invalid YAML:** Refuses to run. Error message shows exactly what's wrong.
2. **Image pull fails:** Error with clear message. No container created.
3. **Port conflict:** Auto-increments to next available port.
4. **Container crashes:** Restarts up to 5 times with increasing delays.
5. **Docker not running:** Clear error message on any command.

## What K42 Does NOT Do

Explicitly forbidden:

- ❌ Clustering / multiple nodes
- ❌ Pods / replicas / scaling
- ❌ Services / ingress controllers
- ❌ Controllers / reconciliation loops
- ❌ CRDs / operators
- ❌ Sidecars / init containers
- ❌ "Desired vs actual state" model
- ❌ Health checks / probes
- ❌ Resource limits (host OS handles this)
- ❌ Secrets management
- ❌ Web dashboards
- ❌ Remote API
- ❌ Helm / templating
- ❌ Cloud provider integrations

## Examples

### Web Server

```yaml
name: web
image: nginx:alpine
host-port: 80
```

### Database

```yaml
name: postgres
image: postgres:16-alpine
container-port: 5432
host-port: 5432
public-network: false
storage-size: 20GB
environment:
  POSTGRES_USER: app
  POSTGRES_PASSWORD: secretpassword
  POSTGRES_DB: myapp
  PGDATA: /data/pgdata
```

### Full Stack

```bash
# Start database
k42 run ./postgres.k42

# Start cache
k42 run ./redis.k42

# Start application
k42 run ./my-app.k42

# Check everything
k42 list
```

## Shutdown Behavior

On SIGTERM / SIGINT:

1. K42 attempts graceful container shutdown
2. Containers are stopped one by one
3. Default timeout: 10 seconds per container
4. Then K42 exits

## Auto-Start on Boot

K42 containers auto-restart via Docker's restart policy. No separate daemon needed.

To run K42 as a system service (optional):

```bash
sudo systemctl enable k42
sudo systemctl start k42
```

## Project Structure

```
K42/
├── src/K42/
│   ├── Commands/          # CLI commands
│   ├── Logging/           # System logging
│   ├── Runtime/           # Container runtime
│   ├── Schema/            # YAML parsing
│   └── Program.cs         # Entry point
├── examples/              # Example scripts
├── install/               # Installation scripts
└── README.md
```

## FAQ

**Q: Why no health checks?**
A: Process exit status is the only truth. If a process is running, it's "healthy." If it exits, it failed. Anything more is speculation.

**Q: Why plain text secrets?**
A: Secrets management is a separate problem. K42 doesn't pretend to solve it. Use proper secret management (Vault, etc.) and inject values into your YAML.

**Q: Why no scaling?**
A: If you need scaling, you need orchestration. Use Kubernetes. K42 is for the 90% of services that just need to run on one machine.

**Q: Why no resource limits?**
A: The host OS is responsible. Use cgroups, systemd resource control, or whatever your OS provides. K42 stays out of it.

**Q: Can I run multiple services?**
A: Yes. One file per service. They all run on the same host until resources are exhausted.

**Q: What if two services want the same port?**
A: First one gets it. Second auto-increments. Or specify different ports in YAML.

## License

MIT

---

*K42 is boring on purpose. Boring means reliable. Reliable means humane.*
