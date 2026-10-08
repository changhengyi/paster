using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Paster;

public sealed class ClipStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ClipsJsonPath { get; }

    public ClipStorage(string clipsJsonPath)
    {
        ClipsJsonPath = clipsJsonPath;
    }

    public ObservableCollection<ClipPage> Load()
    {
        if (!File.Exists(ClipsJsonPath))
        {
            return new ObservableCollection<ClipPage>();
        }

        var raw = File.ReadAllText(ClipsJsonPath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ObservableCollection<ClipPage>();
        }

        try
        {
            var pages = JsonSerializer.Deserialize<List<ClipPage>>(raw) ?? new List<ClipPage>();
            return new ObservableCollection<ClipPage>(pages);
        }
        catch (JsonException)
        {
            System.Windows.MessageBox.Show(
                "clips.json 分页格式无效，未能解析 JSON。将以空列表打开。",
                "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return new ObservableCollection<ClipPage>();
        }
    }

    public void Save(IEnumerable<ClipPage> pages)
    {
        var dir = Path.GetDirectoryName(ClipsJsonPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var payload = JsonSerializer.Serialize(pages, JsonOptions);
        File.WriteAllText(ClipsJsonPath, payload);
    }
}
