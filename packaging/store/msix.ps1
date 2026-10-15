# Monta el paquete MSIX de Taskbar Groups Fluent, que es lo que se sube a la Microsoft Store.
#
#   .\packaging\store\msix.ps1 -Registrar
#       compila, monta la carpeta del paquete y la registra en este equipo para probarla.
#       Hace falta el Modo de desarrollador: con el, Windows instala un paquete sin firmar.
#   .\packaging\store\msix.ps1
#       compila y monta el .msix SIN firmar, que es como lo quiere la Store: la firma la
#       pone ella al certificarlo.
#
# Todo sale a dist\msix, que no se versiona.
#
# Por que MSIX y no el instalador de siempre: un .exe en la Store hay que firmarlo con un
# certificado de pago, y un MSIX lo firma Microsoft gratis cuando pasa la revision.
#
# Dos diferencias con la build del instalador, las dos a proposito:
# - .NET va DENTRO (self-contained). El instalador descarga el runtime si falta; un paquete
#   de la Store no puede, y sin el la app no arranca.
# - Los tres programas van en la MISMA carpeta. Cada uno lleva su copia de .NET y, juntos,
#   los archivos iguales se pisan y queda una sola. Con el panel en Background\ como en el
#   instalador, el paquete llevaria .NET dos veces. Paths.BackgroundApplication sabe
#   encontrar el panel en los dos sitios.

param(
    [switch] $Registrar
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$raiz = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$salida = Join-Path $raiz "dist\msix"
$paquete = Join-Path $salida "paquete"

$identidad = Get-Content (Join-Path $PSScriptRoot "identidad.json") -Raw | ConvertFrom-Json

# El numero de MSIX lleva cuatro partes y la cuarta es de Microsoft: tiene que ir a cero o
# la Store rechaza el paquete sin decir cual de sus reglas se ha saltado.
$csproj = Get-Content (Join-Path $raiz "src\TaskbarGroups.App\TaskbarGroups.App.csproj") -Raw
if ($csproj -notmatch "<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>") { throw "No encuentro <Version> en el csproj de la app." }
$version = $Matches[1]
$versionMsix = "$version.0"

# Un paquete registrado se ejecuta DESDE su carpeta, asi que antes de rehacerla se quita el
# que hubiera; si no, sus procesos tienen los archivos abiertos y la copia falla a medias.
if ($Registrar) {
    Get-AppxPackage -Name $identidad.identityName | ForEach-Object {
        Write-Host "Quitando el paquete registrado $($_.PackageFullName)"
        Remove-AppxPackage -Package $_.PackageFullName
    }
}

if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $paquete "Assets") | Out-Null

