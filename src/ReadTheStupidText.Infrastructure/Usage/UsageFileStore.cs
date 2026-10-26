using ReadTheStupidText.Application.Usage;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Usage;

/// <summary>
/// Keeps the usage counts' state in the app's local data (Slice 140): the user's switch in <see cref="ApplicationData.LocalSettings"/>
/// (on by default), the queue and the last good remote config as files in the <c>usage</c> folder of <see cref="ApplicationData.LocalFolder"/>
/// (a setting value is capped at 8 KB, a full queue is not). Nothing here holds an id or any text the person read.
/// </summary>
public sealed class UsageFileStore : IUsageStore
{
    private const string EnabledKey = "UsageCountsEnabled";
    private const string Folder = "usage";
    private const string QueueFile = "queue.json";
    private const string ConfigFile = "config.json";

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
    private readonly string _folder = Path.Combine(ApplicationData.Current.LocalFolder.Path, Folder);

    public bool Enabled
    {
        get => _settings.Values[EnabledKey] as bool? ?? true;
        set => _settings.Values[EnabledKey] = value;
    }

    public string? LoadQueue() => Read(QueueFile);

    public void SaveQueue(string? json)
    {
        if (json is null)
        {
            Delete(QueueFile);
            return;
        }

        Write(QueueFile, json);
    }

    public string? LoadConfig() => Read(ConfigFile);

    public void SaveConfig(string json) => Write(ConfigFile, json);

    private string? Read(string name)
    {
        string path = Path.Combine(_folder, name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    // Written to a temp file and moved over, so a crash mid-write never leaves a half queue.
    private void Write(string name, string content)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, name);
        string temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private void Delete(string name)
    {
        string path = Path.Combine(_folder, name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
