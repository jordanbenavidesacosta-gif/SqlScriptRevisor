using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.NoLock;
using RevisorScripstSQL.Utilities;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Core
{
    public class SqlAutoFixService
    {
        public void GenerarArchivoCorregido(string archivo, List<SqlError> errores, string rutaRaiz)
        {
            var encoding = DetectarEncoding(archivo);
            var lineasOriginales = File.ReadAllLines(archivo, encoding).ToList();
            var lineasCorregidas = new List<string>(lineasOriginales);

            int offset = 0;

            if (errores.Any(e => e.Regla == "USE"))
            {
                bool yaTieneUse = lineasCorregidas
                    .Take(10)
                    .Any(l => l.TrimStart().StartsWith("USE ", StringComparison.OrdinalIgnoreCase));

                if (!yaTieneUse)
                {
                    var bloqueUseInicial = new List<string>
                    {
                        "USE [Base de D]",
                        "GO",
                        "",
                        "SET ANSI_NULLS ON",
                        "GO",
                        "SET QUOTED_IDENTIFIER ON",
                        "GO",
                        ""
                    };

                    lineasCorregidas.InsertRange(0, bloqueUseInicial);
                    offset += bloqueUseInicial.Count;
                }
            }

            int indexSP = lineasCorregidas.FindIndex(l =>
                Regex.IsMatch(l, @"\b(CREATE|ALTER)\s+PROCEDURE\b", RegexOptions.IgnoreCase));

            bool esSP = indexSP != -1;

            foreach (var error in errores.Where(e => e.Regla != "NOLOCK" && e.Regla != "ALIAS"))
            {
                if (string.IsNullOrWhiteSpace(error.Correccion))
                    continue;

                if (error.Regla == "USE")
                    continue;

                int index = (error.Linea - 1) + offset;

                if (index < 0 || index >= lineasCorregidas.Count)
                    continue;

                var lineaOriginal = lineasCorregidas[index];

                if (error.Regla == "SP_HEADER")
                {
                    var bloque = new List<string>
                    {
                        "/*",
                        "-- Author:",
                        "-- Create date:",
                        "-- Description:",
                        "*/"
                    };
                    lineasCorregidas.InsertRange(index, bloque);
                    offset += bloque.Count;
                    continue;
                }

                if (error.Regla == "TRIGGER_HEADER")
                {
                    var bloque = new List<string>
                    {
                        "/*",
                        "-- Author:",
                        "-- Create date:",
                        "-- Description:",
                        "*/"
                    };
                    lineasCorregidas.InsertRange(index, bloque);
                    offset += bloque.Count;
                    continue;
                }

                if (esSP && error.Regla == "SP_PARAM_IDERROR")
                {
                    if (lineasCorregidas.Any(l => l.Contains("@iderror", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    int indexParams = lineasCorregidas.FindIndex(indexSP, l => l.Contains("("));
                    if (indexParams != -1)
                    {
                        lineasCorregidas.Insert(indexParams + 1, "    @iderror BIGINT = NULL OUTPUT,");
                        offset++;
                    }
                    continue;
                }

                if (esSP && error.Regla == "SP_PARAM_MENSAJE")
                {
                    if (lineasCorregidas.Any(l => l.Contains("@mensaje", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    int indexParams = lineasCorregidas.FindIndex(indexSP, l => l.Contains("("));
                    if (indexParams != -1)
                    {
                        lineasCorregidas.Insert(indexParams + 2, "    @mensaje NVARCHAR(1000) = NULL OUTPUT,");
                        offset++;
                    }
                    continue;
                }

                if (esSP && error.Regla == "SP_TRY")
                {
                    int indexBegin = lineasCorregidas.FindIndex(l =>
                        Regex.IsMatch(l.Trim(), @"^BEGIN\b", RegexOptions.IgnoreCase));

                    if (indexBegin != -1)
                    {
                        lineasCorregidas.Insert(indexBegin + 1, "    BEGIN TRY");
                        offset++;
                    }
                    continue;
                }

                if (esSP && error.Regla == "SP_CATCH")
                {
                    int indexEnd = lineasCorregidas.FindLastIndex(l =>
                        Regex.IsMatch(l.Trim(), @"^END\b", RegexOptions.IgnoreCase));

                    if (indexEnd != -1)
                    {
                        var bloqueCatch = new List<string>
                        {
                            "    END TRY",
                            "    BEGIN CATCH",
                            "        EXEC @iderror = administracion.sp_obtenerinformacionerror;",
                            "        SET @mensaje = ERROR_MESSAGE();",
                            "    END CATCH"
                        };
                        lineasCorregidas.InsertRange(indexEnd, bloqueCatch);
                        offset += bloqueCatch.Count;
                    }
                    continue;
                }

                if (error.Regla == "PRIMARY_KEY")
                {
                    int indexInicio = (error.Linea - 1) + offset;
                    if (lineasCorregidas[indexInicio].Contains("#")) continue;

                    int indexOpen = lineasCorregidas.FindIndex(indexInicio, l => l.Contains("("));
                    if (indexOpen == -1) continue;

                    int nivel = 0, indexClose = -1;
                    for (int i = indexOpen; i < lineasCorregidas.Count; i++)
                    {
                        nivel += lineasCorregidas[i].Count(c => c == '(');
                        nivel -= lineasCorregidas[i].Count(c => c == ')');
                        if (nivel == 0 && i > indexOpen) { indexClose = i; break; }
                    }
                    if (indexClose == -1) continue;

                    bool yaTienePK = lineasCorregidas
                        .Skip(indexOpen).Take(indexClose - indexOpen)
                        .Any(l => l.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase));
                    if (yaTienePK) continue;

                    int indexCol = indexOpen + 1;
                    while (indexCol < indexClose && string.IsNullOrWhiteSpace(lineasCorregidas[indexCol]))
                        indexCol++;

                    var lineaCol = lineasCorregidas[indexCol].TrimEnd();
                    bool esUltima = indexCol == indexClose - 1;
                    lineaCol = lineaCol.TrimEnd(',');
                    lineaCol += esUltima ? " PRIMARY KEY" : " PRIMARY KEY,";
                    lineasCorregidas[indexCol] = lineaCol;
                    continue;
                }

                if (error.Regla == "CREATE_TABLE")
                {
                    int indexInicio = (error.Linea - 1) + offset;
                    var lineaCreate = lineasCorregidas[indexInicio];
                    if (lineaCreate.Contains("#")) continue;

                    var match = Regex.Match(lineaCreate,
                        @"CREATE\s+TABLE\s+([\w\.\[\]]+)", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;

                    var tabla = match.Groups[1].Value;
                    bool yaTieneIf = lineasCorregidas
                        .Take(indexInicio)
                        .Any(l => l.Contains("OBJECT_ID", StringComparison.OrdinalIgnoreCase));

                    if (!yaTieneIf)
                    {
                        lineasCorregidas.Insert(indexInicio, $"IF OBJECT_ID('{tabla}') IS NULL");
                        lineasCorregidas.Insert(indexInicio + 1, "BEGIN");
                        offset += 2;
                        indexInicio += 2;
                    }

                    int indexOpen = lineasCorregidas.FindIndex(indexInicio, l => l.Contains("("));
                    int nivel = 0, indexClose = -1;
                    for (int i = indexOpen; i < lineasCorregidas.Count; i++)
                    {
                        nivel += lineasCorregidas[i].Count(c => c == '(');
                        nivel -= lineasCorregidas[i].Count(c => c == ')');
                        if (nivel == 0 && i > indexOpen) { indexClose = i; break; }
                    }
                    if (indexClose != -1) { lineasCorregidas.Insert(indexClose + 1, "END"); offset++; }
                    continue;
                }

                if (error.Regla == "TRIGGER")
                {
                    lineasCorregidas[index] = Regex.Replace(
                        lineaOriginal,
                        @"\b(CREATE|ALTER)\s+TRIGGER\b",
                        "CREATE OR ALTER TRIGGER",
                        RegexOptions.IgnoreCase);
                    continue;
                }

                if (error.Regla == "SP_CREATE")
                {
                    lineasCorregidas[index] = Regex.Replace(
                        lineaOriginal,
                        @"\b(CREATE|ALTER)\s+PROCEDURE\b",
                        "CREATE OR ALTER PROCEDURE",
                        RegexOptions.IgnoreCase);
                    continue;
                }

                lineasCorregidas[index] =
                    $"-- ORIGINAL: {lineasOriginales[error.Linea - 1]}\n{error.Correccion}";
            }

            var bloqueUsePre = ExtraerBloqueUse(lineasCorregidas);
            var headersPre = ExtraerHeadersSP(lineasCorregidas);

            if (errores.Any(e => e.Regla == "NOLOCK"))
            {
                var sqlNoLock = string.Join("\n", lineasCorregidas);
                sqlNoLock = AplicarNoLockConVisitor(sqlNoLock);
                lineasCorregidas = sqlNoLock.Split('\n').ToList();
            }

            if (errores.Any(e => e.Regla == "ALIAS"))
            {
                var sqlParaAlias = string.Join(Environment.NewLine, lineasCorregidas);
                var comentariosAlias = ExtraerComentariosInline(sqlParaAlias);

                var aliasService = new AliasRewriteService();
                var resultado = aliasService.ReescribirAliases(sqlParaAlias);

                if (!string.IsNullOrWhiteSpace(resultado))
                {
                    resultado = RestaurarComentariosInline(resultado, comentariosAlias);

                    resultado = Regex.Replace(resultado,
                        @"\b((?:LEFT|RIGHT|INNER|FULL|CROSS)\s+(?:OUTER\s+)?JOIN|JOIN)\s*\r?\n\s+",
                        m => m.Groups[1].Value + " ",
                        RegexOptions.IgnoreCase);

                    lineasCorregidas = resultado.Split(Environment.NewLine).ToList();
                }
                resultado = ColapsarJoinsSimples(resultado);
                lineasCorregidas = resultado.Split(Environment.NewLine).ToList();
            }

            var sqlFinal = string.Join(Environment.NewLine, lineasCorregidas);
            sqlFinal = SqlFormatter.FormatearKeywordsExtras(sqlFinal);

            if (!string.IsNullOrEmpty(bloqueUsePre) &&
                !sqlFinal.TrimStart().StartsWith("USE", StringComparison.OrdinalIgnoreCase))
            {
                sqlFinal = bloqueUsePre + Environment.NewLine + Environment.NewLine + sqlFinal;
            }

            sqlFinal = RestaurarHeaders(sqlFinal, headersPre);

            sqlFinal = FormatearParametrosSP(sqlFinal);

            var carpetaRelativa = Path.GetRelativePath(rutaRaiz, Path.GetDirectoryName(archivo)!);

            var carpetaSalida = carpetaRelativa == "."
                ? @"C:\Logs\Corregidos"
                : Path.Combine(@"C:\Logs\Corregidos", carpetaRelativa);

            if (!Directory.Exists(carpetaSalida))
                Directory.CreateDirectory(carpetaSalida);

            var nombreArchivo = Path.GetFileNameWithoutExtension(archivo);
            nombreArchivo = nombreArchivo
                .Replace("[", "")
                .Replace("]", "")
                .Trim();

            var rutaCorregido = Path.Combine(carpetaSalida, $"{nombreArchivo}_FIX.sql");

            File.WriteAllText(rutaCorregido, sqlFinal, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }


        private static string AplicarNoLockConVisitor(string sql)
        {
            var parser = new TSql150Parser(false);
            var fragment = parser.Parse(new StringReader(sql), out IList<ParseError> errors);

            if (errors?.Count > 0) return sql;

            var visitor = new NoLockVisitor();
            fragment.Accept(visitor);

            if (!visitor.Tablas.Any()) return sql;

            var lineas = sql.Split('\n').ToList();
            var procesadas = new HashSet<int>();

            foreach (var info in visitor.Tablas)
            {
                int lineIdx = info.Tabla.StartLine - 1;

                if (lineIdx < 0 || lineIdx >= lineas.Count) continue;
                if (procesadas.Contains(lineIdx)) continue;

                var linea = lineas[lineIdx];

                if (linea.Contains("NOLOCK", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Regex.IsMatch(linea, @"\b(FROM|JOIN)\b", RegexOptions.IgnoreCase)) continue;

                if (Regex.IsMatch(linea, @"\btemporales\.", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(linea, @"\b#\w+")) continue;

                bool enContextoDelete = false;
                for (int back = lineIdx - 1; back >= Math.Max(0, lineIdx - 15); back--)
                {
                    var lineaBack = lineas[back].Trim();
                    if (Regex.IsMatch(lineaBack, @"^DELETE\b", RegexOptions.IgnoreCase))
                    {
                        enContextoDelete = true;
                        break;
                    }
                    if (Regex.IsMatch(lineaBack, @"^(SELECT|INSERT|BEGIN|GO)\b", RegexOptions.IgnoreCase))
                        break;
                }
                if (enContextoDelete) continue;

                procesadas.Add(lineIdx);

                lineas[lineIdx] = Regex.Replace(
                    linea,
                    @"\b(FROM|INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+([\w\.\[\]@]+)(\s+(?:AS\s+)?[\w]+)?(?=\s+(?:ON|WITH|WHERE|INNER|LEFT|RIGHT|CROSS|FULL|$)|\s*$)",
                    mm =>
                    {
                        var kw = mm.Groups[1].Value;
                        var tabla = mm.Groups[2].Value;
                        var alias = mm.Groups[3].Value;
                        return $"{kw} {tabla}{alias} WITH (NOLOCK)";
                    },
                    RegexOptions.IgnoreCase);
            }

            return string.Join('\n', lineas);
        }

        private static List<(string Comentario, string LineaSiguiente)> ExtraerComentariosInline(string sql)
        {
            var resultado = new List<(string, string)>();
            var lineas = sql.Split('\n');

            for (int i = 0; i < lineas.Length; i++)
            {
                var l = lineas[i].TrimStart();

                if (!l.StartsWith("--")) continue;

                string siguienteLinea = string.Empty;

                for (int j = i + 1; j < lineas.Length; j++)
                {
                    var siguiente = lineas[j].Trim();

                    if (string.IsNullOrWhiteSpace(siguiente) || siguiente.StartsWith("--"))
                        continue;

                    siguienteLinea = siguiente;
                    break;
                }

                if (!string.IsNullOrEmpty(siguienteLinea))
                    resultado.Add((lineas[i].TrimEnd(), siguienteLinea));
            }

            return resultado;
        }
        private static string RestaurarComentariosInline(
            string sql,
            List<(string Comentario, string LineaSiguiente)> comentarios)
        {
            if (comentarios.Count == 0) return sql;

            var lineas = sql.Split('\n').ToList();

            var restaurados = new HashSet<int>();

            foreach (var (comentario, siguienteLinea) in comentarios)
            {
                var patron = Regex.Escape(siguienteLinea.Trim());

                for (int i = 0; i < lineas.Count; i++)
                {
                    if (restaurados.Contains(i)) continue;

                    if (!Regex.IsMatch(lineas[i].Trim(), $@"^{patron}", RegexOptions.IgnoreCase))
                        continue;

                    if (i > 0 && lineas[i - 1].Trim().Equals(comentario.Trim(), StringComparison.OrdinalIgnoreCase))
                        break;

                    lineas.Insert(i, comentario);
                    restaurados.Add(i);
                    break;
                }
            }

            return string.Join('\n', lineas);
        }


        private static string ExtraerBloqueUse(List<string> lineas)
        {
            var bloque = new List<string>();
            foreach (var linea in lineas)
            {
                if (Regex.IsMatch(linea.Trim(), @"^(CREATE|ALTER)\b", RegexOptions.IgnoreCase)) break;
                bloque.Add(linea);
            }
            var resultado = string.Join(Environment.NewLine, bloque).Trim();
            return resultado.Length > 0 ? resultado : string.Empty;
        }

        private static Dictionary<string, string> ExtraerHeadersSP(List<string> lineas)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lineas.Count; i++)
            {
                var linea = lineas[i].Trim();

                if (!Regex.IsMatch(linea,
                    @"\b(CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(PROCEDURE|TRIGGER)\b",
                    RegexOptions.IgnoreCase))
                    continue;

                int j = i - 1;
                while (j >= 0 && string.IsNullOrWhiteSpace(lineas[j])) j--;
                if (j < 0 || lineas[j].Trim() != "*/") continue;

                int fin = j;
                while (j >= 0 && !lineas[j].Trim().StartsWith("/*")) j--;
                if (j < 0) continue;

                var bloqueHeader = lineas.Skip(j).Take(fin - j + 1).ToList();
                var nombre = Regex.Match(linea,
                    @"\b(PROCEDURE|TRIGGER)\s+([\[\]\w\.]+)",
                    RegexOptions.IgnoreCase).Groups[2].Value;

                if (!string.IsNullOrEmpty(nombre) && bloqueHeader.Count > 0)
                    headers[nombre] = string.Join(Environment.NewLine, bloqueHeader);
            }

            return headers;
        }

        private static string RestaurarHeaders(string sql, Dictionary<string, string> headers)
        {
            if (headers.Count == 0) return sql;

            foreach (var kv in headers)
            {
                var nombreBase = kv.Key.Replace("[", "").Replace("]", "").Split('.').Last();
                var nombreEscaped = Regex.Escape(nombreBase);

                sql = Regex.Replace(sql,
                    $@"((?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(?:PROCEDURE|TRIGGER)\s+[\[\]\w\.]*{nombreEscaped}[\[\]\w\.]*)",
                    m =>
                    {
                        var antes = sql.Substring(0, m.Index).TrimEnd();
                        return antes.EndsWith("*/")
                            ? m.Value
                            : kv.Value + Environment.NewLine + m.Value;
                    },
                    RegexOptions.IgnoreCase);
            }

            return sql;
        }

        private static string FormatearParametrosSP(string sql)
        {
            return Regex.Replace(
                sql,
                @"(CREATE\s+OR\s+ALTER\s+PROCEDURE\s+[\[\]\w\.]+)([\s\S]*?)(\r?\nAS\b)",
                m =>
                {
                    var nombre = m.Groups[1].Value;
                    var bloque = m.Groups[2].Value;
                    var asKeyword = m.Groups[3].Value;

                    string parametros;

                    if (bloque.TrimStart().StartsWith("("))
                    {
                        int inicio = bloque.IndexOf('(');
                        int nivel = 0, fin = -1;
                        for (int i = inicio; i < bloque.Length; i++)
                        {
                            if (bloque[i] == '(') nivel++;
                            else if (bloque[i] == ')') { nivel--; if (nivel == 0) { fin = i; break; } }
                        }
                        if (fin == -1) return m.Value;
                        parametros = bloque.Substring(inicio + 1, fin - inicio - 1);
                    }
                    else
                    {
                        parametros = bloque;
                    }

                    var plano = Regex.Replace(parametros, @"[\r\n\t ]+", " ").Trim();
                    plano = plano.TrimStart(',').Trim();

                    if (string.IsNullOrWhiteSpace(plano)) return m.Value;

                    var lista = SplitParamsRespetandoParentesis(plano)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => Regex.Replace(p, @"\s*=\s*", " = "));

                    var formateado = string.Join("," + Environment.NewLine + "    ", lista);

                    return nombre + "(" + Environment.NewLine +
                           "    " + formateado + Environment.NewLine +
                           ")" + asKeyword;
                },
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        private static List<string> SplitParamsRespetandoParentesis(string parametros)
        {
            var result = new List<string>();
            int nivel = 0, inicio = 0;

            for (int i = 0; i < parametros.Length; i++)
            {
                if (parametros[i] == '(') nivel++;
                else if (parametros[i] == ')') nivel--;
                else if (parametros[i] == ',' && nivel == 0)
                {
                    result.Add(parametros.Substring(inicio, i - inicio));
                    inicio = i + 1;
                }
            }
            if (inicio < parametros.Length)
                result.Add(parametros.Substring(inicio));

            return result;
        }
        private static string ColapsarJoinsSimples(string sql)
        {
            sql = Regex.Replace(sql,
                @"\b((?:LEFT|RIGHT|INNER|FULL|CROSS)\s+(?:OUTER\s+)?JOIN|JOIN)\s*\r?\n[ \t]+(?!\()",
                m => m.Groups[1].Value + " ",
                RegexOptions.IgnoreCase);

            sql = Regex.Replace(sql,
                @"(\bWITH\s*\(NOLOCK\))\s*\r?\n[ \t]+(ON\b[^\r\n]+)",
                m => m.Groups[1].Value + " " + m.Groups[2].Value,
                RegexOptions.IgnoreCase);

            sql = Regex.Replace(sql,
                @"(?<![)\s])\b([\w\]]+)\s*\r?\n[ \t]+(ON\b[^\r\n]+)",
                m => m.Groups[1].Value + " " + m.Groups[2].Value,
                RegexOptions.IgnoreCase);

            return sql;
        }
        private static Encoding DetectarEncoding(string archivo)
        {
            var bom = new byte[4];
            using (var file = new FileStream(archivo, FileMode.Open, FileAccess.Read))
                file.Read(bom, 0, 4);

            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode;
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode;

            return Encoding.GetEncoding(1252);
        }
    }
}