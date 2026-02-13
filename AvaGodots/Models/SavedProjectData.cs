using System.Collections.Generic;

namespace AvaGodots.Models;

/// <summary>
/// 持久化存储的项目数据
/// </summary>
public class SavedProjectData
{
    public string Path { get; set; } = string.Empty;
    public string? EditorPath { get; set; }
    public bool IsFavorite { get; set; }
    public bool ShowEditWarning { get; set; } = true;
    public List<CustomCommand>? CustomCommands { get; set; }
}
