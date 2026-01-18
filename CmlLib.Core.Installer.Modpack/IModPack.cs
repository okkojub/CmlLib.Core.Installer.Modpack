namespace CmlLib.Core.Installer.Modpack;

using CmlLib.Core;
using CmlLib.Core.Installer;
using System.Threading.Tasks;

public interface IModPack
{
    /// <summary>
    /// ModPack 고유 ID (ex: curseforge, modrinth)
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// 팩 이름
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 팩 버전
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Minecraft 버전
    /// </summary>
    string MinecraftVersion { get; }

    /// <summary>
    /// 권장 메모리 (MB)
    /// </summary>
    int RecommendedRam { get; }

    /// <summary>
    /// modpack.zip 파싱 (manifest 등)
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// ModPack 설치
    /// - Forge/Fabric 설치
    /// - mods 다운로드
    /// - overrides 적용
    /// </summary>
    Task<string> InstallAsync(
        ModPackInstallOptions options);

    Task DownloadModsFromManifestAsync(string gameDir);
}