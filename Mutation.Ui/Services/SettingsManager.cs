using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Storage;
using CognitiveSupport;

namespace Mutation.Ui.Services;

/// <summary>
/// Basic settings manager for WinUI that stores settings as JSON in the local folder.
/// </summary>
public class SettingsManager : ISettingsManager
{
private const string FileName = "Mutation.json";
private readonly JsonSerializerSettings _jsonSettings = new() { Formatting = Formatting.Indented };

public async Task<Settings> LoadAsync()
{
StorageFile file = await GetFileAsync();
string json = await FileIO.ReadTextAsync(file);
return JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
}

public async Task SaveAsync(Settings settings)
{
StorageFile file = await GetFileAsync();
string json = JsonConvert.SerializeObject(settings, _jsonSettings);
await FileIO.WriteTextAsync(file, json);
}

private static async Task<StorageFile> GetFileAsync()
{
StorageFolder folder = ApplicationData.Current.LocalFolder;
try
{
return await folder.GetFileAsync(FileName);
}
catch (FileNotFoundException)
{
return await folder.CreateFileAsync(FileName, CreationCollisionOption.OpenIfExists);
}
}
}

public interface ISettingsManager
{
Task<Settings> LoadAsync();
Task SaveAsync(Settings settings);
}
