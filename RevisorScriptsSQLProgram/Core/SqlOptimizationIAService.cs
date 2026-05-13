using System.Text;
using System.Text.Json;

public class SqlOptimizationIAService
{
    private readonly HttpClient _http = new HttpClient();

    public async Task<string> OptimizarSql(string sql)
    {
        var prompt = $@"You are a SQL Server query optimizer.

Rewrite the SQL query to improve performance.

STRICT RULES:
- Output ONLY valid SQL Server code
- DO NOT include explanations
- DO NOT include comments
- DO NOT suggest indexes
- DO NOT describe anything
- DO NOT add text before or after the SQL
- DO NOT wrap the response in markdown
- Keep the exact same result set
- Do not change business logic
- Preserve all output columns

If no optimization is possible, return the original query.

SQL:
{sql}
";

        var request = new
        {
            model = "deepseek-coder",
            prompt = prompt,
            stream = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _http.PostAsync("http://localhost:11434/api/generate", content);
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("response", out var responseProp))
        {
            return responseProp.GetString();
        }

        if (doc.RootElement.TryGetProperty("error", out var errorProp))
        {
            throw new Exception($"Ollama error: {errorProp.GetString()}");
        }

        throw new Exception("Respuesta inesperada de Ollama");
    }
}