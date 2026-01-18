using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;

namespace CmlLib.Core.Installer.Modpack;

using CmlLib.Core.Installer;
using System;

public sealed class ModPackInstallOptions: MLaunchOption
{
    public string InstanceRoot { get; init; } = null!;
    public string InstanceName { get; init; } = null!;

    public string GameDirectory { get; set; } = null!;
    
    public IProgress<InstallerProgressChangedEventArgs>? FileProgress { get; init; }
    public IProgress<ByteProgress>? ByteProgress { get; init; }
}

