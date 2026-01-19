using System;
using System.IO;

namespace CmlLib.Core.Installer.Modpack;

public class Util
{
    /// <summary>
    /// CurseForge ModPack 여부
    /// - .zip 확장자
    /// - .mrpack 은 제외
    /// </summary>
    public static bool IsCurseForgeModpack(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var ext = Path.GetExtension(path);
        return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Modrinth ModPack 여부
    /// - .mrpack 확장자
    /// </summary>
    public static bool IsModrinthModpack(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var ext = Path.GetExtension(path);
        return ext.Equals(".mrpack", StringComparison.OrdinalIgnoreCase);
    }
}