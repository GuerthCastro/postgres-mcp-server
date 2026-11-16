using Npgsql;
using NpgsqlTypes;

namespace Mcp.PostgreSQL.Services;

public sealed record WriteColumn(string Name, string TypeName, bool IsGenerated, bool IsIdentityAlways);

public sealed record WriteTable(
    string Schema,
    string Name,
    IReadOnlyList<WriteColumn> Columns,
    IReadOnlyList<WriteColumn> KeyColumns,
    string KeyDescription)
{
    public string QualifiedName => $"{WriteSql.Quote(Schema)}.{WriteSql.Quote(Name)}";
    public string DisplayName => $"{Schema}.{Name}";
    public IReadOnlyList<WriteColumn> InsertableColumns => Columns.Where(column => !column.IsGenerated).ToList();
}

public static class WriteCatalog
{
    private const string ResolveTableSql = """
        SELECT c.oid, n.nspname, c.relname, c.relkind::text
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.oid = to_regclass(@table)
        """;

    private const string ColumnsSql = """
        SELECT a.attname, format_type(a.atttypid, a.atttypmod), a.attgenerated <> '', a.attidentity = 'a'
        FROM pg_attribute a
        WHERE a.attrelid = @oid AND a.attnum > 0 AND NOT a.attisdropped
        ORDER BY a.attnum
        """;

    private const string UniqueKeySql = """
        SELECT i.indisprimary, i.indexrelid::regclass::text, array_agg(a.attname ORDER BY k.ordinality)
        FROM pg_index i
        CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ordinality)
        JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum
        WHERE i.indrelid = @oid
          AND i.indisunique
          AND i.indisvalid
          AND i.indpred IS NULL
          AND i.indexprs IS NULL
          AND k.ordinality <= i.indnkeyatts
        GROUP BY i.indexrelid, i.indisprimary
        HAVING bool_and(a.attnotnull)
        ORDER BY i.indisprimary DESC, count(*), i.indexrelid::regclass::text
        LIMIT 1
        """;

    public static async Task<(WriteTable? Table, string? Error)> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName)
    {
        uint oid;
        string schema;
        string name;

        await using (var command = new NpgsqlCommand(ResolveTableSql, connection, transaction))
        {
            command.Parameters.AddWithValue("table", tableName);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return (null, $"Table {tableName} was not found in the target database.");

            if (reader.GetString(3) is not ("r" or "p"))
                return (null, $"{tableName} is not a table. ExecuteWrite only modifies tables.");

            oid = reader.GetFieldValue<uint>(0);
            schema = reader.GetString(1);
            name = reader.GetString(2);
        }

        var columns = new List<WriteColumn>();
        await using (var command = new NpgsqlCommand(ColumnsSql, connection, transaction))
        {
            command.Parameters.AddWithValue("oid", NpgsqlDbType.Oid, oid);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                columns.Add(new WriteColumn(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)));
        }

        await using (var command = new NpgsqlCommand(UniqueKeySql, connection, transaction))
        {
            command.Parameters.AddWithValue("oid", NpgsqlDbType.Oid, oid);
            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return (null, $"Table {schema}.{name} has no primary key and no unique index on NOT NULL columns, so the write could not be reverted reliably. Writes to this table are rejected.");

            var isPrimaryKey = reader.GetBoolean(0);
            var indexName = reader.GetString(1);
            var keyNames = reader.GetFieldValue<string[]>(2);
            var keyColumns = keyNames.Select(keyName => columns.Single(column => column.Name == keyName)).ToList();
            var description = isPrimaryKey
                ? $"primary key ({string.Join(", ", keyNames)})"
                : $"unique index {indexName} ({string.Join(", ", keyNames)})";

            return (new WriteTable(schema, name, columns, keyColumns, description), null);
        }
    }
}
