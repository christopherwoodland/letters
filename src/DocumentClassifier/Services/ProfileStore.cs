using DocumentClassifier.Models;

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
