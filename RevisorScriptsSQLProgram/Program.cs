using RevisorScripstSQL.Core;
using System.Text;

var log = new StringBuilder();

void Log(string mensaje)
{
    Console.WriteLine(mensaje);
    log.AppendLine(mensaje);
}

try
{
    var iaService = new SqlOptimizationIAService();
    bool usarIA = false;
    Log("Ingrese la ruta de la carpeta a escanear:");
    string? ruta = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(ruta) || !Directory.Exists(ruta))
    {
        Log("❌ Ruta inválida");
        return;
    }

    var auditor = new SqlAuditorService();

    var archivos = Directory.GetFiles(ruta, "*.sql", SearchOption.AllDirectories);

    if (archivos.Length == 0)
    {
        Log("⚠ No se encontraron archivos .sql en la carpeta");
        return;
    }

    Log($"\nSe encontraron {archivos.Length} archivos\n");

    var agrupados = archivos.GroupBy(f =>
    {
        var relative = Path.GetRelativePath(ruta, Path.GetDirectoryName(f)!);
        return string.IsNullOrWhiteSpace(relative) ? "RAIZ" : relative;
    });

    foreach (var carpeta in agrupados.OrderBy(g => g.Key))
    {
        Log($"\n📁 CARPETA: {carpeta.Key}");
        Log(new string('=', 60));

        foreach (var archivo in carpeta)
        {
            Log($"\n📄 ARCHIVO: {Path.GetFileName(archivo)}");
            Log(new string('-', 60));

            string contenido;

            try
            {
                contenido = File.ReadAllText(archivo, Encoding.UTF8);
            }
            catch
            {
                Log("❌ Error leyendo el archivo\n");
                continue;
            }

            string sqlFinal = contenido;

            var autoFix = new SqlAutoFixService();

            if (usarIA)
            {
                Log("🤖 Analizando optimización con IA...\n");

                try
                {
                    var optimizado = await iaService.OptimizarSql(contenido);

                    if (!string.IsNullOrWhiteSpace(optimizado))
                    {
                        sqlFinal = optimizado;

                        Log("🔧 SQL OPTIMIZADO (IA):\n");
                        Log(optimizado);
                    }
                    else
                    {
                        Log("⚠ IA no retornó contenido, se usa SQL original\n");
                    }

                    Log(new string('-', 80));
                }
                catch (Exception ex)
                {
                    Log($"❌ Error IA: {ex.Message}");
                }
            }

            var errores = auditor.Auditar(sqlFinal);

            if (errores.Count > 0)
            {
                autoFix.GenerarArchivoCorregido(archivo, errores, ruta);
            }

            if (errores.Count == 0)
            {
                Log("✔ SIN ERRORES\n");
                continue;
            }            

            Log($"Errores encontrados: {errores.Count}\n");

            var porLinea = errores.GroupBy(e => e.Linea);

            foreach (var grupo in porLinea.OrderBy(g => g.Key))
            {
                var linea = grupo.First();
                var codigoLinea = linea.CodigoLinea?.Trim() ?? "";

                Log($"LINEA {linea.Linea.ToString().PadLeft(4)} | {codigoLinea}");

                foreach (var e in grupo)
                {
                    Log($"  ERROR: {e.Mensaje.Trim()}");

                    if (!string.IsNullOrWhiteSpace(e.Correccion))
                        Log($"  POSIBLE CORRECCIÓN: {e.Correccion.Trim()}");

                    Log("");
                }

                Log(new string('-', 60));
            }

            Log(new string('=', 80));
        }
    }
}
catch (Exception ex)
{
    Log($"❌ Error inesperado: {ex.Message}");
}
finally
{
    try
    {
        var carpetaLogs = @"C:\Logs";

        if (!Directory.Exists(carpetaLogs))
        {
            Directory.CreateDirectory(carpetaLogs);
        }

        var rutaLog = Path.Combine(
            carpetaLogs,
            $"ResultadoLogsRevisorSQL_{DateTime.Now:yyyyMMdd}.txt"
        );

        File.WriteAllText(rutaLog, log.ToString());

        Console.WriteLine($"\n📄 Log generado en: {rutaLog}");
    }
    catch
    {
        Console.WriteLine("❌ No se pudo guardar el log en C:\\Logs");
    }

    Console.WriteLine("\n✔ Proceso finalizado");
    Console.WriteLine("Presione una tecla para salir...");
    Console.ReadKey();
}