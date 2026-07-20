using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentClassifier.Tests.Services;

public class ClassificationServiceTests
{
    [Fact]
    public void ClassifyWithHeuristic_DetectsReliefRequests()
    {
        // Test that the heuristic fallback detects relief-seeking language
        // This uses reflection to test the private method
        var options = new Microsoft.Extensions.Options.OptionsWrapper<AzureOpenAIOptions>(
            new AzureOpenAIOptions { Endpoint = "", DeploymentName = "" }
        );
        var resOptions = new Microsoft.Extensions.Options.OptionsWrapper<ResilienceOptions>(
            new ResilienceOptions { MaxRetries = 1, BaseDelayMs = 1, MaxDelayMs = 1, OperationTimeoutSeconds = 5 }
        );

        var service = new ClassificationService(options, resOptions, new Azure.Identity.DefaultAzureCredential(), NullLogger<ClassificationService>.Instance);

        var profile = new ClassificationProfile
        {
            Name = "binary",
            Description = "Test",
            SystemPrompt = "Classify",
            Categories = new List<string> { "asks_for_relief", "no_relief_requested" },
            CategoryExamples = new Dictionary<string, List<string>>
            {
                ["asks_for_relief"] = new List<string> { "grant", "request", "ask the court" },
                ["no_relief_requested"] = new List<string> { "information only", "notice" }
            }
        };

        // Test relieftext
        var reliefText = "I respectfully request that the Court grant my motion.";

        // We need to use reflection since the method is private
        var method = typeof(ClassificationService).GetMethod("ClassifyWithHeuristic",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);

        var result = (ClassificationResult?)method?.Invoke(service, new object[] { reliefText, profile });
        Assert.NotNull(result);
        Assert.Equal("asks_for_relief", result!.Category);
        Assert.True(result.Confidence >= 0.5);
    }

    [Fact]
    public void ClassifyWithHeuristic_DetectsNonReliefDocuments()
    {
        var options = new Microsoft.Extensions.Options.OptionsWrapper<AzureOpenAIOptions>(
            new AzureOpenAIOptions { Endpoint = "", DeploymentName = "" }
        );
        var resOptions = new Microsoft.Extensions.Options.OptionsWrapper<ResilienceOptions>(
            new ResilienceOptions { MaxRetries = 1, BaseDelayMs = 1, MaxDelayMs = 1, OperationTimeoutSeconds = 5 }
        );

        var service = new ClassificationService(options, resOptions, new Azure.Identity.DefaultAzureCredential(), NullLogger<ClassificationService>.Instance);

        var profile = new ClassificationProfile
        {
            Name = "binary",
            Description = "Test",
            SystemPrompt = "Classify",
            Categories = new List<string> { "asks_for_relief", "no_relief_requested" },
            CategoryExamples = new Dictionary<string, List<string>>
            {
                ["asks_for_relief"] = new List<string> { "grant", "request", "ask the court" },
                ["no_relief_requested"] = new List<string> { "information only", "notice" }
            }
        };

        var noReliefText = "This is notice that the hearing is scheduled for next month.";

        var method = typeof(ClassificationService).GetMethod("ClassifyWithHeuristic",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);

        var result = (ClassificationResult?)method?.Invoke(service, new object[] { noReliefText, profile });
        Assert.NotNull(result);
        Assert.Equal("no_relief_requested", result!.Category);
    }
}
