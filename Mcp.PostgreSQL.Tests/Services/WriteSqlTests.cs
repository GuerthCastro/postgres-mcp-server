using AwesomeAssertions;
using Mcp.PostgreSQL.Services;

namespace Mcp.PostgreSQL.Tests.Services;

public class WriteSqlTests
{
    private static readonly WriteColumn Id = new("id", "integer", false, true);
    private static readonly WriteColumn Name = new("name", "text", false, false);
    private static readonly WriteColumn Tags = new("tags", "text[]", false, false);
    private static readonly WriteColumn Total = new("total", "numeric(10,2)", true, false);

    private static readonly WriteTable Items = new("public", "items", [Id, Name, Tags, Total], [Id], "primary key (id)");

    [Fact]
    public void Literal_ShouldEscapeQuotesAndCastToDeclaredType_WhenValueIsText()
    {
        // Arrange
        const string value = "it's";

        // Act
        var literal = WriteSql.Literal(Name, value);

        // Assert
        literal.Should().Be("'it''s'::text");
    }

    [Fact]
    public void Literal_ShouldReturnNull_WhenValueIsNull()
    {
        // Arrange
        string? value = null;

        // Act
        var literal = WriteSql.Literal(Tags, value);

        // Assert
        literal.Should().Be("NULL");
    }

    [Fact]
    public void InverseOfDelete_ShouldInsertEveryNonGeneratedColumnWithOverriding_WhenTableHasIdentityAlways()
    {
        // Arrange
        string?[] row = ["7", "it's", "{a,b}", "10.00"];

        // Act
        var inverse = WriteSql.InverseOfDelete(Items, [row]);

        // Assert
        inverse.Should().Be("INSERT INTO \"public\".\"items\" (\"id\", \"name\", \"tags\") OVERRIDING SYSTEM VALUE VALUES ('7'::integer, 'it''s'::text, '{a,b}'::text[]);");
    }

    [Fact]
    public void InverseOfUpdate_ShouldRestoreOnlyTargetColumnsByKey_WhenRowsWereUpdated()
    {
        // Arrange
        string?[] first = ["1", "old name", null, "1.00"];
        string?[] second = ["2", "other", "{x}", "2.00"];

        // Act
        var inverse = WriteSql.InverseOfUpdate(Items, [Name, Tags], [first, second]);

        // Assert
        inverse.Should().Be(
            "UPDATE \"public\".\"items\" SET \"name\" = 'old name'::text, \"tags\" = NULL WHERE \"id\" = '1'::integer;\n" +
            "UPDATE \"public\".\"items\" SET \"name\" = 'other'::text, \"tags\" = '{x}'::text[] WHERE \"id\" = '2'::integer;");
    }

    [Fact]
    public void InverseOfInsert_ShouldDeleteByInsertedKeys_WhenRowsWereInserted()
    {
        // Arrange
        string?[] first = ["101"];
        string?[] second = ["102"];

        // Act
        var inverse = WriteSql.InverseOfInsert(Items, [first, second]);

        // Assert
        inverse.Should().Be(
            "DELETE FROM \"public\".\"items\" WHERE \"id\" = '101'::integer;\n" +
            "DELETE FROM \"public\".\"items\" WHERE \"id\" = '102'::integer;");
    }

    [Fact]
    public void InverseOfUpdate_ShouldUseEveryKeyColumn_WhenKeyIsComposite()
    {
        // Arrange
        var tenant = new WriteColumn("tenant", "text", false, false);
        var code = new WriteColumn("code", "text", false, false);
        var label = new WriteColumn("label", "text", false, false);
        var table = new WriteTable("app", "codes", [tenant, code, label], [tenant, code], "unique index app.codes_key (tenant, code)");
        string?[] row = ["t1", "A", "alpha"];

        // Act
        var inverse = WriteSql.InverseOfUpdate(table, [label], [row]);

        // Assert
        inverse.Should().Be("UPDATE \"app\".\"codes\" SET \"label\" = 'alpha'::text WHERE \"tenant\" = 't1'::text AND \"code\" = 'A'::text;");
    }

    [Fact]
    public void BeforeImageQuery_ShouldSelectEveryColumnAsTextWithSameWhere_WhenStatementIsUpdate()
    {
        // Arrange
        var statement = new WriteStatement(WriteOperation.Update, "UPDATE items SET name = 'x' WHERE id < 3", "items", "id < 3", ["name"]);

        // Act
        var query = WriteSql.BeforeImageQuery(Items, statement, 51);

        // Assert
        query.Should().Be("SELECT \"id\"::text, \"name\"::text, \"tags\"::text, \"total\"::text FROM items WHERE id < 3 ORDER BY \"id\" LIMIT 51");
    }

    [Fact]
    public void InsertWithReturnedKeys_ShouldAppendReturningOfKeyColumns_WhenStatementIsInsert()
    {
        // Arrange
        var statement = new WriteStatement(WriteOperation.Insert, "INSERT INTO items (name) SELECT name FROM staging ORDER BY name LIMIT 2", "items", null, []);

        // Act
        var sql = WriteSql.InsertWithReturnedKeys(Items, statement);

        // Assert
        sql.Should().Be("INSERT INTO items (name) SELECT name FROM staging ORDER BY name LIMIT 2 RETURNING \"id\"::text");
    }
}
