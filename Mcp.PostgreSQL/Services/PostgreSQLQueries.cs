namespace Mcp.PostgreSQL.Services;

public static class PostgreSQLQueries
{
    public const string ListDatabases = """
        SELECT datname
        FROM pg_database
        WHERE datistemplate = false
        ORDER BY datname
        """;

    public const string ListSchemas = """
        SELECT schema_name
        FROM information_schema.schemata
        WHERE schema_name NOT IN ('pg_catalog', 'information_schema')
        ORDER BY schema_name
        """;

    public const string ListTables = """
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = @schema
          AND table_type = 'BASE TABLE'
        ORDER BY table_name
        """;

    public const string DescribeTable = """
        SELECT
            c.column_name,
            c.data_type,
            c.is_nullable,
            c.column_default,
            CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_primary_key,
            CASE WHEN fk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END AS is_foreign_key,
            fk.foreign_table_name,
            fk.foreign_column_name
        FROM information_schema.columns c
        LEFT JOIN (
            SELECT ku.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage ku
                ON tc.constraint_name = ku.constraint_name
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND tc.table_schema = @schema
              AND tc.table_name = @table
        ) pk ON c.column_name = pk.column_name
        LEFT JOIN (
            SELECT
                ku.column_name,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage ku
                ON tc.constraint_name = ku.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON tc.constraint_name = ccu.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @schema
              AND tc.table_name = @table
        ) fk ON c.column_name = fk.column_name
        WHERE c.table_schema = @schema
          AND c.table_name = @table
        ORDER BY c.ordinal_position
        """;

    public const string ExecuteQuery = """
        {0}
        """;

    public static readonly string[] ForbiddenKeywords = ["INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "GRANT", "REVOKE"];

    public const int MaxOutputCharacters = 50_000;
}
