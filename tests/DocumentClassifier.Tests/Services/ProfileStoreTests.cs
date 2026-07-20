using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Xunit;

namespace DocumentClassifier.Tests.Services;

public class ProfileStoreTests
{
    [Fact]
    public void AddOrUpdate_AddsNewProfile()
    {
        // Arrange
        var store = new InMemoryProfileStore();
        var profile = new ClassificationProfile
        {
            Name = "test",
            Description = "Test profile",
            SystemPrompt = "You are a classifier",
            Categories = new List<string> { "cat1", "cat2" }
        };

        // Act
        store.AddOrUpdate(profile);

        // Assert
        var retrieved = store.GetProfile("test");
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved!.Name);
    }

    [Fact]
    public void AddOrUpdate_UpdatesExistingProfile()
    {
        // Arrange
        var store = new InMemoryProfileStore();
        var profile1 = new ClassificationProfile
        {
            Name = "test",
            Description = "Original",
            SystemPrompt = "Prompt 1",
            Categories = new List<string> { "cat1" }
        };
        store.AddOrUpdate(profile1);

        var profile2 = new ClassificationProfile
        {
            Name = "test",
            Description = "Updated",
            SystemPrompt = "Prompt 2",
            Categories = new List<string> { "cat2" }
        };

        // Act
        store.AddOrUpdate(profile2);

        // Assert
        var retrieved = store.GetProfile("test");
        Assert.NotNull(retrieved);
        Assert.Equal("Updated", retrieved!.Description);
        Assert.Equal("Prompt 2", retrieved.SystemPrompt);
    }

    [Fact]
    public void GetAll_ReturnsAllProfiles()
    {
        // Arrange
        var store = new InMemoryProfileStore();
        var profiles = new[]
        {
            new ClassificationProfile { Name = "prof1", Description = "1", SystemPrompt = "P1", Categories = new List<string> { "c1" } },
            new ClassificationProfile { Name = "prof2", Description = "2", SystemPrompt = "P2", Categories = new List<string> { "c2" } },
            new ClassificationProfile { Name = "prof3", Description = "3", SystemPrompt = "P3", Categories = new List<string> { "c3" } }
        };

        foreach (var profile in profiles)
        {
            store.AddOrUpdate(profile);
        }

        // Act
        var result = store.GetAll();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void GetProfile_ReturnsNullWhenNotFound()
    {
        // Arrange
        var store = new InMemoryProfileStore();

        // Act
        var result = store.GetProfile("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        // Arrange
        var store = new InMemoryProfileStore();
        var profile = new ClassificationProfile
        {
            Name = "toDelete",
            Description = "Will be deleted",
            SystemPrompt = "Prompt",
            Categories = new List<string> { "cat1" }
        };
        store.AddOrUpdate(profile);

        // Act
        var deleted = store.Delete("toDelete");

        // Assert
        Assert.True(deleted);
        Assert.Null(store.GetProfile("toDelete"));
    }

    [Fact]
    public void Delete_ReturnsFalseWhenNotFound()
    {
        // Arrange
        var store = new InMemoryProfileStore();

        // Act
        var result = store.Delete("nonexistent");

        // Assert
        Assert.False(result);
    }
}
