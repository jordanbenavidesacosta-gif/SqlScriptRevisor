using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.Triggers
{
    public class TriggerRule : ISqlRules
    {
        public List<SqlError> Validate(string[] lineas)
        {
            var errores = new List<SqlError>();

            int indexTrigger = -1;
            string lineaTrigger = "";

            for (int i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();

                if (Regex.IsMatch(linea, @"\b(CREATE|ALTER)\s+TRIGGER\b", RegexOptions.IgnoreCase))
                {
                    indexTrigger = i;
                    lineaTrigger = linea;
                    break;
                }
            }

            if (indexTrigger == -1)
                return errores;

            var script = string.Join("\n", lineas);

            if (!Regex.IsMatch(lineaTrigger, @"CREATE\s+OR\s+ALTER\s+TRIGGER", RegexOptions.IgnoreCase))
            {
                var nombre = Regex.Match(lineaTrigger, @"TRIGGER\s+([\[\]\w\.]+)", RegexOptions.IgnoreCase);

                errores.Add(new SqlError
                {
                    Linea = indexTrigger + 1,
                    Regla = "TRIGGER",
                    Mensaje = "Debe usar CREATE OR ALTER TRIGGER",
                    Correccion = nombre.Success
                        ? $"CREATE OR ALTER TRIGGER {nombre.Groups[1].Value}"
                        : "CREATE OR ALTER TRIGGER <schema.trigger>",
                    CodigoLinea = lineaTrigger
                });
            }

            var comentariosAntes = lineas
                .Take(indexTrigger)
                .Reverse()
                .Take(10)
                .Where(l => l.TrimStart().StartsWith("--") || l.Contains("/*"))
                .ToList();

            if (!comentariosAntes.Any())
            {
                errores.Add(new SqlError
                {
                    Linea = indexTrigger + 1,
                    Regla = "TRIGGER_HEADER",
                    Mensaje = "El TRIGGER no tiene encabezado de comentarios",
                    Correccion =
@"/*
-- Author:
-- Create date:
-- Description:
*/",
                    CodigoLinea = lineaTrigger
                });
            }

            return errores;
        }
    }
}