using System.Collections.Generic;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Interfaces;

/// <summary>
/// 配置服务接口
/// 负责应用配置的读取和保存
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// 获取应用配置
    /// </summary>
    AppConfig Config { get; }

    /// <summary>
    /// 加载配置
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// 保存配置
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 获取应用数据目录路径
    /// </summary>
    string GetAppDataPath();
}
