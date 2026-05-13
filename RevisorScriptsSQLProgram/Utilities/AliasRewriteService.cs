using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.StoreProcedures.Alias;
using RevisorScripstSQL.Utilities;
using System.IO;

namespace RevisorScripstSQL.Core
{
    public class AliasRewriteService
    {
        public string ReescribirAliases(string sql)
        {
            var parser = new TSql150Parser(false);
            IList<ParseError> errors;

            var fragment = parser.Parse(new StringReader(sql), out errors);

            if (errors != null && errors.Count > 0)
                return sql;

            var visitor = new AliasVisitor();
            fragment.Accept(visitor);

            if (!visitor.TablasSinAlias.Any())
                return sql;

            var rewriter = new AliasAstRewriter(visitor.TablasSinAlias);
            fragment.Accept(rewriter);

            var generator = new Sql150ScriptGenerator(SqlFormatter.OpcionesFormato);
            generator.GenerateScript(fragment, out string sqlFinal);

            sqlFinal = SqlFormatter.FormatearKeywordsExtras(sqlFinal);

            return sqlFinal;
        }
    }
}
