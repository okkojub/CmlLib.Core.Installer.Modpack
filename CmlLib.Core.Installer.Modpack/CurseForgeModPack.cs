using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Forge;

namespace CmlLib.Core.Installer.Modpack;

public sealed class CurseForgeModPack : IModPack, IAsyncDisposable
{
    private readonly string _zipPath;
    private string? _extractDir;
    private CurseForgeManifest? _manifest;

    public CurseForgeModPack(string zipPath)
    {
        _zipPath = zipPath ?? throw new ArgumentNullException(nameof(zipPath));
    }

    // =========================
    // Metadata
    // =========================
    public string Provider => "curseforge";
    public string Name => _manifest?.name ?? "Unknown";
    public string Version => _manifest?.version ?? "Unknown";
    public string MinecraftVersion => _manifest?.minecraft.version ?? "Unknown";
    public int RecommendedRam => _manifest?.minecraft.recommendedRam ?? 512;

    // =========================
    // Load
    // =========================
    public async Task LoadAsync()
    {
        _extractDir = Path.Combine(
            Path.GetTempPath(),
            "hwi-modpack",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_extractDir);

        // ZIP extract (async)
        await ZipFile.ExtractToDirectoryAsync(_zipPath, _extractDir);

        var manifestPath = Path.Combine(_extractDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("manifest.json not found");

        await using var stream = File.OpenRead(manifestPath);
        _manifest = await JsonSerializer.DeserializeAsync<CurseForgeManifest>(stream)
            ?? throw new InvalidDataException("Invalid manifest.json");
    }

    // =========================
    // Install
    // =========================
    public async Task<string> InstallAsync(ModPackInstallOptions options)
    {
        if (_manifest == null || _extractDir == null)
            throw new InvalidOperationException("Call LoadAsync() first");

        if (string.IsNullOrWhiteSpace(options.GameDirectory))
            throw new ArgumentException("GameDirectory is required", nameof(options));

        // 인스턴스 루트 = 독립 .minecraft
        var instanceDir = options.GameDirectory;
        Directory.CreateDirectory(instanceDir);

        var mcPath = new MinecraftPath(instanceDir);
        var launcher = new MinecraftLauncher(mcPath);

        // Forge loader 선택
        var loader = _manifest.minecraft.modLoaders.Find(x => x.primary)
            ?? throw new InvalidDataException("No primary mod loader");

        if (!loader.id.StartsWith("forge-"))
            throw new NotSupportedException($"Unsupported loader: {loader.id}");

        var forgeVersion = loader.id["forge-".Length..];

        // Forge 설치
        var forgeInstaller = new ForgeInstaller(launcher);

        var versionName = await forgeInstaller.Install(
            _manifest.minecraft.version,
            forgeVersion,
            new ForgeInstallOptions
            {
                FileProgress = options.FileProgress,
                ByteProgress = options.ByteProgress
            });

        // Minecraft 본체 설치
        await launcher.InstallAsync(
            versionName,
            options.FileProgress,
            options.ByteProgress);

        // overrides 적용
        await InstallOverridesAsync(instanceDir);

        return versionName;
    }
    /// <summary>
    /// ModPack 설치 후 바로 Minecraft 프로세스를 생성
    /// </summary>
    /// <param name="options">ModPack 설치 옵션</param>
    /// <returns>실행 가능한 Process</returns>
    public async Task<Process> InstallAndBuildProcessAsync(ModPackInstallOptions options)
    {
        if (_manifest == null || _extractDir == null)
            throw new InvalidOperationException("Call LoadAsync() first");

        // 1. Minecraft + Forge 설치
        string versionName = await InstallAsync(options);

        // 2. 모드 다운로드
        await DownloadModsFromManifestAsync(options.GameDirectory);

        // 3. CmlLib ProcessBuilder 생성
        var mcPath = new MinecraftPath(options.GameDirectory);
        var launcher = new MinecraftLauncher(mcPath);

        var processBuilder = await launcher.BuildProcessAsync(versionName, options);

        return processBuilder;
    }

    // =========================
    // Overrides
    // =========================
    private async Task InstallOverridesAsync(string gameDir)
    {
        var overridesDir = Path.Combine(
            _extractDir!,
            _manifest!.overrides ?? "overrides");

        if (!Directory.Exists(overridesDir))
            return;

        var files = Directory.GetFiles(
            overridesDir, "*", SearchOption.AllDirectories);

        await Parallel.ForEachAsync(files, async (file, _) =>
        {
            var relative = Path.GetRelativePath(overridesDir, file);
            var target = Path.Combine(gameDir, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var src = File.OpenRead(file);
            await using var dst = File.Create(target);
            await src.CopyToAsync(dst);
        });
    }

    // =========================
    // Mods download (manifest 기반)
    // =========================
    public async Task DownloadModsFromManifestAsync(string gameDir)
    {
        if (_manifest == null)
            throw new InvalidOperationException("Call LoadAsync() first");

        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        foreach (var file in _manifest.files)
        {
            if (!file.required) continue;

            var targetFile = Path.Combine(modsDir, $"{file.projectID}-{file.fileID}.jar");
            if (File.Exists(targetFile))
            {
                Console.WriteLine($"[SKIP] {targetFile}");
                continue;
            }

            var downloadUrl = $"https://www.curseforge.com/api/v1/mods/{file.projectID}/files/{file.fileID}/download";

            try
            {
                await using var src = await http.GetStreamAsync(downloadUrl);
                await using var dst = File.Create(targetFile);
                await src.CopyToAsync(dst);
                Console.WriteLine($"[OK] {targetFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {file.projectID}-{file.fileID}: {ex.Message}");
            }
        }
    }

    // =========================
    // Cleanup
    // =========================
    public ValueTask DisposeAsync()
    {
        try
        {
            if (_extractDir != null && Directory.Exists(_extractDir))
                Directory.Delete(_extractDir, true);
        }
        catch
        {
            // cleanup 실패는 무시
        }

        return ValueTask.CompletedTask;
    }
}
