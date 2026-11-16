# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [0.2.0] - 2026-11-16

### Added

- Bearer token authentication on `/mcp` through the required `MCP_API_TOKEN` environment variable.
- `/health` endpoint (no authentication) for container and monitoring checks.
- `Disconnect` MCP tool.
- Configurable limits: `MCP_COMMAND_TIMEOUT_SECONDS`, `MCP_PAGE_MAX_ROWS` (default 200), `MCP_PAGE_MAX_CHARACTERS` (default 20000), `MCP_CONNECTION_IDLE_MINUTES`.
- `ExecuteQuery` paging: optional `offset` parameter, and every response starts with `rows X-Y · nextOffset: N · hasMore: true|false`. `offset > 0` requires an `ORDER BY` in the outer query. A row larger than `MCP_PAGE_MAX_CHARACTERS` is returned truncated with a marker and `nextOffset` moves past it. Each page re-executes the query, so pages are not consistent if the data changes between calls.
- `ExecuteQuery` accepts queries starting with `WITH` (CTEs).
- `Mcp.PostgreSQL.Tests` project with unit tests for the query guard.

### Changed

- **Breaking:** `Connect` returns a `connectionId` and every other tool requires it. Connections are isolated per client instead of being shared by all clients of the process.
- Calling a tool without `connectionId` returns the same "Unknown or expired connectionId" message as an invalid one, instead of a generic SDK error.
- `ExecuteQuery` runs inside a `READ ONLY` transaction that is always rolled back.
- `ExecuteQuery` stops reading once the page is full and cancels the rest of the result set. The SQL is never rewritten for paging.
- Keyword guard ignores string literals and quoted identifiers, rejects multiple statements, and blocks `INTO`, `COPY`, `CALL`, `DO` and functions with side effects such as `pg_terminate_backend` and `set_config`.
- Keyword guard removes `--` and `/* */` comments before validating; the query sent to the server is the same text without comments.
- Failures while cancelling a truncated query, closing its reader or rolling back are logged instead of being ignored (exception type and message only, no SQL or credentials). The expected cancellation error after truncating a result is logged as debug; anything else as a warning.

### Security

- Fixed: any client could run queries with the credentials of the last client that called `Connect`.
- Fixed: `SELECT ... INTO` could create tables despite the read-only guard.
- `Connect` rejects servers where `standard_conforming_strings` is `off`, because backslashes in plain string literals could then hide additional statements from the guard.
- Keyword guard rejects escape string constants (`E'...'`) and dollar-quoted strings (`$$...$$`, `$tag$...$tag$`), which could hide a `;` and a second statement from the guard.

## [0.1.0] - 2025-05-01

### Added

- `Connect` MCP tool — connects to a PostgreSQL server by host, port, username, password, and optional default database.
- `ListDatabases` MCP tool — lists all non-template databases on the connected server.
- `ListSchemas` MCP tool — lists all user-defined schemas in a given database.
- `ListTables` MCP tool — lists all base tables in a given schema.
- `DescribeTable` MCP tool — describes column names, data types, nullability, primary keys, and foreign key references.
- `ExecuteQuery` MCP tool — executes read-only `SELECT` queries with output truncation at 50,000 characters.
- Keyword-based SQL guard: rejects queries containing `INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, `ALTER`, `CREATE`, `GRANT`, or `REVOKE`.
- `DatabaseService` singleton managing in-memory connection configuration.
- `PostgreSQLQueries` static class centralizing all SQL constants and security policy constants.
- `ServerConfiguration` immutable record holding resolved connection parameters.
- ASP.NET Core bootstrap with MCP HTTP transport exposed at `/mcp`.
- Docker multi-stage build targeting `mcr.microsoft.com/dotnet/aspnet:10.0`.
- `docker-compose.yml` exposing port `3100`.

[Unreleased]: https://github.com/GuerthCastro/postgres-mcp-server/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/GuerthCastro/postgres-mcp-server/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/GuerthCastro/postgres-mcp-server/releases/tag/v0.1.0
