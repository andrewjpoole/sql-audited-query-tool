# Routing Rules

## Domain Routing

| Domain | Primary | Secondary | Keywords |
|--------|---------|-----------|----------|
| Architecture, scope, decisions | Gandalf | — | architecture, design, scope, decision, review |
| Database, API, audit logging, backend services | Samwise | Gandalf | database, sql, api, audit, backend, query, connection, ef core |
| Local LLM, MCP integration, query generation | Radagast | Samwise | llm, mcp, model, prompt, query generation, ollama, ai |
| Chat UI, frontend, user interface | Legolas | Radagast | ui, chat, frontend, interface, display, component |
| Security, readonly enforcement, data privacy | Faramir | Gandalf | security, readonly, privacy, audit, compliance, data exposure |

## Code Review Routing

| Reviewer | Reviews For | Gate |
|----------|------------|------|
| Gandalf | All agents | Architecture, scope alignment |
| Faramir | Samwise, Radagast | Security, data exposure, readonly enforcement |

## Escalation

If ambiguous, route to Gandalf. Gandalf delegates or handles directly.
