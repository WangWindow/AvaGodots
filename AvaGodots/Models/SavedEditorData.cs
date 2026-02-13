using System.Collections.Generic;

namespace AvaGodots.Models;

/// <summary>
/// 持久化存储的编辑器数据
/// </summary>
public class SavedEditorData
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> ExtraArguments { get; set; } = [];
    public string VersionHint { get; set; } = string.Empty;
    public List<CustomCommand> CustomCommands { get; set; } = [];
}
