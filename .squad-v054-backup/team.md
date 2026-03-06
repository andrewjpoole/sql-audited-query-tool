# Squad Team

## Project Context

**Project:** SQL Audited Query Tool
**User:** Andrew
**Description:** An application that allows users to query a SQL database in readonly mode for incident investigation. All queries are posted to a GitHub issue for audit purposes. Includes a local LLM with SQL Server MCP to assist with queries (strictly without exposing database data). A chat interface helps users with incident investigation and creating queries — both for finding data and suggesting fixes (fix queries must be run in a separate tool). The tool only has a readonly connection. The local LLM may also access local code repositories to discover EF Core database contexts and code.

**Stack:** .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
**Created:** 2026-02-22

## Members

| Name | Role | Model | Badge |
|------|------|-------|-------|
| Gandalf | Lead | auto | 🏗️ Lead |
| Samwise | Backend Dev | claude-sonnet-4.5 | 🔧 Backend |
| Radagast | LLM Engineer | claude-sonnet-4.5 | 📊 LLM |
| Legolas | Frontend Dev | claude-sonnet-4.5 | ⚛️ Frontend |
| Faramir | Security | claude-sonnet-4.5 | 🔒 Security |
| Scribe | Scribe | claude-haiku-4.5 | 📋 Scribe |
| Ralph | Work Monitor | — | 🔄 Monitor |
