# Guardian of the Globe - offline build-tools pack. Used by the app; the player never opens this file.
#   -Extract <pack> [-ForApp]   unpack a pack into this folder (the app does this when a pack is next to it)
#   -Make [-Source <folder>] [-Out <file>]   build the pack from an installed tools\portable (developer only)
param([switch]$Make, [string]$Extract = '', [switch]$ForApp, [string]$Source = '', [string]$Out = '')
$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot

function Remove-Safe([string]$p) { if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue } }
function Get-Mb([string]$p) { if (-not (Test-Path -LiteralPath $p)) { return 0 }; $s = (Get-ChildItem -LiteralPath $p -Recurse -Force -File | Measure-Object Length -Sum).Sum; [int]($s / 1MB) }

if ($Extract) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Write-Host ('Unpacking ' + (Split-Path -Leaf $Extract) + ' ...')
    $hasInner = $false
    $zf = [IO.Compression.ZipFile]::OpenRead($Extract)
    try { foreach ($en in $zf.Entries) { if ($en.FullName -eq 'inner.tar.xz') { $hasInner = $true } } } finally { $zf.Dispose() }
    if (-not $hasInner) {
        $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
        & $tar -xf $Extract -C $Root
        if ($LASTEXITCODE -ne 0) { Write-Host 'Unpacking failed.'; exit 3 }
    } else {
        $tmp = Join-Path $env:TEMP 'gotg_unpack'
        Remove-Safe $tmp
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($Extract, $tmp)
        [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $tmp 'py.zip'), (Join-Path $tmp 'py'))
        Write-Host 'Unpacking the tools (about a minute) ...'
        & (Join-Path $tmp 'py\python.exe') -I -c "import sys, tarfile; t = tarfile.open(sys.argv[1], 'r:xz'); t.extractall(sys.argv[2], filter='fully_trusted'); t.close()" (Join-Path $tmp 'inner.tar.xz') $Root
        $code = $LASTEXITCODE
        Remove-Safe $tmp
        if ($code -ne 0) { Write-Host 'Unpacking failed.'; exit 3 }
    }
    Write-Host 'Unpacked.'
    if ($ForApp) {
        & (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'portable.ps1') -ForApp
        exit $LASTEXITCODE
    }
    exit 0
}

if (-not $Make) { Write-Host 'Nothing to do. Use -Make or -Extract.'; exit 0 }

# ---------------------------------------------------------------- make
if (-not $Source) { $Source = Split-Path -Parent $Root; if (-not (Test-Path -LiteralPath (Join-Path $Source 'tools\portable'))) { $Source = $Root } }
$tp = Join-Path $Source 'tools\portable'
$names = @('llvm-mingw', 'cmake', 'ninja', 'python', 'mingit', 'busybox')
foreach ($n in $names) { if (-not (Test-Path -LiteralPath (Join-Path $tp "$n\.installed"))) { Write-Host "STOP: $n is missing or incomplete in $tp"; exit 1 } }
Write-Host "Source: $Source"
$stage = Join-Path $env:TEMP 'gotg_toolpack'
Remove-Safe $stage
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tools\portable'), (Join-Path $stage 'third_party') | Out-Null
foreach ($n in $names) {
    & robocopy (Join-Path $tp $n) (Join-Path $stage "tools\portable\$n") /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { Write-Host "STOP: copying $n failed"; exit 1 }
}
foreach ($n in @('wgpu-native', 'sdl2')) {
    $s = Join-Path $Source "third_party\$n"
    if (Test-Path -LiteralPath $s) {
        & robocopy $s (Join-Path $stage "third_party\$n") /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { Write-Host "STOP: copying $n failed"; exit 1 }
    } else { Write-Host "Note: third_party\$n not found, it will not be in the pack (the build downloads it)." }
}
Write-Host ('Copied: {0} MB' -f (Get-Mb $stage))

# ---- trimming: only what a Windows x86_64 build never touches
$l = Join-Path $stage 'tools\portable\llvm-mingw'
foreach ($d in 'aarch64-w64-mingw32', 'armv7-w64-mingw32', 'i686-w64-mingw32', 'python') { Remove-Safe (Join-Path $l $d) }
if (Test-Path -LiteralPath (Join-Path $l 'bin')) {
    Get-ChildItem -LiteralPath (Join-Path $l 'bin') -File | Where-Object { $_.Name -match '^(lldb|liblldb|clang-tidy|clang-format|clang-repl|clangd|lli|llvm-mca|llvm-exegesis|llvm-xray|llvm-jitlink)' -or $_.Name -match '^(aarch64|armv7|i686)-w64-mingw32-' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}
Get-ChildItem -LiteralPath (Join-Path $l 'lib\clang') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $w = Join-Path $_.FullName 'lib\windows'
    if (Test-Path -LiteralPath $w) { Get-ChildItem -LiteralPath $w -File | Where-Object { $_.Name -match 'aarch64|arm64ec|armv7|i386' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force } }
}
$c = Join-Path $stage 'tools\portable\cmake'
foreach ($d in 'doc', 'man') { Remove-Safe (Join-Path $c $d) }
foreach ($f in 'cmake-gui.exe', 'cpack.exe', 'ctest.exe') { Remove-Safe (Join-Path $c "bin\$f") }
Get-ChildItem -LiteralPath (Join-Path $c 'share') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Name -match '^(vim|emacs|bash-completion|aclocal)$') { Remove-Safe $_.FullName } else { Remove-Safe (Join-Path $_.FullName 'Help') }
}
Write-Host ('After trimming: {0} MB (llvm-mingw {1}, cmake {2}, mingit {3})' -f (Get-Mb $stage), (Get-Mb $l), (Get-Mb $c), (Get-Mb (Join-Path $stage 'tools\portable\mingit')))

