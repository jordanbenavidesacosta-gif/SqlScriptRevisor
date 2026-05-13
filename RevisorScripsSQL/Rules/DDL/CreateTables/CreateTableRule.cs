using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.DDL.CreateTables
{
    public class CreateTableRule : ISqlRules
    {
        public List<SqlError> Validate(string[] lineas)
        {
            var errores = new List<SqlError>();
            var script = string.Join("\n", lineas);

            if (Regex.IsMatch(script, @"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase))
            {
                if (!Regex.IsMatch(script, @"\bUSE\s+\[?.+\]?", RegexOptions.IgnoreCase))
                {
                    errores.Add(new SqlError
                    {
                        Linea = 1,
                        Regla = "CREATE_TABLE",
                        Mensaje = "El script debe contener USE para definir la base de datos",
                        Correccion = "USE [NombreBase]\nGO",
                        CodigoLinea = "SCRIPT"
                    });
                }
            }

            for (int i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();

                if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith("--"))
                    continue;

                if (Regex.IsMatch(linea, @"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase))
                {
                    if (Regex.IsMatch(linea, @"CREATE\s+TABLE\s+#", RegexOptions.IgnoreCase))
                        continue;

                    var bloqueAnterior = string.Join(" ",
                        lineas.Skip(Math.Max(0, i - 10)).Take(10));

                    bool tieneValidacion =
                        Regex.IsMatch(bloqueAnterior, @"OBJECT_ID\s*\(", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(bloqueAnterior, @"IF\s+NOT\s+EXISTS", RegexOptions.IgnoreCase);

                    if (!tieneValidacion)
                    {
                        errores.Add(new SqlError
                        {
                            Linea = i + 1,
                            Regla = "CREATE_TABLE",
                            Mensaje = "CREATE TABLE sin validación de existencia",
                            Correccion = "IF OBJECT_ID('schema.tabla') IS NULL\nCREATE TABLE ...",
                            CodigoLinea = linea
                        });
                    }
                }
            }

            return errores;
        }
    }
}