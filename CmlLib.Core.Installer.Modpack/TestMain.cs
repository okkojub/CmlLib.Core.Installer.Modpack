using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Modpack;
using CmlLib.Core.Installer.Modpack.ModpackLoader;

namespace CmlLib.Core.Installer.Modpack;

internal class TestMain
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

    public static async Task Main()
    {
        // Load modpack from URL.
        var pack = await CurseForgeModPack.FromUrlAsync("https://github.com/ihwiyun/hwiyun-discord-bot-oauth/releases/download/server/modpack.zip");
        
        // Install modpack on your computer.
        var process = await pack.InstallAndBuildProcessAsync(new ModPackInstallOptions()
        {
            ServerIp = "your.server",
            MaximumRamMb = pack.RecommendedRam,
            MinimumRamMb = 128,
            
            GameDirectory = @$"modpacks\{pack.Name}_{pack.Version}", // safe directory. don't use already used folder by other mc client
            
            Session = MSession.CreateOfflineSession("player123"), // Put Your session. see https://cmllib.github.io/CmlLib.Core-wiki/en/auth.microsoft/
            
            ByteProgress = new Progress<ByteProgress>(e =>
            {
                Console.WriteLine(e.ToRatio() * 100 + "%");
            }),
            FileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
            {
                Console.WriteLine($"Name: {e.Name}\nType: {e.EventType}\n Progressed: {e.ProgressedTasks}/{e.TotalTasks}");
            }) 
        });
        process.Start();
    }
}
