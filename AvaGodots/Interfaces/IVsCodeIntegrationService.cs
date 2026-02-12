using System.Threading.Tasks;

namespace AvaGodots.Interfaces;

/// <summary>
/// VS Code 集成服务接口
/// 负责在项目中创建和管理 .vscode 配置
/// </summary>
public interface IVsCodeIntegrationService
{
    /// <summary>
    /// 为项目创建 .vscode 目录和配置文件
    /// </summary>
    /// <param name="projectDir">项目目录路径</param>
    /// <param name="godotEditorPath">Godot 编辑器可执行文件路径</param>
    Task SetupVsCodeAsync(string projectDir, string godotEditorPath);

    /// <summary>
    /// 更新项目的 VS Code 配置中的 Godot 路径
    /// </summary>
    /// <param name="projectDir">项目目录路径</param>
    /// <param name="godotEditorPath">Godot 编辑器可执行文件路径</param>
    Task UpdateGodotPathAsync(string projectDir, string godotEditorPath);

    /// <summary>
    /// 检查项目是否已有 VS Code 配置
    /// </summary>
    /// <param name="projectDir">项目目录路径</param>
    bool HasVsCodeConfig(string projectDir);
}
