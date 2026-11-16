namespace Mcp.PostgreSQL.Services;

public static class WriteSql
{
    public static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public static string Literal(WriteColumn column, string? value) =>
        value is null ? "NULL" : $"'{value.Replace("'", "''")}'::{column.TypeName}";

    public static string BeforeImageQuery(WriteTable table, WriteStatement statement, int limit)
    {
        var columns = string.Join(", ", table.Columns.Select(column => $"{Quote(column.Name)}::text"));
        var order = string.Join(", ", table.KeyColumns.Select(column => Quote(column.Name)));
        return $"SELECT {columns} FROM {statement.TableName} WHERE {statement.WhereClause} ORDER BY {order} LIMIT {limit}";
    }

    public static string InsertWithReturnedKeys(WriteTable table, WriteStatement statement)
    {
        var keys = string.Join(", ", table.KeyColumns.Select(column => $"{Quote(column.Name)}::text"));
        return $"{statement.Sql} RETURNING {keys}";
    }

    public static string InverseOfDelete(WriteTable table, IReadOnlyList<string?[]> beforeImage)
    {
        var columns = table.InsertableColumns;
        var columnList = string.Join(", ", columns.Select(column => Quote(column.Name)));
        var overriding = columns.Any(column => column.IsIdentityAlways) ? " OVERRIDING SYSTEM VALUE" : string.Empty;

        return string.Join("\n", beforeImage.Select(row =>
        {
            var values = string.Join(", ", columns.Select(column => Literal(column, ValueOf(table, row, column))));
            return $"INSERT INTO {table.QualifiedName} ({columnList}){overriding} VALUES ({values});";
        }));
    }

    public static string InverseOfUpdate(WriteTable table, IReadOnlyList<WriteColumn> targets, IReadOnlyList<string?[]> beforeImage) =>
        string.Join("\n", beforeImage.Select(row =>
        {
            var assignments = string.Join(", ", targets.Select(column => $"{Quote(column.Name)} = {Literal(column, ValueOf(table, row, column))}"));
            return $"UPDATE {table.QualifiedName} SET {assignments} WHERE {KeyPredicate(table, column => ValueOf(table, row, column))};";
        }));

    public static string InverseOfInsert(WriteTable table, IReadOnlyList<string?[]> insertedKeys) =>
        string.Join("\n", insertedKeys.Select(keys =>
            $"DELETE FROM {table.QualifiedName} WHERE {KeyPredicate(table, column => keys[IndexOf(table.KeyColumns, column)])};"));

    private static string KeyPredicate(WriteTable table, Func<WriteColumn, string?> valueOf) =>
        string.Join(" AND ", table.KeyColumns.Select(column => $"{Quote(column.Name)} = {Literal(column, valueOf(column))}"));

    private static string? ValueOf(WriteTable table, string?[] row, WriteColumn column) => row[IndexOf(table.Columns, column)];

    private static int IndexOf(IReadOnlyList<WriteColumn> columns, WriteColumn column)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index] == column)
                return index;
        }

        throw new InvalidOperationException($"Column {column.Name} is not part of the expected column list.");
    }
}
