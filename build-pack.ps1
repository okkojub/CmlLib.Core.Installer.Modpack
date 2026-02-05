# build-pack.ps1
Write-Host "=== Construyendo paquete NuGet con soporte NeoForge ===" -ForegroundColor Cyan

# Limpiar builds anteriores
dotnet clean

# Restaurar paquetes
dotnet restore

# Construir proyecto
dotnet build --configuration Release

# Crear paquete NuGet
dotnet pack --configuration Release --output ./nupkgs --no-build

# Mostrar archivos generados
Write-Host "`n=== Paquete(s) generado(s) ===" -ForegroundColor Green
Get-ChildItem ./nupkgs/*.nupkg | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor Yellow
    Write-Host "    Ruta: $($_.FullName)" -ForegroundColor Gray
}

Write-Host "`n=== Para usar en tu proyecto del launcher ===" -ForegroundColor Cyan
Write-Host "Ejecuta en tu proyecto del launcher:" -ForegroundColor White
Write-Host "dotnet add package CmlLib.Core.Installer.Modpack.NeoForge --version 1.0.1-neoforge --source $(Resolve-Path ./nupkgs)" -ForegroundColor Yellow