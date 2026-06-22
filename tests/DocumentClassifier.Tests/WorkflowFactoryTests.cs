using DocumentClassifier.Services;
using DocumentClassifier.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocumentClassifier.Tests;

public class WorkflowFactoryTests
{
    [Fact]
    public void ConfidenceThreshold_ComesFromOptions()
    {
        var threshold = 0.81;
        var factory = new DocumentClassificationWorkflowFactory(
            Mock.Of<IDocumentStorageService>(),
            Mock.Of<ITextExtractionService>(),
            Mock.Of<IClassificationService>(),
            Mock.Of<IProfileStore>(),
            Mock.Of<ISearchIndexingService>(),
            Mock.Of<IRagService>(),
            Mock.Of<IReviewQueueStore>(),
            Options.Create(new WorkflowOptions { ConfidenceThreshold = threshold }));

        Assert.Equal(threshold, factory.ConfidenceThreshold, 3);
    }

    [Fact]
    public void Build_ReturnsWorkflowInstance()
    {
        var factory = new DocumentClassificationWorkflowFactory(
            Mock.Of<IDocumentStorageService>(),
            Mock.Of<ITextExtractionService>(),
            Mock.Of<IClassificationService>(),
            Mock.Of<IProfileStore>(),
            Mock.Of<ISearchIndexingService>(),
            Mock.Of<IRagService>(),
            Mock.Of<IReviewQueueStore>(),
            Options.Create(new WorkflowOptions { ConfidenceThreshold = 0.7 }));

        var workflow = factory.Build();

        Assert.NotNull(workflow);
    }
}
