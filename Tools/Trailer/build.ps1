<#
.SYNOPSIS
  Assembles the Deckshift trailer with ffmpeg from a cut list.

.DESCRIPTION
  Reads a cut list (default Tools/Trailer/cut.json): an ordered list of segments, each one of
    { "src": "<video>", "in": s, "out": s, "speed": 1.0, "flash": 0 }   a clip of a recorded video
    { "shot": "<name>", "from": f, "to": f }                             frames from TrailerDirector
    { "title": "...", "sub": "...", "seconds": s, "size": px, "fadeIn": s, "fadeOut": s }
    { "black": true, "seconds": s }
  and drives ffmpeg to trim, card, concatenate, lay the music and the clips' own game audio
  under it, and encode a 1080p60 H.264 mp4.

  Music is a list of splices played back to back with a short crossfade, so the track can be
  shortened at bar boundaries ("play 0-74.7, then jump to the outro at 105.8"). It is trimmed to
  the picture and faded out at the end. The clips' own audio (game SFX) rides underneath at
  `sfxGain`; titles and frame shots are silent.

  Re-cutting is editing the json and running this again: a full rebuild is well under a minute.

  Run from the project root:   powershell -File Tools/Trailer/build.ps1
  Fast review encode:          powershell -File Tools/Trailer/build.ps1 -Draft
  One segment, silent:         powershell -File Tools/Trailer/build.ps1 -Only 6

.NOTES
  ffmpeg is found on PATH or in the WinGet install (Gyan.FFmpeg). Frame numbers are 1-based and
  `to` is inclusive; clip times are seconds and `out` is exclusive. `flash` is a white-in from a
  hit, in seconds (0.1 is plenty). Keep this file ASCII: Windows PowerShell 5.1 reads it as ANSI.
