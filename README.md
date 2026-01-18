# CurseForge ModPack Parser for CmlLib

[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**CmlLib.Core.Installer.Modpack**는 [CmlLib](https://github.com/CmlLib/CmlLib.Core)용 에드온(Add-on) 프로젝트로,  
CurseForge에서 제공하는 Minecraft 모드팩(`.zip`)을 손쉽게 읽고 설치할 수 있도록 도와줍니다.

> ⚡ **주의:** 이 프로젝트는 CmlLib Minecraft 런처 라이브러리를 기반으로 동작합니다.  
> 독립 실행형 런처가 아닌, CmlLib를 사용하는 프로젝트에서 모드팩 관리 기능을 확장하는 용도입니다.

---

## 주요 기능

- CurseForge 모드팩 ZIP 파일 읽기 (`manifest.json` 기반)
- Minecraft 버전 및 Forge 로더 자동 감지
- Forge 설치 및 Minecraft 런처 통합
- `overrides` 폴더 자동 적용
- Manifest에 정의된 모드 파일 다운로드 및 설치
- `ModPackInstallOptions` 기반 설치 진행 상태 추적 (파일/바이트 단위)
- CmlLib `Process` 빌드 지원 → 설치 후 바로 게임 실행 가능

---

## 추후 개선사항

- Modrinth 형식의 모드팩도 가능하게
- Fabric/Forge 자동 전환
- Neoforge 지원
- FTB 형식 지원
---

## 설치

NuGet 또는 직접 프로젝트 참조 방식 사용 가능:

```bash
# 프로젝트에 직접 추가
git clone https://github.com/jwyoon1220/CurseForgeModPackParser.git
```

사용 예시
```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Modpack;

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
            Session = MSession.CreateOfflineSession("player"),
            ServerIp = "your.server",
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

```

요구사항
.NET 10 이상

CmlLib.Core (4.0.6)
CmlLib.Core.Installer.Forge(1.1.1)
CmlLib.Core.Installer.NeoForge(4.0.0)

인터넷 연결 (CurseForge 모드 다운로드용)

Windows 환경에서 테스트 완료, Linux/MacOS는 검증되지 않았습니다.

