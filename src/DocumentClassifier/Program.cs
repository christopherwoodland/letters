using DocumentClassifier.Services;
using DocumentClassifier.Workflow;
using DocumentClassifier.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
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
builder.Services.AddSingleton<IProfileStore, FileBackedProfileStore>();
builder.Services.AddSingleton<ITextExtractionService, TextExtractionService>();
builder.Services.AddSingleton<IClassificationService, ClassificationService>();
builder.Services.AddSingleton<IDocumentStorageService, DocumentStorageService>();
builder.Services.AddSingleton<ISearchIndexingService, SearchIndexingService>();
builder.Services.AddSingleton<IRagService, RagService>();
builder.Services.AddSingleton<IReviewQueueStore, FileBackedReviewQueueStore>();
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();
builder.Services.AddSingleton<DocumentClassificationWorkflowFactory>();
builder.Services.AddScoped<IDocumentWorkflow, DocumentWorkflow>();

// Authentication (Azure AD/Entra ID)
var authOptions = builder.Configuration.GetSection("Authentication");
if (authOptions.GetValue<bool>("Enabled"))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("Authentication"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(AuthorizationPolicies.DocumentProcessing, policy =>
            policy.RequireAuthenticatedUser())
        .AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole("Admin", "DocumentClassifierAdmin"));
}

// CORS with environment-specific configuration
var corsOptions = builder.Configuration.GetSection("Cors");
var allowedOrigins = corsOptions.GetValue<string>("AllowedOrigins", "http://localhost:5173")?.Split(";") ?? [];
var allowedMethods = corsOptions.GetSection("AllowedMethods").Get<string[]>() ?? ["GET", "POST"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DocumentClassifierPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods(allowedMethods)
              .AllowAnyHeader()
              .WithExposedHeaders("X-Correlation-Id");
    });
});

// Rate limiting
var rateLimitOptions = builder.Configuration.GetSection("RateLimiting");
if (rateLimitOptions.GetValue<bool>("Enabled"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return new ValueTask();
        };
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Document Classifier API", Version = "v1" });

    // Add JWT bearer authorization to Swagger if authentication is enabled
    if (authOptions.GetValue<bool>("Enabled"))
    {
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Azure AD Bearer token"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] { }
            }
        });
    }
});

var app = builder.Build();

// Global exception handler (must be first)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Security headers middleware
app.Use(async (context, next) =>
{
    // Prevent MIME sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // Prevent clickjacking
    context.Response.Headers["X-Frame-Options"] = "DENY";

    // XSS protection (modern browsers use CSP, but keep for legacy)
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

    // HSTS (HTTP Strict Transport Security)
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    // Content Security Policy
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';";

    // Referrer Policy
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    await next();
});

// HTTPS enforcement in production
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

// Rate limiting (if enabled)
if (rateLimitOptions.GetValue<bool>("Enabled"))
{
    app.UseRateLimiter();
}

app.UseCors("DocumentClassifierPolicy");

// Authentication and Authorization
if (authOptions.GetValue<bool>("Enabled"))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

// Seed default classification profiles
var profileStore = app.Services.GetRequiredService<IProfileStore>();
SeedProfiles(profileStore);

// Ensure search index exists if configured
var searchOptions = app.Services.GetRequiredService<IOptions<SearchOptions>>().Value;
var workflowOptions = app.Services.GetRequiredService<IOptions<WorkflowOptions>>().Value;
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

startupLogger.LogInformation(
    "Workflow RAG flags: Examples={EnableRagExamples}, Indexing={EnableRagIndexing}, Query={EnableRagQuery}",
    workflowOptions.EnableRagExamples,
    workflowOptions.EnableRagIndexing,
    workflowOptions.EnableRagQuery);

if (workflowOptions.EnableRagIndexing
    && !string.IsNullOrWhiteSpace(searchOptions.Endpoint)
    && !searchOptions.Endpoint.Contains("YOUR-SEARCH", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var searchService = app.Services.GetRequiredService<ISearchIndexingService>();
        await searchService.EnsureIndexExistsAsync();
        startupLogger.LogInformation("Search index initialized successfully.");
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Search index initialization failed. API will continue running without RAG indexing.");
    }
}
else if (!workflowOptions.EnableRagIndexing)
{
    startupLogger.LogInformation("RAG indexing is disabled by workflow configuration. Skipping search index initialization.");
}
else
{
    startupLogger.LogInformation("Search endpoint not configured. Skipping index initialization.");
}

if (authOptions.GetValue<bool>("Enabled"))
{
    startupLogger.LogInformation("Authentication enabled. Entra ID authentication is active.");
}
else
{
    startupLogger.LogWarning("Authentication is disabled. Consider enabling for production environments.");
}

app.Run();

static void SeedProfiles(IProfileStore store)
{
    const string defaultProfileName = "relief_request_binary";
    if (store.GetProfile(defaultProfileName) is not null)
        return;

    store.AddOrUpdate(new DocumentClassifier.Models.ClassificationProfile
    {
        Name = defaultProfileName,
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
