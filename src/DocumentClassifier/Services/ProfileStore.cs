using DocumentClassifier.Models;
using System.Text.Json;

namespace DocumentClassifier.Services;

public interface IProfileStore
{
    ClassificationProfile? GetProfile(string name);
    IReadOnlyList<ClassificationProfile> GetAll();
    void AddOrUpdate(ClassificationProfile profile);
    bool Delete(string name);
}

public class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<string, ClassificationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public ClassificationProfile? GetProfile(string name) =>
        _profiles.TryGetValue(name, out var p) ? p : null;

    public IReadOnlyList<ClassificationProfile> GetAll() =>
        _profiles.Values.ToList();

    public void AddOrUpdate(ClassificationProfile profile) =>
        _profiles[profile.Name] = profile;

    public bool Delete(string name) =>
        _profiles.Remove(name);
}

public class FileBackedProfileStore : IProfileStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileBackedProfileStore()
    {
        var configuredDataDir = Environment.GetEnvironmentVariable("DOCUMENT_CLASSIFIER_DATA_DIR");
        var appData = string.IsNullOrWhiteSpace(configuredDataDir)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : configuredDataDir;
        Directory.CreateDirectory(appData);
        _path = Path.Combine(appData, "profiles.json");
    }

    public ClassificationProfile? GetProfile(string name)
    {
        lock (_gate)
        {
            var profiles = ReadAllInternal();
            return profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<ClassificationProfile> GetAll()
    {
        lock (_gate)
        {
            return ReadAllInternal();
        }
    }

    public void AddOrUpdate(ClassificationProfile profile)
    {
        lock (_gate)
        {
            var profiles = ReadAllInternal().ToList();
            var index = profiles.FindIndex(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
                profiles[index] = profile;
            else
                profiles.Add(profile);

            WriteAllInternal(profiles);
        }
    }

    public bool Delete(string name)
    {
        lock (_gate)
        {
            var profiles = ReadAllInternal().ToList();
            var removed = profiles.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
                return false;

            WriteAllInternal(profiles);
            return true;
        }
    }

    private IReadOnlyList<ClassificationProfile> ReadAllInternal()
    {
        if (!File.Exists(_path))
            return Array.Empty<ClassificationProfile>();

        try
        {
            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<ClassificationProfile>();

            return JsonSerializer.Deserialize<List<ClassificationProfile>>(json, JsonOptions)
                ?? new List<ClassificationProfile>();
        }
        catch
        {
            // Treat malformed/corrupt profile file as empty so API remains available.
            return Array.Empty<ClassificationProfile>();
        }
    }

    private void WriteAllInternal(List<ClassificationProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