# --- Los binarios ---------------------------------------------------------------------
foreach ($proyecto in @("TaskbarGroups.App", "TaskbarGroups.Background", "TaskbarGroups.Hover")) {
    Write-Host "Publicando $proyecto"
    # Solo los recursos en ingles y espannol, los dos idiomas de la app: WPF trae los de
    # trece idiomas mas que nadie veria nunca.
    $log = dotnet publish (Join-Path $raiz "src\$proyecto") -c Release -r win-x64 --self-contained true `
        "-p:SatelliteResourceLanguages=en%3Bes" -o $paquete -nologo -v q 2>&1
    if ($LASTEXITCODE -ne 0) { $log | Out-Host; throw "Fallo el publish de $proyecto." }
}

# Fuera los .pdb y createdump.exe, que no le sirven a nadie instalado.
Get-ChildItem $paquete -Filter *.pdb -Recurse | Remove-Item -Force
Remove-Item (Join-Path $paquete "createdump.exe") -Force -ErrorAction SilentlyContinue

# Lo que se empaqueta es el exe, asi que la version se le pregunta a el y no al csproj.
$dentro = (Get-Item (Join-Path $paquete "TaskbarGroups.App.exe")).VersionInfo.ProductVersion -replace "\+.*$", ""
if ($dentro -ne $version) { throw "El exe dice ser la $dentro y el csproj va por la $version." }

# --- Los iconos -----------------------------------------------------------------------
$logo = [System.Drawing.Image]::FromFile((Join-Path $raiz "brand\logo-1024.png"))

function Guardar-Icono([int] $lado, [string] $nombre) {
    $bmp = New-Object System.Drawing.Bitmap($lado, $lado)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = "HighQualityBicubic"
    $g.SmoothingMode = "HighQuality"
    $g.PixelOffsetMode = "HighQuality"
    $g.CompositingQuality = "HighQuality"
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($logo, 0, 0, $lado, $lado)
    $g.Dispose()
    $bmp.Save((Join-Path $paquete "Assets\$nombre"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$hechos = 0
foreach ($cuadrado in @(@("StoreLogo", 50), @("Square44x44Logo", 44), @("Square150x150Logo", 150))) {
    foreach ($escala in @(100, 125, 150, 200, 400)) {
        $lado = [int][Math]::Round($cuadrado[1] * $escala / 100)
        $nombre = if ($escala -eq 100) { "$($cuadrado[0]).png" } else { "$($cuadrado[0]).scale-$escala.png" }
        Guardar-Icono $lado $nombre
        $hechos++
    }
}

# Los que Windows pide por tamanno exacto: la barra de tareas, Inicio y el Explorador.
foreach ($px in @(16, 24, 32, 48, 256)) {
    foreach ($sufijo in @("", ".altform-unplated", ".altform-lightunplated")) {
        Guardar-Icono $px "Square44x44Logo.targetsize-$px$sufijo.png"
        $hechos++
    }
}
$logo.Dispose()
Write-Host "Iconos: $hechos"

# --- El manifiesto --------------------------------------------------------------------
$manifiesto = (Get-Content (Join-Path $PSScriptRoot "AppxManifest.plantilla.xml") -Raw -Encoding UTF8).
    Replace("{{IDENTITY_NAME}}", $identidad.identityName).
    Replace("{{PUBLISHER}}", $identidad.publisher).
    Replace("{{VERSION}}", $versionMsix).
    Replace("{{PUBLISHER_DISPLAY}}", $identidad.publisherDisplayName)

$hueco = [regex]::Match($manifiesto, "\{\{[A-Z_]+\}\}")
if ($hueco.Success) { throw "Ha quedado un hueco sin rellenar en el manifiesto: $($hueco.Value)" }
[System.IO.File]::WriteAllText((Join-Path $paquete "AppxManifest.xml"), $manifiesto, (New-Object System.Text.UTF8Encoding($false)))

# --- Registrar o empaquetar -----------------------------------------------------------
if ($Registrar) {
    Add-AppxPackage -Register (Join-Path $paquete "AppxManifest.xml")
    $instalado = Get-AppxPackage -Name $identidad.identityName
    Write-Host "Registrado: $($instalado.PackageFullName)"
    Write-Host "Se quita con: Get-AppxPackage -Name $($identidad.identityName) | Remove-AppxPackage"
}
else {
    $makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
    if (-not (Test-Path $makeappx)) { throw "No encuentro makeappx.exe: hace falta el SDK de Windows 10/11." }
    $msix = Join-Path $salida "TaskbarGroupsFluent_${versionMsix}_x64.msix"
    if (Test-Path $msix) { Remove-Item $msix -Force }

    # makeappx narra cada archivo que mete: solo se ensenna si falla, que es cuando importa.
    $log = & $makeappx pack /d $paquete /p $msix /o 2>&1
    if ($LASTEXITCODE -ne 0) { $log | Out-Host; throw "makeappx fallo (codigo $LASTEXITCODE)." }
    $mb = "{0:N2}" -f ((Get-Item $msix).Length / 1MB)
    Write-Host "Paquete: $msix ($mb MB), sin firmar: la firma la pone la Store."
}
