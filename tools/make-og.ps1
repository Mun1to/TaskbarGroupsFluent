# Genera la tarjeta que se ve al compartir el enlace (1200x630). Va en INGLES a
# proposito: el crawler no ejecuta el JS que traduce la pagina, asi que solo hay
# una version, y el enlace se pega sobre todo en sitios internacionales.
Add-Type -AssemblyName System.Drawing

$logo = "C:\proyectos\taskbar-groups-fluent\brand\logo-1024.png"
$out  = "C:\proyectos\taskbar-groups-fluent\site\assets\og.png"

$W = 1200; $H = 630
$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = "AntiAlias"
$g.InterpolationMode = "HighQualityBicubic"
$g.TextRenderingHint = "ClearTypeGridFit"

# Fondo: el mismo azul casi negro de la landing, con un halo azul arriba.
$g.Clear([System.Drawing.Color]::FromArgb(10, 14, 23))
$halo = New-Object System.Drawing.Drawing2D.GraphicsPath
$halo.AddEllipse(180, -420, 840, 840)
$brushHalo = New-Object System.Drawing.Drawing2D.PathGradientBrush($halo)
$brushHalo.CenterColor = [System.Drawing.Color]::FromArgb(70, 76, 194, 255)
$brushHalo.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 76, 194, 255))
$g.FillPath($brushHalo, $halo)

# Logo grande a la izquierda
$img = [System.Drawing.Image]::FromFile($logo)
$g.DrawImage($img, 96, 175, 280, 280)
$img.Dispose()

# Textos
$white  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(233, 237, 248))
$blue   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(76, 194, 255))
$muted  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(147, 160, 199))
$amber  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 200, 61))

$fTitle = New-Object System.Drawing.Font("Segoe UI", 54, [System.Drawing.FontStyle]::Bold)
$fBig   = New-Object System.Drawing.Font("Segoe UI", 40, [System.Drawing.FontStyle]::Bold)
$fSub   = New-Object System.Drawing.Font("Segoe UI", 21)
$fKick  = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)

$x = 430
$g.DrawString("TASKBAR GROUPS FLUENT", $fKick, $amber, $x, 172)
$g.DrawString("One icon,", $fTitle, $white, ($x - 6), 208)
$g.DrawString("all your apps.", $fTitle, $blue, ($x - 6), 285)
$g.DrawString("Group your shortcuts into a single", $fSub, $muted, $x, 385)
$g.DrawString("taskbar button. Free and open source.", $fSub, $muted, $x, 419)
# El punto medio se escribe por codigo: el script se lee como ANSI y saldria roto.
$dot = [char]0x00B7
$g.DrawString("Windows 10 / 11   $dot   MIT licence", $fKick, $muted, $x, 476)

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "escrita: $out"
