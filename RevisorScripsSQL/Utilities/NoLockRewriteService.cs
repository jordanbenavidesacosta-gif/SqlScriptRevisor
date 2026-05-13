using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Rules.NoLock;
using RevisorScripstSQL.Utilities;
using System.IO;

namespace RevisorScripstSQL.Utilities
{
    public class NoLockRewriteService
    {
        public string AgregarNoLock(string sql)
        {
            var parser = new TSql150Parser(false);
            IList<ParseError> errors;

            var fragment = parser.Parse(new StringReader(sql), out errors);

            // Si el script tiene errores de parseo, devolver sin modificar
            if (errors != null && errors.Count > 0)
                return sql;

            var rewriter = new NoLockAstRewriter();
            fragment.Accept(rewriter);

            // Usar las mismas opciones de formato compartidas
            var generator = new Sql150ScriptGenerator(SqlFormatter.OpcionesFormato);
            generator.GenerateScript(fragment, out string resultado);

            resultado = SqlFormatter.FormatearKeywordsExtras(resultado);

            return resultado;
        }
    }
}
