# postgres-mcp-server

A read-only MCP server for PostgreSQL databases, built with .NET 10 and the official
[ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

Connect any MCP-compatible AI agent to your PostgreSQL server and let it explore
databases, schemas, tables, and execute read-only queries — all through natural language.

---

## Features

- Connect to any PostgreSQL server with host, port, credentials
- List all databases on the server
- List schemas within a database
- List tables within a schema
- Describe table structure: columns, types, nullability, primary keys, foreign keys
- Execute read-only SELECT queries
- Rejects any non-SELECT or destructive SQL (INSERT, UPDATE, DELETE, DROP, etc.)
- Runs as a lightweight Docker container
- Compatible with Claude Desktop, Claude Code, and any MCP-compatible client

---

## Quick Start

```bash
git clone https://github.com/GuerthCastro/postgres-mcp-server.git
cd postgres-mcp-server
docker compose up -d
```

The server starts on port `3100` by default.

---

## Configuration

No configuration needed at the container level. The connection is established at runtime
via the `connect` tool. The AI agent handles the session for the duration of the conversation.

To change the port, edit `docker-compose.yml`:

```yaml
services:
  postgres-mcp:
    build: .
    ports:
      - "YOUR_PORT:80"
    restart: unless-stopped
```

---

## MCP Tools

| Tool | Description |
|---|---|
| `Connect` | Connects to a PostgreSQL server |
| `ListDatabases` | Lists all databases on the connected server |
| `ListSchemas` | Lists all schemas in a database |
| `ListTables` | Lists all tables in a schema |
| `DescribeTable` | Describes columns, types, PKs and FKs of a table |
| `ExecuteQuery` | Executes a read-only SELECT query |

---

## Claude Desktop Setup

Add this to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "postgresqlServer": {
      "url": "http://localhost:3100/mcp"
    }
  }
}
```

---

## Claude Code Setup

```bash
claude mcp add postgresqlServer http://localhost:3100/mcp
```

---

## Example Conversation

```
You: Connect to my database server at 192.168.1.100, user postgres, password secret

Claude: Connected to PostgreSQL 16.2 at 192.168.1.100:5432

You: What databases are available?

Claude: Found 3 databases:
- myapp
- registrodocente
- postgres

You: Show me the tables in the public schema of myapp

Claude: Found 5 tables in public:
- users
- orders
- products
- categories
- audit_log

You: Describe the users table

Claude: Table: public.users
------------------------------------------------------------------------
Column          Type            Nullable   PK    FK    References
------------------------------------------------------------------------
id              bigint          NO         YES   NO
email           varchar         NO         NO    NO
name            varchar         YES        NO    NO
created_on      timestamptz     NO         NO    NO
```

---

## Security

- **Read-only by design.** Only SELECT queries are accepted.
- **No credentials stored.** Connection parameters are held in memory only for the duration
  of the running process and are never written to disk or logs.
- **Local network recommended.** Deploy on your internal network and avoid exposing
  the container port to the public internet without additional authentication.

---

## Requirements

- Docker
- Any MCP-compatible client (Claude Desktop, Claude Code, Cursor, Windsurf, etc.)
- A running PostgreSQL server accessible from the container

---

## Stack

- [.NET 10 LTS](https://dotnet.microsoft.com)
- [ModelContextProtocol.AspNetCore 1.3.0](https://github.com/modelcontextprotocol/csharp-sdk)
- [Npgsql](https://npgsql.org)

---

## License

MIT

---

*Built by [Guerth Castro](https://github.com/GuerthCastro)*