# K42 YAML Schema Reference

This document defines the complete K42 YAML specification.

## Schema

```yaml
# Required: Container name
# Must be unique on this host
# Allowed: lowercase letters, numbers, hyphens, underscores
# Must start with a letter or number
name: my-container

# Required: Container image
# Always pulled on first run
# Use explicit tags (no :latest in production)
image: nginx:1.25-alpine

# Optional: Port inside the container
# Default: 80
# Set to 0 to disable port binding
container-port: 80

# Optional: Port on the host
# Default: 80
# Auto-increments if taken (81, 82, ...)
# Set to 0 to disable port binding
host-port: 80

# Optional: Public network access
# Default: true (binds to 0.0.0.0)
# Set to false for 127.0.0.1 only
public-network: true

# Optional: Persistent storage size
# Default: 1GB
# Format: NMB or NGB (e.g., 500MB, 10GB)
storage-size: 1GB

# Optional: Environment variables
# Plain text values only
# No interpolation, no references
environment:
  VAR_NAME: value
  ANOTHER_VAR: another value

# Optional: Override container command
# List of strings
command:
  - /bin/sh
  - -c
  - echo hello

# Optional: Working directory in container
workdir: /app
```

## Validation Rules

1. **name**: Required. Must match `^[a-z0-9][a-z0-9_-]*$`
2. **image**: Required. Must not be empty.
3. **container-port**: 0-65535
4. **host-port**: 0-65535
5. **storage-size**: Must match `^\d+(MB|GB)$`
6. **environment**: Keys must be valid environment variable names
7. **command**: If present, must be a list of strings

## Invalid YAML Behavior

If YAML is invalid:
- K42 refuses to run
- Error message shows exactly what's wrong
- No best-effort execution
- No silent correction

## Example: Minimal

```yaml
name: hello
image: hello-world
```

## Example: Full

```yaml
name: production-api
image: myregistry.com/api:v2.1.0
container-port: 3000
host-port: 443
public-network: true
storage-size: 5GB
workdir: /app
environment:
  NODE_ENV: production
  DATABASE_URL: postgresql://user:pass@localhost:5432/db
  REDIS_URL: redis://localhost:6379
  API_SECRET: supersecretkey123
  LOG_LEVEL: warn
  MAX_WORKERS: "4"
command:
  - node
  - server.js
```

## What's NOT in the Schema

K42 explicitly does not support:

- `replicas` - One container per file
- `resources` / `limits` - Host OS responsibility  
- `healthCheck` / `livenessProbe` - Only exit status matters
- `volumes` / `mounts` - Auto-managed at /data
- `networks` - Host networking only
- `secrets` - Plain text in environment
- `depends_on` - No dependency management
- `restart` - Always restarts (5 attempts max)
- `user` - Always runs as root
- `labels` - K42 manages its own labels
- `annotations` - Not Kubernetes
