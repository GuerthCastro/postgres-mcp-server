using System.Text;
using System.Text.RegularExpressions;

namespace Mcp.PostgreSQL.Services;

public static partial class QueryGuard
{
    public static string? Validate(string sql, IReadOnlyCollection<string> forbiddenKeywords, out string normalizedSql)
    {
        normalizedSql = string.Empty;
        var tokens = SqlToken().Matches(sql ?? string.Empty);

        if (tokens.Any(token => token.Groups["escapeString"].Success))
            return "Escape string constants (E'...') are not allowed.";

        if (tokens.Any(token => token.Groups["dollarQuote"].Success))
            return "Dollar-quoted strings ($$...$$ or $tag$...$tag$) are not allowed.";

        var sqlWithoutComments = SqlToken().Replace(sql ?? string.Empty, match => match.Groups["comment"].Success ? " " : match.Value);
        normalizedSql = sqlWithoutComments.Trim().TrimEnd(';').Trim();

        if (normalizedSql.Length == 0)
            return "Query is empty.";

        var upperSqlWithoutQuotedText = SqlToken().Replace(normalizedSql, "''").ToUpperInvariant();

        if (!upperSqlWithoutQuotedText.StartsWith("SELECT") && !upperSqlWithoutQuotedText.StartsWith("WITH"))
            return "Only read-only queries starting with SELECT or WITH are allowed.";

        if (upperSqlWithoutQuotedText.Contains(';'))
            return "Multiple statements are not allowed. Send a single query without ';'.";

        var forbidden = forbiddenKeywords.FirstOrDefault(keyword => Regex.IsMatch(upperSqlWithoutQuotedText, $@"\b{Regex.Escape(keyword)}\b"));

        return forbidden is null
            ? null
            : $"Query contains forbidden keyword: {forbidden}. Only read-only queries are allowed.";
    }

    public static string? ValidatePaging(string normalizedSql, int offset)
    {
        if (offset < 0)
            return "offset must be zero or greater.";

        if (offset > 0 && !HasTopLevelOrderBy(normalizedSql))
            return "offset > 0 requires an ORDER BY in the outer query. Without it the database may return rows in a different order on each call, so pages could overlap or skip rows. Add ORDER BY on a unique key, or use offset 0 with a keyset filter such as WHERE id > last_id ORDER BY id.";

        return null;
    }

    private static bool HasTopLevelOrderBy(string normalizedSql)
    {
        var sqlWithoutQuotedText = SqlToken().Replace(normalizedSql, match => match.Groups["comment"].Success ? " " : "''");
        var topLevelSql = new StringBuilder(sqlWithoutQuotedText.Length);
        var depth = 0;

        foreach (var character in sqlWithoutQuotedText)
        {
            if (character == '(')
                depth++;
            else if (character == ')')
                depth = Math.Max(0, depth - 1);

            topLevelSql.Append(depth == 0 && character != ')' ? character : ' ');
        }

        return TopLevelOrderBy().IsMatch(topLevelSql.ToString());
    }

    [GeneratedRegex("""(?<comment>--[^\r\n]*|/\*.*?\*/)|(?<escapeString>(?<![\w$])[Ee]')|(?<dollarQuote>(?<![\w$])\$(?:[A-Za-z_][A-Za-z_0-9]*)?\$)|"(?:[^"]|"")*"|'(?:[^']|'')*'""", RegexOptions.Singleline)]
    private static partial Regex SqlToken();

    [GeneratedRegex(@"\bORDER\s+BY\b", RegexOptions.IgnoreCase)]
    private static partial Regex TopLevelOrderBy();
}
