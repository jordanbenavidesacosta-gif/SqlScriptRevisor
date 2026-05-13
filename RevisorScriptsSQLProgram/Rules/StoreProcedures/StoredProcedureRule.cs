using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.Interfaces;
using System.IO;

namespace RevisorScripstSQL.Rules.StoreProcedures
{
    public class StoredProcedureRule : ISqlRules
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

            var visitor = new SpCompletoVisitor();
            fragment.Accept(visitor);

            if (!visitor.TodosLosSPs.Any())
                return errores;

            foreach (var sp in visitor.SpIncorrectos)
            {
                var nombre = ObtenerNombre(sp);
                var codigoLinea = sp.StartLine - 1 < lineas.Length
                    ? lineas[sp.StartLine - 1] : "";

                errores.Add(new SqlError
                {
                    Linea = sp.StartLine,
                    Regla = "SP_CREATE",
                    Mensaje = "Debe usar CREATE OR ALTER PROCEDURE",
                    Correccion = $"CREATE OR ALTER PROCEDURE {nombre}",
                    CodigoLinea = codigoLinea
                });
            }

            foreach (var sp in visitor.TodosLosSPs)
            {
                int lineaSP = sp.StartLine;
                string codigoLinea = lineaSP - 1 < lineas.Length
                    ? lineas[lineaSP - 1] : "";

                bool tieneIderror = sp.Parameters.Any(p =>
                    p.VariableName.Value.Equals("@iderror", StringComparison.OrdinalIgnoreCase) &&
                    p.Modifier == ParameterModifier.Output);

                if (!tieneIderror)
                {
                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_PARAM_IDERROR",
                        Mensaje = "Falta parámetro @iderror OUTPUT",
                        Correccion = "@iderror BIGINT = NULL OUTPUT,",
                        CodigoLinea = codigoLinea
                    });
                }

                bool tieneMensaje = sp.Parameters.Any(p =>
                    p.VariableName.Value.Equals("@mensaje", StringComparison.OrdinalIgnoreCase) &&
                    p.Modifier == ParameterModifier.Output);

                if (!tieneMensaje)
                {
                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_PARAM_MENSAJE",
                        Mensaje = "Falta parámetro @mensaje OUTPUT",
                        Correccion = "@mensaje NVARCHAR(1000) = NULL OUTPUT",
                        CodigoLinea = codigoLinea
                    });
                }

                var tryCatchChecker = new TryCatchChecker();
                sp.StatementList?.Accept(tryCatchChecker);

                if (!tryCatchChecker.TieneTryCatch)
                {

                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_TRY",
                        Mensaje = "Falta bloque BEGIN TRY",
                        Correccion = "BEGIN TRY",
                        CodigoLinea = codigoLinea
                    });

                    errores.Add(new SqlError
                    {
                        Linea = lineaSP,
                        Regla = "SP_CATCH",
                        Mensaje = "Falta bloque CATCH estándar",
                        Correccion =
@"BEGIN CATCH
    EXEC @iderror = administracion.sp_obtenerinformacionerror;
    SET @mensaje = ERROR_MESSAGE();
END CATCH",
                        CodigoLinea = codigoLinea
                    });
                }
            }

            return errores;
        }

        private static string ObtenerNombre(ProcedureStatementBodyBase sp)
        {
            SchemaObjectName nombre = sp switch
            {
                CreateOrAlterProcedureStatement coa => coa.ProcedureReference.Name,
                CreateProcedureStatement c => c.ProcedureReference.Name,
                AlterProcedureStatement a => a.ProcedureReference.Name,
                _ => null
            };

            if (nombre == null) return "<schema.sp_nombre>";

            var schema = nombre.SchemaIdentifier?.Value;
            var base_ = nombre.BaseIdentifier?.Value;

            return string.IsNullOrEmpty(schema)
                ? base_ ?? "<sp_nombre>"
                : $"{schema}.{base_}";
        }

        internal class SpCompletoVisitor : TSqlFragmentVisitor
        {

            public List<ProcedureStatementBodyBase> TodosLosSPs { get; } = new();


            public List<ProcedureStatementBodyBase> SpIncorrectos { get; } = new();

            public override void Visit(CreateOrAlterProcedureStatement node)
            {
                TodosLosSPs.Add(node);
            }

            public override void Visit(CreateProcedureStatement node)
            {
                TodosLosSPs.Add(node);
                SpIncorrectos.Add(node);
            }

            public override void Visit(AlterProcedureStatement node)
            {
                TodosLosSPs.Add(node);
                SpIncorrectos.Add(node);
            }
        }

        internal class TryCatchChecker : TSqlFragmentVisitor
        {
            public bool TieneTryCatch { get; private set; }

            public override void Visit(TryCatchStatement node)
            {
                TieneTryCatch = true;
            }
        }
    }
}
