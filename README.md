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

##추후 개선사항

- Modrinth 형식의 모드팩도 가능하게
- Fabric/Forge 자동 전환
- Neoforge 지원
- FTB 형식 지원

## 설치

NuGet 또는 직접 프로젝트 참조 방식 사용 가능:

```bash
# 프로젝트에 직접 추가
git clone https://github.com/your-repo/CurseForgeModPackParser.git
```
⚠️ 반드시 CmlLib.Core (v3 이상 권장)이 필요합니다.

사용 예시
```csharp
using CmlLib.Core.Installer.Modpack;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var modPack = new CurseForgeModPack("test-pack.zip");

        await modPack.LoadAsync();

        // ModPackInstallOptions는 MLaunchOption을 extend 합니다. 마인크래프트 메모리/경로 등등을 바꾸길 원하신다면 여기서 지정해주세요.
        var options = new ModPackInstallOptions 
        {
            GameDirectory = @"C:\Minecraft\Instances\TestPack",
            FileProgress = new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(
                progress => Console.WriteLine("File: " + progress.ToString())),
            ByteProgress = new Progress<CmlLib.Core.Installers.ByteProgress>(
                progress => Console.WriteLine("Bytes: " + progress.ToString()))
        };

        // 설치 및 프로세스 빌드
        var process = await modPack.InstallAndBuildProcessAsync(options);
        Console.WriteLine("모드팩 설치 완료. Minecraft 프로세스 생성됨.");
        process.Start();
    }
}
```

요구사항
.NET 10 이상

CmlLib.Core (Minecraft 런처 라이브러리)
CmlLib.Core.Installer.Forge
CmlLib.Core.Installer.NeoForge

인터넷 연결 (CurseForge 모드 다운로드용)

Windows 환경에서 테스트 완료, Linux/OSX 미검증

라이선스
MIT License © 2026 Junwon Yoon

참고
이 프로젝트는 **CmlLib용 에드온(Add-on)**으로 설계되었습니다.
독립적인 런처를 만들기보다는 CmlLib 프로젝트 내에서 모드팩 설치/관리를 확장할 때 사용하세요.
