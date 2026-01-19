# ModPack Parser for CmlLib
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

**CmlLib.Core.Installer.Modpack**은 [CmlLib](https://github.com/CmlLib/CmlLib.Core)용 에드온(Add-on) 프로젝트로,  
CurseForge에서 제공하는 Minecraft 모드팩(`.zip`)을 손쉽게 읽고 설치할 수 있도록 도와줍니다.

**CmlLib.Core.Installer.Modpack**is add-on for [CmlLib](https://github.com/CmlLib/CmlLib.Core) project,
Help easily read and install the Minecraft mode pack provided by Modpack files(`.zip`).

> ⚡ **주의:** 이 프로젝트는 CmlLib Minecraft 런처 라이브러리를 기반으로 동작합니다.  
> 독립 실행형 런처가 아닌, CmlLib를 사용하는 프로젝트에서 모드팩 관리 기능을 확장하는 용도입니다.


> ⚡ **Caution:** This project operates based on the CmlLib Minecraft launcher library.  
>  Rather than a standalone launcher, it is intended to extend modpack management capabilities in projects using CmlLib.
 

---

## 주요 기능

- CurseForge 모드팩 ZIP 파일 읽기 (`manifest.json` 기반)
- Minecraft 버전 및 Forge 로더 자동 감지
- Forge 설치 및 Minecraft 런처 통합
- `overrides` 폴더 자동 적용
- Manifest에 정의된 모드 파일 다운로드 및 설치
- `ModPackInstallOptions` 기반 설치 진행 상태 추적 (파일/바이트 단위)
- CmlLib `Process` 빌드 지원 → 설치 후 바로 게임 실행 가능

## Key Features

- Read CurseForge Modpack ZIP File (based on `manifest.json`)
- Automatic detection of Minecraft versions and Forge loaders
- Forge installation and Minecraft launcher integration
- Automatically apply the 'overrides' folder
- Download and install mode files defined in Manifest
- Tracking the progress of installation based on 'ModPack Install Options' (in file/byte)
- CmlLib 'Process' Build Support → Run the game immediately after installation

---

## 추후 개선사항

- Modrinth 형식의 모드팩도 가능하게
- Fabric/Forge 자동 전환
- Neoforge 지원
- FTB 형식 지원

## Future of this project.

- Modrinth Modpacks
- Fabric/Forge automatic select
- Neoforge Support
- FTB Support
---

## 설치 / Installation

NuGet 또는 직접 프로젝트 참조 방식 사용 가능:

use NuGet. Search `CmlLib.Core.Installer.Modpack` or You can use below script.


```bash
# 프로젝트에 직접 추가
git clone https://github.com/jwyoon1220/CurseForgeModPackParser.git
```

사용 예시 / Example
```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer;
using CmlLib.Core.Installer.Modpack;
using CmlLib.Core.Installer.Modpack.ModpackLoader;

class TestMain
{
    /// <summary>
    /// ModPack 파일 다운로드
    /// Download modpack file to temp directory
    /// </summary>
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
        // =========================
        // 1. ModPack 다운로드
        // Download modpack file
        // =========================
        string packPath = await DownloadModpackAsync(
            "https://github.com/ihwiyun/hwiyun-discord-bot-oauth/releases/download/server/modpack.zip"
            // Modrinth example:
            // "https://cdn.modrinth.com/data/xxxx/versions/yyyy/pack.mrpack"
        );

        // =========================
        // 2. ModPack 로더 선택
        // Select modpack loader by extension
        // =========================
        IModPack modpack;

        if (Util.IsModrinthModpack(packPath))
        {
            modpack = new ModrinthModPack(packPath);
        }
        else if (Util.IsCurseForgeModpack(packPath))
        {
            modpack = new CurseForgeModPack(packPath);
        }
        else
        {
            throw new NotSupportedException("Unsupported modpack format");
        }

        await using (modpack as IAsyncDisposable)
        {
            // =========================
            // 3. Manifest 로드
            // Load modpack manifest
            // =========================
            Console.WriteLine("Loading modpack...");
            await modpack.LoadAsync();

            Console.WriteLine($"Provider: {modpack.Provider}");
            Console.WriteLine($"ModPack: {modpack.Name} v{modpack.Version}");
            Console.WriteLine($"Minecraft: {modpack.MinecraftVersion}");
            Console.WriteLine($"Recommended RAM: {modpack.RecommendedRam} MB");

            // =========================
            // 4. 설치 옵션 구성
            // Build install options
            // =========================
            string instancesRoot = @"C:\users\koroutine\instances";
            string instanceDir = Path.Combine(
                instancesRoot,
                $"{modpack.Name}_{modpack.Version}");

            var options = new ModPackInstallOptions
            {
                // Instance directory
                GameDirectory = instanceDir,

                // File-based progress (Forge / Minecraft install)
                FileProgress = new Progress<InstallerProgressChangedEventArgs>(p =>
                {
                    Console.WriteLine(
                        $"[File] {p.Name} {p.ProgressedTasks}/{p.TotalTasks}");
                }),

                // Byte-based download progress
                ByteProgress = new Progress<ByteProgress>(p =>
                {
                    Console.WriteLine($"[Bytes] {(p.ToRatio() * 100):F2}%");
                }),

                // Offline session (no Microsoft login)
                Session = MSession.CreateOfflineSession("player"),

                // Optional server IP
                ServerIp = "your.server",

                // Memory settings
                MaximumRamMb = modpack.RecommendedRam,
                MinimumRamMb = 1024
            };

            // =========================
            // 5. 설치 + 실행 프로세스 생성
            // Install modpack and build launch process
            // =========================
            Console.WriteLine("Installing modpack...");
            Process process = await modpack.InstallAndBuildProcessAsync(options);

            Console.WriteLine("Installation complete!");
            process.Start();
        }
    }
}


```

요구사항 / requirement
.NET 10  

CmlLib.Core (4.0.6)

CmlLib.Core.Installer.Forge(1.1.1)

CmlLib.Core.Installer.NeoForge(4.0.0)

인터넷 연결 (CurseForge 모드 다운로드용) / internet connection.

Windows 11 25H2 환경에서 테스트 완료, Linux/MacOS는 검증되지 않았습니다.

Tested on Windows 11 25H2.

