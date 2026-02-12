namespace AvaGodots.Models;

/// <summary>
/// 远程编辑器版本信息（从 GitHub Releases 获取）
/// </summary>
public class RemoteEditorVersion
{
    /// <summary>
    /// 版本名称（如 "4.3-stable"）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 标签名（如 "4.3-stable"）
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// 是否为预发布版本
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    /// 是否为 Mono 版本
    /// </summary>
    public bool IsMono { get; set; }

    /// <summary>
    /// 下载链接
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 主版本号（如 "4"）
    /// </summary>
    public string MajorVersion { get; set; } = string.Empty;
}