# ---- self-test of the trimmed tools
$ok = $true
function Test-Step([string]$label, [bool]$pass) { if ($pass) { Write-Host "  PASS  $label" } else { Write-Host "  FAIL  $label"; $script:ok = $false } }
$oldPath = $env:PATH
$env:PATH = (Join-Path $l 'bin') + ';' + $oldPath
$tmp = Join-Path $env:TEMP 'gotg_tp_test'; Remove-Safe $tmp; New-Item -ItemType Directory -Force -Path $tmp | Out-Null
Set-Content -LiteralPath (Join-Path $tmp 'h.cpp') -Value '#include <iostream>' , 'int main() { std::cout << "toolpack-ok"; return 0; }' -Encoding ASCII
Push-Location $tmp
& (Join-Path $l 'bin\clang++.exe') -target x86_64-w64-mingw32 h.cpp -o h.exe 2>&1 | Out-Null
Test-Step 'clang++ builds a program' ((Test-Path -LiteralPath (Join-Path $tmp 'h.exe')))
if (Test-Path -LiteralPath (Join-Path $tmp 'h.exe')) { $o = & (Join-Path $tmp 'h.exe'); Test-Step 'that program runs' ($o -eq 'toolpack-ok') }
& (Join-Path $l 'bin\clang.exe') --version 2>&1 | Out-Null; Test-Step 'clang runs' ($LASTEXITCODE -eq 0)
foreach ($t in @(@('lld-link / ld.lld', (Join-Path $l 'bin\ld.lld.exe')), @('llvm-ar', (Join-Path $l 'bin\llvm-ar.exe')), @('llvm-ranlib', (Join-Path $l 'bin\llvm-ranlib.exe')))) { Test-Step $t[0] (Test-Path -LiteralPath $t[1]) }
& (Join-Path $c 'bin\cmake.exe') --version 2>&1 | Out-Null; Test-Step 'cmake runs' ($LASTEXITCODE -eq 0)
& (Join-Path $stage 'tools\portable\ninja\ninja.exe') --version 2>&1 | Out-Null; Test-Step 'ninja runs' ($LASTEXITCODE -eq 0)
& (Join-Path $stage 'tools\portable\python\python.exe') -c 'import sys' 2>&1 | Out-Null; Test-Step 'python runs' ($LASTEXITCODE -eq 0)
& (Join-Path $stage 'tools\portable\mingit\usr\bin\bash.exe') -c 'echo ok' 2>&1 | Out-Null; Test-Step 'bash runs' ($LASTEXITCODE -eq 0)
Pop-Location
$env:PATH = $oldPath
Remove-Safe $tmp
if (-not $ok) { Write-Host 'STOP: a trimmed tool failed. Nothing was packed.'; exit 1 }

# ---- pack (one zip holding a small python and the xz-compressed tools: Windows' own tar cannot read xz)
if (-not $Out) { $Out = Join-Path $Source 'toolpack.zip' }
$py = Join-Path $stage 'tools\portable\python\python.exe'
& $py -c 'import lzma, tarfile' 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'STOP: the bundled python has no lzma module.'; exit 1 }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$inner = Join-Path $env:TEMP 'gotg_inner.tar.xz'
$pyzip = Join-Path $env:TEMP 'gotg_py.zip'
$script = Join-Path $env:TEMP 'gotg_mktar.py'
Remove-Safe $inner; Remove-Safe $pyzip; Remove-Safe $Out
Set-Content -LiteralPath $script -Encoding ASCII -Value 'import os, sys, tarfile', 'stage, out = sys.argv[1], sys.argv[2]', 'with tarfile.open(out, "w:xz", preset=6) as t:', '    for name in ("tools", "third_party"):', '        p = os.path.join(stage, name)', '        if os.path.isdir(p): t.add(p, arcname=name)'
Write-Host 'Packing (xz, a few minutes) ...'
& $py -I $script $stage $inner
Remove-Safe $script
if (-not (Test-Path -LiteralPath $inner)) { Write-Host 'STOP: the tool archive was not written.'; exit 1 }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $stage 'tools\portable\python'), $pyzip, [IO.Compression.CompressionLevel]::Optimal, $false)
$za = [IO.Compression.ZipFile]::Open($Out, [IO.Compression.ZipArchiveMode]::Create)
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $pyzip, 'py.zip', [IO.Compression.CompressionLevel]::Optimal)
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $inner, 'inner.tar.xz', [IO.Compression.CompressionLevel]::NoCompression)
} finally { $za.Dispose() }
Remove-Safe $inner; Remove-Safe $pyzip
Remove-Safe (Join-Path $Source 'toolpack.tar.xz')
Remove-Safe $stage
if (-not (Test-Path -LiteralPath $Out)) { Write-Host 'STOP: the pack was not written.'; exit 1 }
Write-Host ('DONE: {0}  ({1} MB)' -f $Out, [int]((Get-Item -LiteralPath $Out).Length / 1MB))
Write-Host ('SHA256: ' + (Get-FileHash -LiteralPath $Out -Algorithm SHA256).Hash)
exit 0