#>
param(
    [string]$Cut = "Tools/Trailer/cut.json",
    [int]$Only = -1,
    [switch]$Draft
)

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Find-Ffmpeg {
    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $links = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\ffmpeg.exe"
    if (Test-Path $links) { return $links }
    $pkg = Get-ChildItem (Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages") -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pkg) { return $pkg.FullName }
    throw "ffmpeg not found. Install with: winget install Gyan.FFmpeg"
}

# drawtext has its own escaping rules on top of the filtergraph's. This is the one place text is
# prepared, so titles can hold apostrophes and colons without anyone remembering the rules.
function Escape-DrawText([string]$s) {
    $s = $s.Replace('\', '\\\\')
    foreach ($c in @("'", ':', ',', ';', '[', ']', '%')) { $s = $s.Replace($c, '\' + $c) }
    return $s
}

function F([double]$x) { return $x.ToString("0.###", [Globalization.CultureInfo]::InvariantCulture) }

$ffmpeg = Find-Ffmpeg
$cfg = Get-Content $Cut -Raw | ConvertFrom-Json
$fps = if ($cfg.fps) { [int]$cfg.fps } else { 60 }
$font = $cfg.font
$W = 1920; $H = 1080
$AR = 48000
$Bg = "0x0D0B09"        # the game's near-black, warm not blue
$Ink = "0xEAD9B8"       # parchment, the display face's natural colour

$segments = @($cfg.segments)
if ($Only -ge 0) { $segments = @($segments[$Only]) }

$inputs = @()
$vchains = @()
$achains = @()
$total = 0.0
$inIdx = 0
$timeline = @()

for ($i = 0; $i -lt $segments.Count; $i++) {
    $s = $segments[$i]
    $start = $total

    if ($s.src) {
        if (-not (Test-Path $s.src)) { throw "segment ${i}: source not found: $($s.src)" }
        $tin = [double]$s.in; $tout = [double]$s.out
        $speed = if ($s.speed) { [double]$s.speed } else { 1.0 }
        $len = ($tout - $tin) / $speed
        if ($len -le 0) { throw "segment ${i}: out must be > in" }

        # Input-side seek: decodes only this clip rather than the whole file for every segment.
        $inputs += @("-ss", (F $tin), "-t", (F ($tout - $tin)), "-i", $s.src)
        $v = "[$($inIdx):v]trim=duration=$(F ($tout - $tin)),setpts=(PTS-STARTPTS)/$(F $speed),scale=${W}:${H}:flags=lanczos,fps=${fps},format=yuv420p"
        if ($s.flash -and [double]$s.flash -gt 0) { $v += ",fade=t=in:st=0:d=$(F ([double]$s.flash)):color=white" }
        $vchains += "$v[v$i]"

        $a = "[$($inIdx):a]asetpts=PTS-STARTPTS,aresample=${AR},aformat=channel_layouts=stereo"
        if ($speed -ne 1.0) { $a += ",atempo=$(F $speed)" }
        $achains += "$a,atrim=duration=$(F $len)[a$i]"
        $inIdx++
        $total += $len
        $timeline += ("{0,6:0.00}  {1,-8} {2}  [{3}-{4}]" -f $start, "clip", $s.src.Split('\/')[-1], $tin, $tout)
    }
    elseif ($s.shot) {
        $dir = "TrailerCapture/$($s.shot)"
        if (-not (Test-Path $dir)) { throw "segment ${i}: no frames for shot '$($s.shot)' - record it first (Deckshift > Trailer)" }
        $from = [int]$s.from; $to = [int]$s.to
        $count = $to - $from + 1
        if ($count -le 0) { throw "segment ${i}: 'to' must be >= 'from'" }
        $len = $count / $fps
        $inputs += @("-framerate", $fps, "-start_number", $from, "-i", "$dir/f%05d.png")
        $vchains += "[$($inIdx):v]trim=end_frame=$count,setpts=PTS-STARTPTS,scale=${W}:${H}:out_color_matrix=bt709:out_range=tv,format=yuv420p[v$i]"
        $achains += "anullsrc=r=${AR}:cl=stereo,atrim=duration=$(F $len)[a$i]"
        $inIdx++
        $total += $len
        $timeline += ("{0,6:0.00}  {1,-8} {2}  [{3}-{4}]" -f $start, "shot", $s.shot, $from, $to)
    }
    elseif ($s.title -or $s.black) {
        $secs = [double]$s.seconds
        $chain = "color=c=${Bg}:s=${W}x${H}:r=${fps}:d=$(F $secs)"
        if ($s.title) {
            $fi = if ($s.fadeIn) { [double]$s.fadeIn } else { 0.4 }
            $fo = if ($s.fadeOut) { [double]$s.fadeOut } else { 0.4 }
            $size = if ($s.size) { [int]$s.size } else { 96 }
            $text = Escape-DrawText $s.title
            if ($s.sub) {
                $sub = Escape-DrawText $s.sub
                $subSize = [int]($size * 0.34)
                $chain += ",drawtext=fontfile=${font}:text='${text}':fontsize=${size}:fontcolor=${Ink}:x=(w-text_w)/2:y=(h-text_h)/2-${subSize}"
                $chain += ",drawtext=fontfile=${font}:text='${sub}':fontsize=${subSize}:fontcolor=${Ink}@0.75:x=(w-text_w)/2:y=(h-text_h)/2+${size}*0.55"
            }
            else {
                $chain += ",drawtext=fontfile=${font}:text='${text}':fontsize=${size}:fontcolor=${Ink}:x=(w-text_w)/2:y=(h-text_h)/2"
            }
            $chain += ",fade=t=in:st=0:d=$(F $fi),fade=t=out:st=$(F ($secs - $fo)):d=$(F $fo)"
        }
        $vchains += "$chain,format=yuv420p[v$i]"
        $achains += "anullsrc=r=${AR}:cl=stereo,atrim=duration=$(F $secs)[a$i]"
        $total += $secs
        $label = if ($s.title) { $s.title } else { "(black)" }
        $timeline += ("{0,6:0.00}  {1,-8} {2}" -f $start, "card", $label)
    }
    else { throw "segment $i is not a clip, shot, title or black" }
}

$n = $segments.Count
$vpairs = ""; $apairs = ""
for ($k = 0; $k -lt $n; $k++) { $vpairs += "[v$k]"; $apairs += "[a$k]" }

# Audio graph, shared by the loudness measurement pass and the render. Ends in [mixraw].
$audioGraph = ($achains -join ";") + ";${apairs}concat=n=${n}:v=0:a=1[sfx]"
$hasMusic = $cfg.music -and $cfg.music.file -and $Only -lt 0
$mi = $inIdx
if ($hasMusic) {
    $m = $cfg.music
    if (-not (Test-Path $m.file)) { throw "music not found: $($m.file)" }
    $fade = if ($m.fadeOut) { [double]$m.fadeOut } else { 1.5 }
    $sfxGain = if ($null -ne $cfg.sfxGain) { [double]$cfg.sfxGain } else { 0.5 }
    $musicGain = if ($null -ne $m.gain) { [double]$m.gain } else { 1.0 }
    $xf = if ($m.spliceCrossfade) { [double]$m.spliceCrossfade } else { 0.08 }
    $splices = if ($m.splices) { @($m.splices) } else { @(@{ from = (if ($m.offset) { $m.offset } else { 0 }); to = 100000 }) }
    $inputs += @("-i", $m.file)

    # Each splice is its own trimmed stream off one split of the track; consecutive splices are
    # joined with a short crossfade so a jump between bars does not click.
    $splitOut = ""
    for ($j = 0; $j -lt $splices.Count; $j++) { $splitOut += "[mi$j]" }
    $audioGraph += ";[$($mi):a]asplit=$($splices.Count)$splitOut"
    $cur = ""
    for ($j = 0; $j -lt $splices.Count; $j++) {
        $sp = $splices[$j]
        $audioGraph += ";[mi$j]atrim=start=$(F ([double]$sp.from)):end=$(F ([double]$sp.to)),asetpts=PTS-STARTPTS,aresample=${AR},aformat=channel_layouts=stereo[m$j]"
        if ($j -eq 0) { $cur = "[m0]" }
        else {
            $audioGraph += ";${cur}[m$j]acrossfade=d=$(F $xf):c1=tri:c2=tri[mx$j]"
            $cur = "[mx$j]"
        }
    }
    $audioGraph += ";${cur}atrim=duration=$(F $total),afade=t=out:st=$(F ($total - $fade)):d=$(F $fade),volume=$(F $musicGain)[music]"
    $audioGraph += ";[sfx]volume=$(F $sfxGain)[sfxg]"
    $audioGraph += ";[music][sfxg]amix=inputs=2:duration=first:normalize=0[mixraw]"
}
else {
    $audioGraph += ";[sfx]anull[mixraw]"
}

$out = if ($Only -ge 0) { "TrailerCapture/_segment$Only.mp4" } else { $cfg.output }
$preset = if ($Draft) { "veryfast" } else { "slow" }
$crf = if ($Draft) { 23 } else { 18 }

Write-Host "Timeline:"
$timeline | ForEach-Object { Write-Host "  $_" }

# ---- pass 1: measure the mix's integrated loudness ------------------------------------------
# A LINEAR gain to the target, not loudnorm's one-pass mode: that one is a dynamic normaliser
# and it flattened the track (the quiet intro came out 7 dB under the drop instead of 13).
# Measured once, applied as a plain volume change with a true-peak limiter behind it.
$targetLufs = if ($cfg.music -and $cfg.music.targetLufs) { [double]$cfg.music.targetLufs } else { -14.0 }
$gainDb = 0.0
if ($Only -lt 0) {
    $measureArgs = @("-v", "info") + $inputs + @("-filter_complex", "$audioGraph;[mixraw]ebur128=peak=true[o]", "-map", "[o]", "-f", "null", "-")
    # ffmpeg reports on stderr; under Stop that would be treated as a failure, so relax it here.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $log = (& $ffmpeg @measureArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
    $ErrorActionPreference = $prevEap
    Set-Content -Path "TrailerCapture/_measure.log" -Value $log -Encoding utf8
    # The Summary block at the end, not the running "I:" printed every 100 ms (the first of
    # those reads -70, the silence floor).
    $summaryAt = $log.LastIndexOf("Summary:")
    $tail = if ($summaryAt -ge 0) { $log.Substring($summaryAt) } else { "" }
    if ($tail -match "I:\s+(-?[\d.]+) LUFS") {
        $measured = [double]$Matches[1]
        $gainDb = $targetLufs - $measured
        Write-Host ("Loudness: measured {0:0.0} LUFS, target {1:0.0}, gain {2:+0.0} dB" -f $measured, $targetLufs, $gainDb)
    }
    else { Write-Warning "loudness measurement failed; rendering at unity gain" }
}

# ---- pass 2: render --------------------------------------------------------------------------
$graph = ($vchains -join ";") + ";${vpairs}concat=n=${n}:v=1:a=0[v];" + $audioGraph
$graph += ";[mixraw]volume=$(F $gainDb)dB,alimiter=limit=0.85:attack=5:release=50:level=false,aresample=${AR}[a]"

$args = @("-y", "-v", "error", "-stats") + $inputs + @(
    "-filter_complex", $graph, "-map", "[v]", "-map", "[a]",
    "-c:v", "libx264", "-preset", $preset, "-crf", $crf, "-pix_fmt", "yuv420p", "-r", $fps,
    "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
    "-movflags", "+faststart", "-shortest", "-c:a", "aac", "-b:a", "192k", $out)

Write-Host ("Building {0}  ({1:0.00}s, {2} segments)" -f $out, $total, $n)
& $ffmpeg @args
if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed ($LASTEXITCODE)" }
Write-Host ("Done: {0}  {1:0.0} MB" -f $out, ((Get-Item $out).Length / 1MB))
