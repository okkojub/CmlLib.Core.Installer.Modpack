namespace CmlLib.Core.Installer.Modpack;

using System.Collections.Generic;

public class CurseForgeManifest
{
    public string name { get; set; }
    public string version { get; set; }
    public Minecraft minecraft { get; set; }
    public string overrides { get; set; }

    // ← 여기에 모드 목록 추가
    public List<CurseForgeFile> files { get; set; }
}

public class Minecraft
{
    public string version { get; set; }
    public List<ModLoader> modLoaders { get; set; }
    public int recommendedRam { get; set; } = 512;
}

public class ModLoader
{
    public string id { get; set; }
    public bool primary { get; set; }
}

// manifest.json 파일 내 mod 파일 정보 구조
public class CurseForgeFile
{
    public int projectID { get; set; }
    public int fileID { get; set; }
    public bool required { get; set; }
    public bool isLocked { get; set; }
}