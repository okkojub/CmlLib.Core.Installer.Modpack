using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ModLoaders.FabricMC;

namespace CmlLib.Core.Installer.Modpack.ModpackLoader;

public sealed class CurseForgeModPack : IAsyncDisposable
{
    private readonly string _zipPath;
    private string? _extractDir;
    private CurseForgeManifest? _manifest;

    private CurseForgeModPack(string zipPath)
    {
        _zipPath = zipPath;
    }

    // =========================
    // Factory (ZIP 다운로드 포함)
    // =========================
    public static async Task<CurseForgeModPack> FromUrlAsync(
        string url,
        IProgress<double>? progress = null)
    {
        var zipPath = await DownloadZipAsync(url, progress);
        var pack = new CurseForgeModPack(zipPath);
        await pack.LoadAsync();
        return pack;
    }

    // =========================
    // Metadata
    // =========================
    public string Name => _manifest?.name ?? "Unknown";
    public string Version => _manifest?.version ?? "Unknown";
    public string MinecraftVersion => _manifest?.minecraft.version ?? "Unknown";
    public int RecommendedRam => _manifest?.minecraft.recommendedRam ?? 512;

    // =========================
    // Load
    // =========================
    private async Task LoadAsync()
    {
        _extractDir = Path.Combine(
            Path.GetTempPath(),
            "hwi-modpack",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_extractDir);

        await ZipFile.ExtractToDirectoryAsync(_zipPath, _extractDir);

        var manifestPath = Path.Combine(_extractDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("manifest.json not found");

        await using var stream = File.OpenRead(manifestPath);
        _manifest = await JsonSerializer.DeserializeAsync<CurseForgeManifest>(stream)
            ?? throw new InvalidDataException("Invalid manifest.json");
    }

    // =========================
    // Install directory
    // =========================
    public string GetInstallDirectory(string rootDir)
    {
        if (_manifest == null)
            throw new InvalidOperationException("Manifest not loaded");

        return Path.Combine(rootDir, $"{Name}_{Version}");
    }

    // =========================
    // Install + Build process
    // =========================
    public async Task<Process> InstallAndBuildProcessAsync(ModPackInstallOptions options)
    {
        if (_manifest == null || _extractDir == null)
            throw new InvalidOperationException("Call LoadAsync first");

        var instanceDir = options.GameDirectory;
        Directory.CreateDirectory(instanceDir);

        var mcPath = new MinecraftPath(instanceDir);
        var launcher = new MinecraftLauncher(mcPath);

        var loader = _manifest.minecraft.modLoaders.Find(x => x.primary)
            ?? throw new InvalidDataException("No primary mod loader");

        string versionName;

        // ===== Forge =====
        if (loader.id.StartsWith("forge-"))
        {
            var forgeVersion = loader.id["forge-".Length..];
            var installer = new ForgeInstaller(launcher);

            versionName = await installer.Install(
                _manifest.minecraft.version,
                forgeVersion,
                new ForgeInstallOptions
                {
                    FileProgress = options.FileProgress,
                    ByteProgress = options.ByteProgress
                });

            await launcher.InstallAsync(
                versionName,
                options.FileProgress,
                options.ByteProgress);
        }
        // ===== Fabric =====
        else if (loader.id.StartsWith("fabric-"))
        {
            var fabricVersion = loader.id["fabric-".Length..];
            var installer = new FabricInstaller(new HttpClient());

            versionName = await installer.Install(
                _manifest.minecraft.version,
                fabricVersion,
                mcPath);

            await launcher.InstallAsync(
                versionName,
                options.FileProgress,
                options.ByteProgress);
        }
        else
        {
            throw new NotSupportedException($"Unsupported loader: {loader.id}");
        }

        await InstallOverridesAsync(instanceDir);
        await DownloadModsAsync(instanceDir);

        var process = await launcher.BuildProcessAsync(versionName, options);
        return process;
    }

    // =========================
    // Overrides
    // =========================
    private async Task InstallOverridesAsync(string gameDir)
    {
        var dir = Path.Combine(_extractDir!, _manifest!.overrides ?? "overrides");
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);

        await Parallel.ForEachAsync(files, async (file, _) =>
        {
            var rel = Path.GetRelativePath(dir, file);
            var dst = Path.Combine(gameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

            await using var src = File.OpenRead(file);
            await using var dest = File.Create(dst);
            await src.CopyToAsync(dest);
        });
    }

    // =========================
    // Mods download
    // =========================
    private async Task DownloadModsAsync(string gameDir)
    {
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        foreach (var file in _manifest!.files)
        {
            if (!file.required) continue;

            var target = Path.Combine(modsDir, $"{file.projectID}-{file.fileID}.jar");
            if (File.Exists(target)) continue;

            var url =
                $"https://www.curseforge.com/api/v1/mods/{file.projectID}/files/{file.fileID}/download";

            await using var src = await http.GetStreamAsync(url);
            await using var dst = File.Create(target);
            await src.CopyToAsync(dst);
        }
    }

    // =========================
    // ZIP download
    // =========================
    private static async Task<string> DownloadZipAsync(
        string url,
        IProgress<double>? progress)
    {
        var target = Path.Combine(
            Path.GetTempPath(),
            "modpacks",
            Path.GetFileName(new Uri(url).LocalPath));

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var http = new HttpClient();
        using var response = await http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var canReport = total > 0 && progress != null;

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(target);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            readTotal += read;

            if (canReport)
                progress!.Report(readTotal * 100d / total);
        }

        progress?.Report(100);
        return target;
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
        catch { }

        return ValueTask.CompletedTask;
    }
}
