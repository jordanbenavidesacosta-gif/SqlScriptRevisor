using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.DDL.CreateTables
{
    public class PrimaryKeyRule : ISqlRules
    {
        public List<SqlError> Validate(string[] lineas)
        {
            var errores = new List<SqlError>();

            for (int i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();

                if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith("--"))
                    continue;

                if (Regex.IsMatch(linea, @"\bCREATE\s+TABLE\b", RegexOptions.IgnoreCase))
                {
                    var matchTabla = Regex.Match(
                        linea,
                        @"CREATE\s+TABLE\s+([\[\]\w\.#]+)",
                        RegexOptions.IgnoreCase
                    );

                    if (matchTabla.Success)
                    {
                        var nombreTabla = matchTabla.Groups[1].Value;

                        if (nombreTabla.StartsWith("#"))
                            continue;
                    }

                    int inicio = i;
                    bool tienePK = false;
                    int nivelParentesis = 0;
                    bool inicioDetectado = false;

                    for (int j = i; j < lineas.Length; j++)
                    {
                        var l = lineas[j];

                        nivelParentesis += l.Count(c => c == '(');
                        nivelParentesis -= l.Count(c => c == ')');

                        if (l.Contains("("))
                            inicioDetectado = true;

                        if (Regex.IsMatch(l, @"\bPRIMARY\s+KEY\b", RegexOptions.IgnoreCase))
                        {
                            tienePK = true;
                        }

                        if (inicioDetectado && nivelParentesis == 0 && j > i)
                        {
                            break;
                        }
                    }

                    if (!tienePK)
                    {
                        errores.Add(new SqlError
                        {
                            Linea = inicio + 1,
                            Regla = "PRIMARY_KEY",
                            Mensaje = "La tabla no tiene PRIMARY KEY",
                            Correccion = "Agregar PRIMARY KEY (columna)",
                            CodigoLinea = linea
                        });
                    }
                }
            }

            return errores;
        }
    }
}