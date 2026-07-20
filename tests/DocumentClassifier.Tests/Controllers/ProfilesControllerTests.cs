using DocumentClassifier.Controllers;
using DocumentClassifier.Models;
using DocumentClassifier.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DocumentClassifier.Tests.Controllers;

public class ProfilesControllerTests
{
    private readonly IProfileStore _profileStore;
    private readonly ProfilesController _controller;

    public ProfilesControllerTests()
    {
        _profileStore = new InMemoryProfileStore();
        _controller = new ProfilesController(_profileStore);
    }

    [Fact]
    public void GetAll_ReturnsEmptyListWhenNoProfiles()
    {
        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsAssignableFrom<IReadOnlyList<ClassificationProfile>>(okResult.Value);
        Assert.Empty(profiles);
    }

    [Fact]
    public void GetAll_ReturnsAllProfiles()
    {
        // Arrange
        var profile1 = new ClassificationProfile
        {
            Name = "profile1",
            Description = "Test profile 1",
            SystemPrompt = "You are a classifier",
            Categories = new List<string> { "cat1", "cat2" }
        };
        var profile2 = new ClassificationProfile
        {
            Name = "profile2",
            Description = "Test profile 2",
            SystemPrompt = "You are another classifier",
            Categories = new List<string> { "cat3" }
        };

        _profileStore.AddOrUpdate(profile1);
        _profileStore.AddOrUpdate(profile2);

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var profiles = Assert.IsAssignableFrom<IReadOnlyList<ClassificationProfile>>(okResult.Value);
        Assert.Equal(2, profiles.Count);
    }

    [Fact]
    public void Get_ReturnsProfileWhenExists()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "test_profile",
            Description = "Test",
            SystemPrompt = "Prompt",
            Categories = new List<string> { "cat1" }
        };
        _profileStore.AddOrUpdate(profile);

        // Act
        var result = _controller.Get("test_profile");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedProfile = Assert.IsType<ClassificationProfile>(okResult.Value);
        Assert.Equal("test_profile", returnedProfile.Name);
    }

    [Fact]
    public void Get_ReturnsNotFoundWhenProfileDoesNotExist()
    {
        // Act
        var result = _controller.Get("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void CreateOrUpdate_ReturnsOkWhenProfileIsValid()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "new_profile",
            Description = "New profile",
            SystemPrompt = "System prompt text",
            Categories = new List<string> { "cat1", "cat2" }
        };

        // Act
        var result = _controller.CreateOrUpdate("new_profile", profile);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedProfile = Assert.IsType<ClassificationProfile>(okResult.Value);
        Assert.Equal("new_profile", returnedProfile.Name);
        Assert.NotNull(_profileStore.GetProfile("new_profile"));
    }

    [Fact]
    public void CreateOrUpdate_ReturnsBadRequestWhenNameMismatch()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "profile1",
            Description = "Test",
            SystemPrompt = "Prompt",
            Categories = new List<string> { "cat1" }
        };

        // Act
        var result = _controller.CreateOrUpdate("profile2", profile);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void CreateOrUpdate_ReturnsBadRequestWhenNoCategoriesProvided()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "profile",
            Description = "Test",
            SystemPrompt = "Prompt",
            Categories = new List<string>()
        };

        // Act
        var result = _controller.CreateOrUpdate("profile", profile);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void CreateOrUpdate_ReturnsBadRequestWhenSystemPromptMissing()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "profile",
            Description = "Test",
            SystemPrompt = "",
            Categories = new List<string> { "cat1" }
        };

        // Act
        var result = _controller.CreateOrUpdate("profile", profile);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Delete_ReturnsNoContentWhenProfileExists()
    {
        // Arrange
        var profile = new ClassificationProfile
        {
            Name = "to_delete",
            Description = "Will be deleted",
            SystemPrompt = "Prompt",
            Categories = new List<string> { "cat1" }
        };
        _profileStore.AddOrUpdate(profile);

        // Act
        var result = _controller.Delete("to_delete");

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Null(_profileStore.GetProfile("to_delete"));
    }

    [Fact]
    public void Delete_ReturnsNotFoundWhenProfileDoesNotExist()
    {
        // Act
        var result = _controller.Delete("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
