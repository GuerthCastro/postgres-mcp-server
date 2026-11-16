using AwesomeAssertions;
using Mcp.PostgreSQL.Services;

namespace Mcp.PostgreSQL.Tests.Services;

public class QueryGuardTests
{
    [Theory]
    [InlineData("SELECT E'abc'")]
    [InlineData("select e'abc'")]
    [InlineData("SELECT E'\'' ; DROP TABLE x; SELECT '")]
    [InlineData("SELECT 1 WHERE name = E'\''")]
    public void Validate_ShouldRejectQuery_WhenQueryUsesEscapeString(string sql)
    {
        // Arrange
        var forbiddenKeywords = PostgreSQLQueries.ForbiddenKeywords;

        // Act
        var error = QueryGuard.Validate(sql, forbiddenKeywords, out _);

        // Assert
        error.Should().Be("Escape string constants (E'...') are not allowed.");
    }

    [Theory]
    [InlineData("SELECT $$abc$$")]
    [InlineData("SELECT $tag$abc$tag$")]
    [InlineData("SELECT $$ ' $$ ; DROP TABLE x; SELECT ' $$")]
    [InlineData("SELECT $_x1$ ; DROP TABLE x $_x1$")]
    public void Validate_ShouldRejectQuery_WhenQueryUsesDollarQuoting(string sql)
    {
        // Arrange
        var forbiddenKeywords = PostgreSQLQueries.ForbiddenKeywords;

        // Act
        var error = QueryGuard.Validate(sql, forbiddenKeywords, out _);

        // Assert
        error.Should().Be("Dollar-quoted strings ($$...$$ or $tag$...$tag$) are not allowed.");
    }

    [Theory]
    [InlineData("SELECT * FROM orders WHERE note = 'E''s'")]
    [InlineData("SELECT * FROM orders WHERE note = 'costs $$5'")]
    [InlineData("SELECT DATE '2026-01-01', TYPE'x'")]
    [InlineData("SELECT price$total$ FROM orders")]
    [InlineData("SELECT 1 -- uses $$ and E'")]
    [InlineData("SELECT 1 /* $tag$ */")]
    public void Validate_ShouldAcceptQuery_WhenEscapeOrDollarSyntaxAppearsOnlyInsideStringsCommentsOrIdentifiers(string sql)
    {
        // Arrange
        var forbiddenKeywords = PostgreSQLQueries.ForbiddenKeywords;

        // Act
        var error = QueryGuard.Validate(sql, forbiddenKeywords, out _);

        // Assert
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("SELECT * INTO x FROM y", "Query contains forbidden keyword: INTO. Only read-only queries are allowed.")]
    [InlineData("SELECT 1; DROP TABLE x", "Multiple statements are not allowed. Send a single query without ';'.")]
    [InlineData("SELECT pg_terminate_backend(1)", "Query contains forbidden keyword: PG_TERMINATE_BACKEND. Only read-only queries are allowed.")]
    [InlineData("DELETE FROM x", "Only read-only queries starting with SELECT or WITH are allowed.")]
    public void Validate_ShouldRejectQuery_WhenQueryIsNotReadOnly(string sql, string expectedError)
    {
        // Arrange
        var forbiddenKeywords = PostgreSQLQueries.ForbiddenKeywords;

        // Act
        var error = QueryGuard.Validate(sql, forbiddenKeywords, out _);

        // Assert
        error.Should().Be(expectedError);
    }

    [Fact]
    public void Validate_ShouldRemoveCommentsFromNormalizedSql_WhenQueryContainsComments()
    {
        // Arrange
        const string sql = "SELECT 1 -- DELETE; DROP\n/* INSERT; */ , '--kept' ;";

        // Act
        var error = QueryGuard.Validate(sql, PostgreSQLQueries.ForbiddenKeywords, out var normalizedSql);

        // Assert
        error.Should().BeNull();
        normalizedSql.Should().Be("SELECT 1  \n  , '--kept'");
    }
}
