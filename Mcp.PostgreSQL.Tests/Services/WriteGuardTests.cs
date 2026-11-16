using AwesomeAssertions;
using Mcp.PostgreSQL.Services;

namespace Mcp.PostgreSQL.Tests.Services;

public class WriteGuardTests
{
    [Theory]
    [InlineData("UPDATE items SET price = 0", "UPDATE without WHERE is not allowed.")]
    [InlineData("UPDATE items SET note = 'WHERE id = 1'", "UPDATE without WHERE is not allowed.")]
    [InlineData("DELETE FROM items", "DELETE without WHERE is not allowed.")]
    [InlineData("DELETE FROM items -- WHERE id = 1", "DELETE without WHERE is not allowed.")]
    [InlineData("UPDATE items SET price = (SELECT max(price) FROM items WHERE id = 1)", "UPDATE without WHERE is not allowed.")]
    public void Analyze_ShouldRejectStatement_WhenUpdateOrDeleteHasNoTopLevelWhere(string sql, string expectedError)
    {
        // Arrange
        var statementSql = sql;

        // Act
        var error = WriteGuard.Analyze(statementSql, out _);

        // Assert
        error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("DROP TABLE items", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("TRUNCATE items", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("CREATE ROLE intruder LOGIN PASSWORD 'x'", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("ALTER ROLE app SUPERUSER", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("GRANT ALL ON items TO PUBLIC", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("SELECT * FROM items", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("DELETE FROM items WHERE id = 1; DROP TABLE items", "Multiple statements are not allowed. Send a single INSERT, UPDATE or DELETE without ';'.")]
    [InlineData("UPDATE items SET price = 1 WHERE id IN (SELECT id FROM items) AND pg_terminate_backend(1)", "Statement contains forbidden keyword: PG_TERMINATE_BACKEND. ExecuteWrite only accepts a single INSERT, UPDATE or DELETE.")]
    [InlineData("INSERT INTO items (name) VALUES ('x') RETURNING id", "RETURNING is not allowed. ExecuteWrite adds its own RETURNING clause to INSERT to capture the inserted keys.")]
    [InlineData("INSERT INTO items (id, name) VALUES (1, 'x') ON CONFLICT (id) DO NOTHING", "ON CONFLICT is not supported by ExecuteWrite.")]
    [InlineData("INSERT INTO items (id, name) VALUES (1, 'x') ON CONFLICT (id) WHERE id > 0 DO NOTHING", "ON CONFLICT is not supported by ExecuteWrite.")]
    [InlineData("UPDATE items SET price = 1 FROM prices WHERE items.id = prices.id", "Unsupported UPDATE form. Use: UPDATE <table> SET <column> = <value>[, ...] WHERE <condition> (no alias, FROM or joins).")]
    [InlineData("UPDATE items AS i SET price = 1 WHERE i.id = 1", "Unsupported UPDATE form. Use: UPDATE <table> SET <column> = <value>[, ...] WHERE <condition> (no alias, FROM or joins).")]
    [InlineData("DELETE FROM items USING prices WHERE items.id = prices.id", "Unsupported DELETE form. Use: DELETE FROM <table> WHERE <condition> (no alias, USING or joins).")]
    [InlineData("UPDATE items SET (price, note) = (1, 'x') WHERE id = 1", "Unsupported SET assignment. Assign each column separately: SET <column> = <value>[, ...]. Row-constructor assignments are not supported.")]
    [InlineData("INSERT INTO items (name) SELECT name FROM (DELETE FROM items WHERE id = 1) AS d", "Only one INSERT, UPDATE or DELETE is allowed. Nested or combined data-modifying statements are not supported.")]
    [InlineData("SELECT * INTO copy FROM items", "ExecuteWrite only accepts a single INSERT, UPDATE or DELETE statement.")]
    [InlineData("UPDATE items SET note = E'\\'' WHERE id = 1", "Escape string constants (E'...') are not allowed.")]
    [InlineData("UPDATE items SET note = $$x$$ WHERE id = 1", "Dollar-quoted strings ($$...$$ or $tag$...$tag$) are not allowed.")]
    public void Analyze_ShouldRejectStatement_WhenStatementIsNotASupportedSingleWrite(string sql, string expectedError)
    {
        // Arrange
        var statementSql = sql;

        // Act
        var error = WriteGuard.Analyze(statementSql, out var statement);

        // Assert
        error.Should().Be(expectedError);
        statement.Should().BeNull();
    }

    [Fact]
    public void Analyze_ShouldExtractTableWhereAndTargets_WhenUpdateIsValid()
    {
        // Arrange
        const string sql = "UPDATE public.\"Items\" SET price = price * 2, \"Note\" = 'a, b = c' WHERE id IN (SELECT id FROM t WHERE x = 'WHERE') -- comment";

        // Act
        var error = WriteGuard.Analyze(sql, out var statement);

        // Assert
        error.Should().BeNull();
        statement!.Operation.Should().Be(WriteOperation.Update);
        statement.TableName.Should().Be("public.\"Items\"");
        statement.SetTargets.Should().Equal("price", "Note");
        statement.WhereClause.Should().Be("id IN (SELECT id FROM t WHERE x = 'WHERE')");
        statement.Sql.Should().Be("UPDATE public.\"Items\" SET price = price * 2, \"Note\" = 'a, b = c' WHERE id IN (SELECT id FROM t WHERE x = 'WHERE')");
    }

    [Fact]
    public void Analyze_ShouldExtractTableAndWhere_WhenDeleteIsValid()
    {
        // Arrange
        const string sql = "delete from items where id between 1 and 3;";

        // Act
        var error = WriteGuard.Analyze(sql, out var statement);

        // Assert
        error.Should().BeNull();
        statement!.Operation.Should().Be(WriteOperation.Delete);
        statement.TableName.Should().Be("items");
        statement.WhereClause.Should().Be("id between 1 and 3");
    }

    [Theory]
    [InlineData("INSERT INTO items (name, price) VALUES ('a', 1), ('b', 2)")]
    [InlineData("INSERT INTO items(name) SELECT name FROM staging WHERE ready")]
    [InlineData("INSERT INTO logs OVERRIDING SYSTEM VALUE VALUES (10, 'x')")]
    [InlineData("INSERT INTO items DEFAULT VALUES")]
    public void Analyze_ShouldAcceptInsert_WhenInsertUsesSupportedForm(string sql)
    {
        // Arrange
        var statementSql = sql;

        // Act
        var error = WriteGuard.Analyze(statementSql, out var statement);

        // Assert
        error.Should().BeNull();
        statement!.Operation.Should().Be(WriteOperation.Insert);
        statement.WhereClause.Should().BeNull();
    }

    [Theory]
    [InlineData("price", "price")]
    [InlineData("Price", "price")]
    [InlineData("\"Price\"", "Price")]
    [InlineData("items.\"Price\"", "Price")]
    [InlineData("\"a\"\"b\"", "a\"b")]
    public void UnquoteIdentifier_ShouldFoldOrUnquote_WhenIdentifierIsQuotedOrNot(string identifier, string expected)
    {
        // Arrange
        var input = identifier;

        // Act
        var name = WriteGuard.UnquoteIdentifier(input);

        // Assert
        name.Should().Be(expected);
    }
}
