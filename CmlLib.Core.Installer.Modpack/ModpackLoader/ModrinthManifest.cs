namespace CmlLib.Core.Installer.Modpack.ModpackLoader;

using System.Collections.Generic;

public class ModrinthManifest
{
    public string game { get; set; }
    public int formatVersion { get; set; }

    // CurseForge의 version 대응
    public string versionId { get; set; }

    public string name { get; set; }
    public string summary { get; set; }

    // 모드 파일 목록
    public List<File> files { get; set; }

    // Minecraft / Loader 정보
    public Dependencies dependencies { get; set; }

    // =========================
    // Nested types
    // =========================

    public class File
    {
        // mods/xxx.jar
        public string path { get; set; }

        public Hashes hashes { get; set; }

        public Env env { get; set; }

        // 다운로드 URL (보통 1개)
        public List<string> downloads { get; set; }

        public long fileSize { get; set; }
    }

    public class Hashes
    {
        public string sha1 { get; set; }
        public string sha512 { get; set; }
    }

    public class Env
    {
        // "required" / "optional" / "unsupported"
        public string client { get; set; }
        public string server { get; set; }
    }

    public class Dependencies
    {
        public string minecraft { get; set; }

        // Forge 모드팩이면 사용
        public string forge { get; set; }

        // Fabric 모드팩이면 사용
        // (JSON: "fabric-loader")
        public string fabric_loader { get; set; }
    }
}