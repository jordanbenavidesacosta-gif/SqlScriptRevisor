using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.IO;

namespace RevisorScripstSQL.Rules.StoreProcedures.Alias
{
    public class AliasRule : ISqlRules
    {
        public List<SqlError> Validate(string[] lineas)
        {
            var errores = new List<SqlError>();

            var script = string.Join("\n", lineas);

            var parser = new TSql150Parser(false);

            IList<ParseError> parseErrors;

            var fragment = parser.Parse(new StringReader(script), out parseErrors);

            if (parseErrors != null && parseErrors.Count > 0)
                return errores;

            var visitor = new AliasVisitor();

            fragment.Accept(visitor);

            foreach (var tabla in visitor.TablasSinAlias)
            {
                var linea = tabla.TablaReferencia.StartLine;

                var codigo = linea - 1 < lineas.Length
                    ? lineas[linea - 1]
                    : "";

                errores.Add(new SqlError
                {
                    Linea = linea,
                    Regla = "ALIAS",
                    Mensaje = $"Tabla sin alias. Alias sugerido: {tabla.AliasNuevo}",
                    CodigoLinea = codigo,
                    Correccion = tabla.AliasNuevo
                });
            }

            return errores;
        }
    }
}