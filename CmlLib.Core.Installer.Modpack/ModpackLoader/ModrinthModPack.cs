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

public sealed class ModrinthModPack : IModPack, IAsyncDisposable
{
    private readonly string _zipPath;
    private string? _extractDir;
    private ModrinthManifest? _manifest;

    /* =========================
     * IModPack Metadata
     * ========================= */
    public string Provider => "modrinth";
    public string Name => _manifest?.name ?? "Unknown";
    public string Version => _manifest?.versionId ?? "Unknown";
    public string MinecraftVersion => _manifest?.dependencies.minecraft ?? "Unknown";
    public int RecommendedRam => 1024; // Modrinth는 별도 필드 없음 → 기본값

    public string? ForgeVersion => _manifest?.dependencies.forge;
    public string? FabricVersion => _manifest?.dependencies.fabric_loader;

    public ModrinthModPack(string zipPath)
    {
        _zipPath = zipPath;
    }
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
    
    public static async Task<ModrinthModPack> FromUrlAsync(
        string url,
        IProgress<double>? progress = null)
    {
        var zipPath = await DownloadZipAsync(url, progress);
        var pack = new ModrinthModPack(zipPath);
        await pack.LoadAsync();
        return pack;
    }

    /* =========================
     * Load
     * ========================= */
    public async Task LoadAsync()
    {
        _extractDir = Path.Combine(
            Path.GetTempPath(),
            "hwi-modrinth",
            Guid.NewGuid().ToString("N"));

        ZipFile.ExtractToDirectory(_zipPath, _extractDir);

        var manifestPath = Path.Combine(_extractDir, "modrinth.index.json");
        await using var fs = File.OpenRead(manifestPath);

        _manifest = await JsonSerializer.DeserializeAsync<ModrinthManifest>(fs)
            ?? throw new InvalidDataException("Invalid modrinth.index.json");
    }

    /* =========================
     * Install (IModPack)
     * ========================= */
    public async Task<string> InstallAsync(ModPackInstallOptions options)
    {
        var process = await InstallAndBuildProcessAsync(options);
        return process.StartInfo.Arguments;
    }

    public async Task DownloadModsFromManifestAsync(string gameDir)
    {
        await DownloadModsAsync(gameDir);
    }

    /* =========================
     * Install + Build
     * ========================= */
    public async Task<Process> InstallAndBuildProcessAsync(ModPackInstallOptions options)
    {
        if (_manifest == null || _extractDir == null)
            throw new InvalidOperationException("Call LoadAsync first");

        var instanceDir = options.GameDirectory;
        Directory.CreateDirectory(instanceDir);

        var mcPath = new MinecraftPath(instanceDir);
        var launcher = new MinecraftLauncher(mcPath);

        string versionName;

        // ===== Forge =====
        if (ForgeVersion != null)
        {
            var installer = new ForgeInstaller(launcher);
            versionName = await installer.Install(
                MinecraftVersion,
                ForgeVersion,
                new ForgeInstallOptions
                {
                    FileProgress = options.FileProgress,
                    ByteProgress = options.ByteProgress
                });

            await launcher.InstallAsync(versionName, options.FileProgress, options.ByteProgress);
        }
        // ===== Fabric =====
        else if (FabricVersion != null)
        {
            var installer = new FabricInstaller(new HttpClient());
            versionName = await installer.Install(
                MinecraftVersion,
                FabricVersion,
                mcPath);

            await launcher.InstallAsync(versionName, options.FileProgress, options.ByteProgress);
        }
        else
        {
            throw new InvalidDataException("Unsupported mod loader");
        }

        await InstallOverridesAsync(instanceDir);
        await DownloadModsAsync(instanceDir);

        return await launcher.BuildProcessAsync(versionName, options);
    }

    /* =========================
     * Overrides
     * ========================= */
    private async Task InstallOverridesAsync(string gameDir)
    {
        var dir = Path.Combine(_extractDir!, "overrides");
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

    /* =========================
     * Mods download
     * ========================= */
    private async Task DownloadModsAsync(string gameDir)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        foreach (var file in _manifest!.files)
        {
            var target = Path.Combine(gameDir, file.path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target)) continue;

            await using var src = await http.GetStreamAsync(file.downloads[0]);
            await using var dst = File.Create(target);
            await src.CopyToAsync(dst);
        }
    }

    /* =========================
     * Cleanup
     * ========================= */
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
