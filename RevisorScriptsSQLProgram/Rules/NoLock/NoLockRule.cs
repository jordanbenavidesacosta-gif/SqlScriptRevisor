using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.NoLock
{
    public class NoLockRule : ISqlRules
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

            var visitor = new NoLockVisitor();
            fragment.Accept(visitor);

            var lineasProcesadas = new HashSet<int>();
            bool esTrigger = lineas.Any(l => l.Contains("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) ||
                                             l.Contains("ALTER TRIGGER", StringComparison.OrdinalIgnoreCase));

            if (esTrigger)
                return errores;

            foreach (var info in visitor.Tablas)
            {
                var tabla = info.Tabla;
                var tipo = info.Tipo;
                var keyword = info.Tipo;

                int linea = tabla.StartLine;

                var lineaTexto = linea - 1 < lineas.Length ? lineas[linea - 1] : "";
                var lineaClean = lineaTexto.TrimStart();

                if (Regex.IsMatch(
                    lineaClean,
                    @"^(UPDATE|DELETE|INSERT|MERGE)\b",
                    RegexOptions.IgnoreCase))
                {
                    continue;
                }

                if (tabla.TableHints != null && tabla.TableHints.Count > 0)
                    continue;

                if (tabla.SchemaObject == null || tabla.SchemaObject.BaseIdentifier == null)
                    continue;


                var schema = tabla.SchemaObject.SchemaIdentifier?.Value;

                if (!string.IsNullOrEmpty(schema))
                {
                    if (schema.Equals("sys", StringComparison.OrdinalIgnoreCase) ||
                        schema.Equals("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (tabla.TableHints != null && tabla.TableHints.Count > 0)
                    continue;

                if (lineasProcesadas.Contains(linea))
                    continue;

                lineasProcesadas.Add(linea);

                var alias = string.IsNullOrEmpty(tabla.Alias?.Value)
                    ? ""
                    : $" {tabla.Alias.Value}";

                var db = tabla.SchemaObject.DatabaseIdentifier?.Value;
                var nombre = tabla.SchemaObject.BaseIdentifier?.Value;

                if (esTrigger)
                {
                    if (nombre.Equals("inserted", StringComparison.OrdinalIgnoreCase) ||
                        nombre.Equals("deleted", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                string nombreTabla;

                if (!string.IsNullOrEmpty(db) && !string.IsNullOrEmpty(schema))
                    nombreTabla = $"{db}.{schema}.{nombre}";
                else if (!string.IsNullOrEmpty(db))
                    nombreTabla = $"{db}..{nombre}";
                else if (!string.IsNullOrEmpty(schema))
                    nombreTabla = $"{schema}.{nombre}";
                else
                    nombreTabla = nombre;

                if (nombreTabla.StartsWith("#"))
                    continue;
                var correccion = $"{keyword} {nombreTabla}{alias} WITH (NOLOCK)";


                errores.Add(new SqlError
                {
                    Linea = linea,
                    Regla = "NOLOCK",
                    Mensaje = "Falta WITH (NOLOCK)",
                    Correccion = correccion,
                    CodigoLinea = lineaTexto
                });
            }

            return errores;
        }
    }
}