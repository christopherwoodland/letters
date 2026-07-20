using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DocumentClassifier.MCP;

public class MCPServerFunction
{
    private readonly MCPToolsHandler _tools;
    private readonly ILogger<MCPServerFunction> _logger;

    public MCPServerFunction(MCPToolsHandler tools, ILogger<MCPServerFunction> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    /// <summary>
    /// HTTP endpoint that accepts MCP tool calls.
    /// 
    /// POST /api/mcp/tools/{toolName}
    /// Body: JSON with tool parameters
    /// 
    /// Tools:
    /// - list_profiles: No parameters
    /// - classify_text: { text, profileName, exampleCount? }
    /// - search_documents: { query, category?, topResults? }
    /// - get_review_queue: No parameters
    /// </summary>
    [Function("MCPTools")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mcp/tools/{toolName}")] HttpRequestData req,
        string toolName,
        FunctionContext context)
    {
        _logger.LogInformation("MCP tool request: {ToolName}", toolName);

        try
        {
            var ct = context.CancellationToken;
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            MCPToolResult result = toolName.ToLowerInvariant() switch
            {
                "list_profiles" =>
                    await _tools.ListProfilesAsync(ct),

                "classify_text" =>
                    await HandleClassifyTextAsync(requestBody, ct),

                "search_documents" =>
                    await HandleSearchDocumentsAsync(requestBody, ct),

                "get_review_queue" =>
                    await _tools.GetReviewQueueAsync(ct),

                _ => MCPToolResult.FromError($"Unknown tool: {toolName}")
            };

            var response = req.CreateResponse(result.Success ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.BadRequest);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteAsJsonAsync(result);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP tool: {ToolName}", toolName);
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(MCPToolResult.FromError($"Internal error: {ex.Message}"));
            return errorResponse;
        }
    }

    private async Task<MCPToolResult> HandleClassifyTextAsync(string requestBody, CancellationToken ct)
    {
        var options = JsonSerializerOptions.Web;
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        var text = root.GetProperty("text").GetString() ?? "";
        var profileName = root.GetProperty("profileName").GetString() ?? "";
        var exampleCount = root.TryGetProperty("exampleCount", out var ec) ? ec.GetInt32() : 3;

        return await _tools.ClassifyTextAsync(text, profileName, exampleCount, ct);
    }

    private async Task<MCPToolResult> HandleSearchDocumentsAsync(string requestBody, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        var query = root.GetProperty("query").GetString() ?? "";
        var category = root.TryGetProperty("category", out var cat) ? cat.GetString() : null;
        var topResults = root.TryGetProperty("topResults", out var tr) ? tr.GetInt32() : 5;

        return await _tools.SearchDocumentsAsync(query, category, topResults, ct);
    }
}
