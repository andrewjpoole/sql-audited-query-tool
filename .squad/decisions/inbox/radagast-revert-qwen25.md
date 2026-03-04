# Decision: Revert LLM model to qwen2.5:7b

**Date:** 2025-07-17
**Author:** Radagast (LLM Engineer)
**Requested by:** Andrew

## Context

The qwen3:14b model was too large to fit in GPU VRAM, causing it to fall back to CPU inference which was unacceptably slow. The previous model, qwen2.5:7b, ran well on the available GPU.

## Decision

Reverted the LLM model from `qwen3:14b` back to `qwen2.5:7b` across both configuration points:

- **appsettings.json**: Model → `qwen2.5:7b`, ChatTimeoutSeconds → `120` (appropriate for GPU-speed inference)
- **AppHost.cs**: AddModel → `qwen2.5:7b` (kept `.WithImageTag("0.17.4")`)

## Rationale

GPU inference with a model that fits in VRAM is dramatically faster than CPU inference with a larger model. The 7B parameter model fits comfortably in GPU memory and delivers good-enough quality for SQL generation tasks at much lower latency.
