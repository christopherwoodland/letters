using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DocumentClassifier.MCP;
using DocumentClassifier.Models;
using DocumentClassifier.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();
        services.AddHttpClient();

        // Register configuration
        services.Configure<SearchOptions>(context.Configuration.GetSection("Search"));
        services.Configure<AzureOpenAIOptions>(context.Configuration.GetSection("AzureOpenAI"));
        services.Configure<ResilienceOptions>(context.Configuration.GetSection("Resilience"));
        services.Configure<DocumentIntelligenceOptions>(context.Configuration.GetSection("DocumentIntelligence"));
        services.Configure<StorageOptions>(context.Configuration.GetSection("Storage"));

        // Register services from DocumentClassifier
        services.AddSingleton<ISearchIndexingService, SearchIndexingService>();
        services.AddSingleton<IClassificationService, ClassificationService>();
        services.AddSingleton<IRagService, RagService>();
        services.AddSingleton<ITextExtractionService, TextExtractionService>();
        services.AddSingleton<IDocumentStorageService, DocumentStorageService>();
        services.AddSingleton<IReviewQueueStore, FileBackedReviewQueueStore>();
        services.AddSingleton<IProfileStore, InMemoryProfileStore>();

        // Add MCP tools handler
        services.AddSingleton<MCPToolsHandler>();
    })
    .Build();

var profileStore = host.Services.GetRequiredService<IProfileStore>();
SeedProfiles(profileStore);

host.Run();

static void SeedProfiles(IProfileStore store)
{
    store.AddOrUpdate(new ClassificationProfile
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
