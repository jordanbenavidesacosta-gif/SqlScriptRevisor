using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Rules.StoredProcedures
{
    public class StoredProcedureHeaderRuleDom : ISqlRules
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

            var visitor = new SpVisitor();
            fragment.Accept(visitor);

            var lineasGo = fragment.ScriptTokenStream
                .Where(t => t.TokenType == TSqlTokenType.Go)
                .Select(t => t.Line)
                .OrderBy(l => l)
                .ToList();

            foreach (var sp in visitor.StoredProcedures)
            {


                int lineaSP = sp.StartLine;


                int inicioBatch = lineasGo
                    .Where(l => l < lineaSP)
                    .DefaultIfEmpty(0)
                    .Max();



                int lineaLimite = visitor.StoredProcedures.Where(p => p.StartLine < lineaSP).Select(p => p.StartLine).DefaultIfEmpty(0).Max();
                var comentariosAntes = fragment.ScriptTokenStream.Where(t => (t.TokenType == TSqlTokenType.SingleLineComment || t.TokenType == TSqlTokenType.MultilineComment) && t.Line < lineaSP && t.Line > lineaLimite).OrderByDescending(t => t.Line).Take(20) .ToList();
                if (!comentariosAntes.Any())
                {
                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_HEADER",
                        Mensaje = "El SP no tiene encabezado de comentarios",
                        Correccion = "Agregar bloque con Author, Create date, Description",
                        CodigoLinea = "CREATE PROCEDURE"
                    });
                    continue;
                }

                var comentarioRaw = string.Join("\n", comentariosAntes.Select(c => c.Text));
                var comentario = Normalizar(comentarioRaw);

                bool tieneAutor = Regex.IsMatch(comentario, @"\b(author|autor)\b");
                bool tieneFecha = Regex.IsMatch(comentario, @"\b(create\s*date|fecha\s*creacion|fecha)\b");
                bool tieneDescripcion = Regex.IsMatch(comentario, @"\b(description|descripcion)\b");

                if (!tieneAutor || !tieneFecha || !tieneDescripcion)
                {
                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_HEADER",
                        Mensaje = "Encabezado incompleto (Author/Autor, Create date/Fecha, Description/Descripción)",
                        Correccion = "Agregar bloque con Author, Create date, Description",
                        CodigoLinea = comentarioRaw.Trim()
                    });
                }
            }

            return errores;
        }

        private string Normalizar(string texto)
        {
            var normalized = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).ToLower();
        }
    }

    public class SpVisitor : TSqlFragmentVisitor
    {
        // Cambiar a la base común de los 3 tipos
        public List<ProcedureStatementBodyBase> StoredProcedures { get; } = new();

        public override void Visit(CreateOrAlterProcedureStatement node)
            => StoredProcedures.Add(node);

        public override void Visit(CreateProcedureStatement node)
            => StoredProcedures.Add(node);

        public override void Visit(AlterProcedureStatement node)
            => StoredProcedures.Add(node);
    }
}
