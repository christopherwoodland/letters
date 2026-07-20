# Contributing to DocumentClassifier

Thank you for your interest in contributing to DocumentClassifier! We welcome contributions of all kinds, from bug reports and documentation improvements to code contributions.

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Getting Started

1. **Fork the repository** and clone it locally
2. **Create a new branch** for your changes: `git checkout -b feature/your-feature-name`
3. **Set up your development environment**:
   ```bash
   cd courts/letters
   dotnet restore
   npm install --prefix ui
   ```

## Development Workflow

### Running Locally

```bash
# Terminal 1: Start the .NET backend
cd src/DocumentClassifier
dotnet run

# Terminal 2: Start the React frontend
cd ui
npm run dev
```

### Authentication Setup (Optional)

By default, the API runs without authentication. To test authentication features:

1. **Enable authentication in appsettings.Development.json**:
   ```json
   {
     "Authentication": {
       "Enabled": true,
       "TenantId": "your-tenant-id",
       "ClientId": "your-client-id"
     }
   }
   ```

2. **Store secrets securely**:
   ```bash
   dotnet user-secrets set "Authentication:TenantId" "your-value"
   dotnet user-secrets set "Authentication:ClientId" "your-value"
   ```

3. **Follow the [Authentication Setup Guide](./AUTHENTICATION_SETUP.md)** for complete instructions

**Note**: Most development requires authentication disabled for ease of testing.

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test tests/DocumentClassifier.Tests/DocumentClassifier.Tests.csproj
```

### Building

```bash
# Build the solution
dotnet build

# Build for release
dotnet build --configuration Release
```

## Code Style Guidelines

This project follows [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and includes an `.editorconfig` file that enforces style consistency.

Key requirements:
- **Naming**: PascalCase for public members, _camelCase for private fields
- **Async**: Use `async/await` consistently; use CancellationToken
- **Logging**: Use ILogger for structured logging
- **Null safety**: Enable nullable reference types (already configured)
- **Documentation**: Add XML doc comments to all public APIs
- **Testing**: Write unit tests for new features; aim for >80% coverage

### Example: Properly Documented Public Method

```csharp
/// <summary>
/// Classifies text using the specified profile.
/// </summary>
/// <param name="text">The text to classify.</param>
/// <param name="profileName">The classification profile name.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>A classification result containing the category and confidence.</returns>
/// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
/// <exception cref="InvalidOperationException">Thrown when the profile is not found.</exception>
public async Task<ClassificationResult> ClassifyAsync(string text, string profileName, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(text);
    ArgumentNullException.ThrowIfNull(profileName);

    _logger.LogInformation("Classifying text with profile {ProfileName}", profileName);
    // ... implementation
}
```

## Submitting Changes

### Before You Submit

1. **Write tests** for your changes
2. **Run the full test suite**: `dotnet test`
3. **Verify code style**: The `.editorconfig` should be automatically applied
4. **Update documentation** if needed
5. **Ensure no regressions**: Test all affected endpoints/services

### Pull Request Process

1. **Push your branch** to your fork
2. **Create a Pull Request** with a clear title and description
3. **Link related issues** in the PR description
4. **Respond to review feedback** promptly
5. **Ensure CI passes** before merging

### PR Title Convention

- `feat: Add new feature description`
- `fix: Fix bug description`
- `docs: Update documentation`
- `test: Add/improve tests`
- `refactor: Refactor component`
- `chore: Update dependencies`

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
How was this tested? Include reproduction steps if applicable.

## Checklist
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] No breaking changes
- [ ] Code follows style guidelines
```

## Reporting Bugs

Create a GitHub Issue with:
- **Clear title** describing the problem
- **Steps to reproduce** the issue
- **Expected vs. actual behavior**
- **Environment** (OS, .NET version, etc.)
- **Screenshots/logs** if applicable

## Feature Requests

Open an issue with:
- **Clear description** of the feature
- **Use case** and motivation
- **Proposed implementation** (optional)
- **Alternative approaches** considered

## Questions?

Feel free to open a discussion or issue. The maintainers are here to help!

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

Thank you for contributing! 🎉
