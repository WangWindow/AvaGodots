using System.Collections.Generic;
using System.Text.Json.Serialization;
using AvaGodots.Models;

namespace AvaGodots.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(List<SavedEditorData>))]
[JsonSerializable(typeof(List<SavedProjectData>))]
[JsonSerializable(typeof(AssetLibResult))]
[JsonSerializable(typeof(AssetLibConfig))]
[JsonSerializable(typeof(AssetLibItem))]
[JsonSerializable(typeof(GithubRelease))]
[JsonSerializable(typeof(List<GithubReleaseAsset>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
