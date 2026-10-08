using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Paster;

public sealed class ClipPage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public ObservableCollection<ClipItem> Items { get; set; } = new();
}
