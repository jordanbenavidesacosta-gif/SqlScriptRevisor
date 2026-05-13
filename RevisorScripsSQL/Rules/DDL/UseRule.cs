using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.DDL
{
    public class UseRule : ISqlRules
    {
        public List<SqlError> Validate(string[] lineas)
        {
            var errores = new List<SqlError>();

            if (lineas == null || lineas.Length == 0)
                return errores;

            int lineaUse = -1;
            int primeraLineaValida = -1;

            for (int i = 0; i < lineas.Length; i++)
            {
                var l = lineas[i].Trim();

                if (string.IsNullOrWhiteSpace(l) || l.StartsWith("--"))
                    continue;

                if (Regex.IsMatch(l, @"^USE\s+", RegexOptions.IgnoreCase))
                {
                    lineaUse = i;
                }

                if (primeraLineaValida == -1)
                    primeraLineaValida = i;
            }

            if (lineaUse == -1)
            {
                errores.Add(new SqlError
                {
                    Linea = primeraLineaValida + 1,
                    Regla = "USE",
                    Mensaje = "El script debe contener USE",
                    Correccion = "Correccion = @\"USE [Base de D]\r\nGO\r\n\r\nSET ANSI_NULLS ON\r\nGO\r\nSET QUOTED_IDENTIFIER ON\r\nGO\"",
                    CodigoLinea = lineas[primeraLineaValida].Trim()
                });
            }

            else if (lineaUse > primeraLineaValida)
            {
                errores.Add(new SqlError
                {
                    Linea = lineaUse + 1,
                    Regla = "USE",
                    Mensaje = "USE debería estar al inicio del script",
                    Correccion = "Mover USE al inicio del script",
                    CodigoLinea = lineas[lineaUse].Trim()
                });
            }

            return errores;
        }
    }
}