using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.IO;

namespace RevisorScripstSQL.Utilities
{
    public class SqlFormatter
    {
        internal static SqlScriptGeneratorOptions OpcionesFormato => new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IndentationSize = 4,
            IncludeSemicolons = true
        };

        internal static readonly string[] KeywordsExtras = new[]
        {
            // DDL
            "create", "alter", "drop", "procedure", "trigger",
            "table", "view", "index", "database", "schema",

            // DML
            "select", "insert", "update", "delete", "merge",
            "truncate",

            // Cláusulas principales
            "from", "where", "join", "inner", "left", "right",
            "full", "outer", "cross", "apply", "on", "having",
            "distinct", "top", "with", "as", "into",

            // Agrupamiento y orden
            "group", "by", "order", "asc", "desc",

            // Control de flujo
            "begin", "end", "try", "catch", "if", "else",
            "while", "case", "when", "then", "return", "break",
            "continue", "goto", "waitfor",

            // Operadores y condiciones
            "and", "or", "not", "is", "null", "in", "like",
            "between", "exists", "any", "all", "some",
            "union", "intersect", "except",

            // Declaraciones y ejecución
            "set", "declare", "exec", "execute", "go", "use",
            "output", "values", "default",

            // Transacciones
            "begin", "commit", "rollback", "transaction", "tran",
            "save", "savepoint",

            // Tipos de JOIN
            "pivot", "unpivot",

            // Funciones y tipos de dato
            "varchar", "nvarchar", "char", "nchar",
            "int", "bigint", "smallint", "tinyint",
            "decimal", "numeric", "float", "real",
            "datetime", "datetime2", "date", "time",
            "smalldatetime", "datetimeoffset",
            "bit", "money", "smallmoney",
            "uniqueidentifier", "binary", "varbinary",
            "text", "ntext", "image", "xml", "sql_variant",

            // Funciones de agregado y cadena
            "substring", "len", "isnull", "nullif", "coalesce",
            "count", "sum", "avg", "min", "max",
            "convert", "cast", "iif", "choose",
            "getdate", "getutcdate", "sysdatetime",
            "dateadd", "datediff", "datename", "datepart",
            "datefromparts", "eomonth",
            "year", "month", "day",
            "upper", "lower", "ltrim", "rtrim", "trim",
            "replace", "stuff", "charindex", "patindex",
            "left", "right", "reverse",
            "round", "floor", "ceiling", "abs", "power", "sqrt",
            "newid", "checksum",
            "row_number", "rank", "dense_rank", "ntile",
            "lead", "lag", "first_value", "last_value",
            "over", "partition",

            // Otros
            "nolock", "with", "primary", "key", "foreign",
            "references", "constraint", "identity", "not",
            "unique", "clustered", "nonclustered",
            "rowcount", "nocount", "ansi_nulls",
            "quoted_identifier", "object_id", "scope_identity"
        };

        public string Formatear(string sql)
        {
            return FormatearKeywordsExtras(sql);
        }

        public string FormatearCompleto(string script)
        {
            return Formatear(script);
        }

        internal static string FormatearKeywordsExtras(string sql)
        {
            foreach (var keyword in KeywordsExtras)
            {
                sql = Regex.Replace(
                    sql,
                    $@"\b{keyword}\b",
                    keyword.ToUpper(),
                    RegexOptions.IgnoreCase);
            }

            sql = Regex.Replace(
                sql,
                @"\b(nvarchar|varchar|char|nchar)\s*\(\s*max\s*\)",
                m => $"{m.Groups[1].Value.ToUpper()}(MAX)",
                RegexOptions.IgnoreCase);

            return sql;
        }
        internal static string FormatearConScriptDom(string sql)
        {
            try
            {
                var parser = new TSql150Parser(false);
                using var reader = new StringReader(sql);

                var fragment = parser.Parse(reader, out IList<ParseError> errors);
                if (errors != null && errors.Count > 0) return sql;

                var generator = new Sql150ScriptGenerator(OpcionesFormato);
                generator.GenerateScript(fragment, out string resultado);

                return FormatearKeywordsExtras(resultado);
            }
            catch
            {
                return sql;
            }
        }
    }
}