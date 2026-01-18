using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Modpack;

namespace CmlLib.Core.Installer.Modpack;

class TestMain
{
    private static async Task<string> DownloadModpackAsync(string url)
    {
        var target = Path.Combine(
            Path.GetTempPath(),
            "modpacks",
            Path.GetFileName(new Uri(url).LocalPath));

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var http = new HttpClient();
        await using var stream = await http.GetStreamAsync(url);
        await using var file = File.Create(target);
        await stream.CopyToAsync(file);

        return target;
    }

    static async Task Main()
    {
        // 1. ModPack ZIP 경로
        string zipPath = await DownloadModpackAsync("https://github.com/ihwiyun/hwiyun-discord-bot-oauth/releases/download/server/modpack.zip");

        // 2. 설치할 게임 디렉터리
        string gameDir = @"C:\users\koroutine\instances";

        await using var modpack = new CurseForgeModPack(zipPath);

        // 3. ZIP 추출 + manifest 로드
        Console.WriteLine("Loading modpack...");
        await modpack.LoadAsync();

        Console.WriteLine($"ModPack: {modpack.Name} v{modpack.Version}");
        Console.WriteLine($"Minecraft Version: {modpack.MinecraftVersion}");
        Console.WriteLine($"Recommended RAM: {modpack.RecommendedRam} MB");

        // 4. Minecraft + Forge 설치
        Console.WriteLine("Installing Minecraft and Forge...");
        var maxMem = modpack.RecommendedRam;
        var options = new ModPackInstallOptions
        {
            GameDirectory = @$"C:\users\koroutine\instances\{modpack.Name}",

            // FileProgress: InstallerProgressChangedEventArgs를 문자열로 출력
            FileProgress = new Progress<InstallerProgressChangedEventArgs>(progress =>
            {
                Console.WriteLine("[Event]");
                Console.WriteLine($"Name: {progress.Name}");
                Console.WriteLine($"Progressed: {progress.ProgressedTasks}");
                Console.WriteLine($"Total: {progress.TotalTasks}");
                Console.WriteLine($"Ratio: {progress.ProgressedTasks / progress.TotalTasks}%");
            }),

            // ByteProgress: ByteProgress를 문자열로 출력
            ByteProgress = new Progress<ByteProgress>(progress =>
                Console.WriteLine("Bytes: " + progress.ToRatio() * 100)),
            
            ServerIp = "giowqmndkl.kr",
            MaximumRamMb = maxMem,
            MinimumRamMb = 1024,
        };

        var process = await modpack.InstallAndBuildProcessAsync(options);

        Console.WriteLine($"Installed Minecraft version: {modpack.MinecraftVersion}");
        
        // 5. 모드 다운로드 (manifest 기반)
        Console.WriteLine("Downloading mods...");
        await modpack.DownloadModsFromManifestAsync(gameDir);
        
        //await modpack.DisposeAsync();
        Console.WriteLine("All done!");
        process.Start();
    }
}
