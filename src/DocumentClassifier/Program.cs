using DocumentClassifier.Services;
using DocumentClassifier.Workflow;
using DocumentClassifier.Infrastructure;
using Microsoft.Extensions.Options;
using Azure.Identity;
using Azure.Core;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<DocumentIntelligenceOptions>(builder.Configuration.GetSection("DocumentIntelligence"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection("Search"));
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection("Workflow"));
builder.Services.Configure<ResilienceOptions>(builder.Configuration.GetSection("Resilience"));

// Shared Azure credential
var tenantId = builder.Configuration["Azure:TenantId"];
builder.Services.AddSingleton<TokenCredential>(_ =>
{
    var options = new DefaultAzureCredentialOptions();
    if (!string.IsNullOrEmpty(tenantId))
        options.TenantId = tenantId;
    return new DefaultAzureCredential(options);
});

// Services
builder.Services.AddSingleton<IProfileStore, InMemoryProfileStore>();
builder.Services.AddSingleton<ITextExtractionService, TextExtractionService>();
builder.Services.AddSingleton<IClassificationService, ClassificationService>();
builder.Services.AddSingleton<IDocumentStorageService, DocumentStorageService>();
builder.Services.AddSingleton<ISearchIndexingService, SearchIndexingService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IReviewQueueStore, FileBackedReviewQueueStore>();
builder.Services.AddSingleton<DocumentClassificationWorkflowFactory>();
builder.Services.AddScoped<IDocumentWorkflow, DocumentWorkflow>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Document Classifier API", Version = "v1" });
});

var app = builder.Build();

// Seed default classification profiles
var profileStore = app.Services.GetRequiredService<IProfileStore>();
SeedProfiles(profileStore);

// Ensure search index exists if configured
var searchOptions = app.Services.GetRequiredService<IOptions<SearchOptions>>().Value;
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

if (!string.IsNullOrWhiteSpace(searchOptions.Endpoint)
    && !searchOptions.Endpoint.Contains("YOUR-SEARCH", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var searchService = app.Services.GetRequiredService<ISearchIndexingService>();
        await searchService.EnsureIndexExistsAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Search index initialization failed. API will continue running without RAG indexing.");
    }
}
else
{
    startupLogger.LogInformation("Search endpoint not configured. Skipping index initialization.");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapControllers();

app.Run();

static void SeedProfiles(IProfileStore store)
{
    store.AddOrUpdate(new DocumentClassifier.Models.ClassificationProfile
    {
        Name = "relief_request_binary",
        Description = "Binary classification: does the filing request court relief or not",
        SystemPrompt = "You are a legal document classifier for court filings. Decide whether the filing asks the court to do something on the litigant's behalf (relief requested) or not.",
        Categories = new()
        {
            "asks_for_relief",
            "no_relief_requested"
        },
        CategoryExamples = new()
        {
            ["asks_for_relief"] = new()
            {
                "I respectfully request that the Court dismiss this case.",
                "Please grant me an extension of time to file my response.",
                "I ask the Court to order the defendant to produce records.",
                "Can you please seal this exhibit for my safety?"
            },
            ["no_relief_requested"] = new()
            {
                "I am submitting this letter to update my mailing address.",
                "Enclosed are copies of records for the Court file.",
                "This notice confirms I received the hearing date.",
                "I am writing to clarify a typo in my previous filing."
            }
        },
        OutputInstructions = "In metadata, extract: requested_relief_action, cue_phrases, tone (formal/informal), confidence_notes."
    });
}

public partial class Program;
