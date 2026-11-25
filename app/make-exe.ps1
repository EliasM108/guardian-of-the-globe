$app = $PSScriptRoot
$root = Split-Path -Parent $app
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) { 'csc.exe not found (.NET Framework 4 is missing).'; exit 1 }
$ico = Join-Path $app 'guardian.ico'
$own = Join-Path $app 'guardian-of-the-globe.ico'
if (-not (Test-Path -LiteralPath $own)) { try {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object Drawing.Bitmap 256, 256
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 4, 16, 9))), 8, 8, 240, 240)
    $ring = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 120, 235, 50)), 12
    $thin = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 120, 235, 50)), 5
    $g.DrawEllipse($ring, 14, 14, 228, 228)
    $g.DrawEllipse($thin, 48, 48, 160, 160)
    $g.DrawEllipse($thin, 88, 48, 80, 160)
    $g.DrawEllipse($thin, 118, 48, 20, 160)
    $g.DrawLine($thin, 48, 128, 208, 128)
    $g.DrawLine($thin, 59, 88, 197, 88)
    $g.DrawLine($thin, 59, 168, 197, 168)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $png = $ms.ToArray()
    $fs = [IO.File]::Create($ico)
    $bw = New-Object IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$png.Length); $bw.Write([uint32]22)
    $bw.Write($png)
    $bw.Close()
} catch { $ico = $null } }
if (Test-Path -LiteralPath $own) { $ico = $own }
$out = Join-Path $root 'Guardian of the Globe.exe'
$src = Join-Path $app 'Launcher.cs'
$a = @('/nologo', '/target:winexe', '/optimize+', "/out:$out", '/reference:System.Windows.Forms.dll')
if ($ico -and (Test-Path -LiteralPath $ico)) { $a += "/win32icon:$ico" }
$a += $src
$res = & $csc $a 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -and $ico) {
    $a2 = @('/nologo', '/target:winexe', '/optimize+', "/out:$out", '/reference:System.Windows.Forms.dll', $src)
    $res = & $csc $a2 2>&1 | Out-String
}
if (Test-Path -LiteralPath $out) { "EXE OK: $out" } else { $res; 'EXE FAILED' }