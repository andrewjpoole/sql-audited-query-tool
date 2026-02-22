# SQL Audited Query Tool

A readonly SQL database query application designed for incident investigation, with a full GitHub issue audit trail.

## Features

- **Readonly SQL Queries** — Execute SELECT queries against SQL Server databases with enforced read-only access
- **GitHub Issue Audit Trail** — Every query execution is logged as a GitHub issue for compliance and traceability
- **Local LLM Query Assistance** — AI-powered query suggestions via a local LLM with SQL Server MCP integration (no data exposure to the LLM)
- **Chat Interface** — Conversational UI for building and executing queries
- **EF Core Code Discovery** — Optional discovery of existing EF Core models and mappings to aid query construction

## Architecture

```
src/
  SqlAuditedQueryTool.Core        — Domain models, interfaces, shared types
  SqlAuditedQueryTool.Database    — SQL Server readonly connection, query execution, EF Core contexts
  SqlAuditedQueryTool.Audit       — GitHub issue audit logging
  SqlAuditedQueryTool.Llm         — Local LLM integration, SQL Server MCP client, query generation
  SqlAuditedQueryTool.App         — Main application with chat UI

tests/
  SqlAuditedQueryTool.Core.Tests
  SqlAuditedQueryTool.Database.Tests
  SqlAuditedQueryTool.Audit.Tests
  SqlAuditedQueryTool.Llm.Tests
```

## Prerequisites

- .NET 10.0 SDK or later
- Docker Desktop with WSL2 backend
- SQL Server instance (read-only access)
- GitHub token (for audit logging)
- NVIDIA GPU (recommended for local LLM — see GPU Setup below)

## Getting Started

```bash
dotnet build
dotnet test
dotnet run --project src/SqlAuditedQueryTool.App
```

## GPU Setup (NVIDIA + Docker + WSL2)

The local LLM (Ollama with `qwen2.5-coder:7b`) runs significantly faster with GPU acceleration. This requires NVIDIA GPU passthrough from Windows → WSL2 → Docker.

### Requirements

- NVIDIA GPU with 8GB+ VRAM (e.g., GeForce RTX 4060/4070/3060 or better)
- Latest NVIDIA Game Ready or Studio driver installed on **Windows** (not inside WSL)
- Docker Desktop with WSL2 backend enabled

### Step 1: Update WSL

The WSL kernel must support GPU paravirtualization. Run from **PowerShell (Admin)**:

```powershell
wsl --update
wsl --shutdown
```

Verify the GPU is visible inside WSL:

```bash
# In a WSL terminal (e.g., Ubuntu)
nvidia-smi
```

You should see your GPU listed. If you see "GPU access blocked by the operating system", your WSL version is too old — re-run `wsl --update`.

### Step 2: Install NVIDIA Container Toolkit (in WSL)

Open your WSL2 distro (Ubuntu) and run:

```bash
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey \
  | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list \
  | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' \
  | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
```

### Step 3: Restart Docker Desktop

Restart Docker Desktop from the Windows system tray (right-click → Restart).

### Step 4: Verify

```bash
docker run --rm --gpus all nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi
```

You should see your GPU with available memory. The `qwen2.5-coder:7b` model uses ~4.5GB VRAM, fitting comfortably in an 8GB card.

### Troubleshooting

| Symptom | Fix |
|---------|-----|
| `nvidia-smi` in WSL: "GPU access blocked by the operating system" | Run `wsl --update && wsl --shutdown`, then retry |
| `nvidia-container-cli: WSL environment detected but no adapters were found` | WSL kernel too old — update WSL (see Step 1) |
| Docker `--gpus all` fails with "could not select device driver" | NVIDIA Container Toolkit not installed (see Step 2) or Docker not restarted (Step 3) |
| GPU visible but model runs on CPU | Ensure `.WithGPUSupport()` is present in `AppHost.cs` on the Ollama resource |

## Security

- All database connections are **read-only** — no INSERT, UPDATE, DELETE, or DDL operations permitted
- The local LLM never receives actual database data — only schema metadata for query generation
- All query executions are audited to GitHub issues with full context
