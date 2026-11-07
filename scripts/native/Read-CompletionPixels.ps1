param([string]$EvidenceDirectory = 'artifacts/preview-completion')
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 hosts the OS WinRT OCR component. Only retained label
# crops are read. OCR disagreement is review evidence, never an automatic PASS.
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapTransform, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime]
$awaitMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetGenericArguments().Count -eq 1 -and
    $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
} | Select-Object -First 1
function Wait-OcrOperation($operation, $resultType) {
    $task = $awaitMethod.MakeGenericMethod($resultType).Invoke($null, @($operation))
    if (!$task.Wait(30000)) { throw 'OS OCR exceeded the 30-second observation bound.' }
    return $task.Result
}
$root = [IO.Path]::GetFullPath($EvidenceDirectory)
$destination = Join-Path $root 'pixel-readings.json'
if (Test-Path -LiteralPath $destination) { throw 'Pixel readings must not overwrite evidence.' }
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if ($null -eq $engine) { @{verdict='INCONCLUSIVE';reason='No OS OCR language is available; manual image review required.'} | ConvertTo-Json | Set-Content $destination -Encoding UTF8; exit 0 }
$rows = [Collections.Generic.List[object]]::new()
foreach ($journal in Get-ChildItem -LiteralPath $root -Filter completion-captures.json -Recurse) {
    foreach ($capture in (Get-Content -LiteralPath $journal.FullName -Raw | ConvertFrom-Json)) {
        foreach ($region in $capture.regions) {
            $file = Join-Path $journal.DirectoryName ($capture.file.Replace('.png', '-'+$region.name+'.png'))
            $storage = Wait-OcrOperation ([Windows.Storage.StorageFile]::GetFileFromPathAsync($file)) ([Windows.Storage.StorageFile])
            $stream = Wait-OcrOperation ($storage.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
            try {
                $decoder = Wait-OcrOperation ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
                # A fixed scale makes small native labels legible to OCR. The raw
                # desktop image and byte-identical crop remain the primary evidence.
                $transform = New-Object Windows.Graphics.Imaging.BitmapTransform
                $transform.ScaledWidth = $decoder.PixelWidth * 3; $transform.ScaledHeight = $decoder.PixelHeight * 3
                $bitmap = Wait-OcrOperation ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,
                    [Windows.Graphics.Imaging.BitmapAlphaMode]::Ignore, $transform,
                    [Windows.Graphics.Imaging.ExifOrientationMode]::IgnoreExifOrientation,
                    [Windows.Graphics.Imaging.ColorManagementMode]::DoNotColorManage)) ([Windows.Graphics.Imaging.SoftwareBitmap])
                try { $result = Wait-OcrOperation ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult]) }
                finally { $bitmap.Dispose() }
                $observed = ($result.Text -replace '\s+',' ').Trim()
                $expected = ($region.expected -replace '\s+',' ').Trim()
                $rows.Add(@{file=$file.Substring($root.Length+1);action=$capture.action;frameIndex=$capture.frameIndex;region=$region.name;
                    expected=$region.expected;observed=$observed;match=($observed -ceq $expected);sha256=(Get-FileHash -LiteralPath $file).Hash;
                    verdict=$(if($observed -ceq $expected){'OCR MATCH'}else{'REQUIRES INDEPENDENT PIXEL REVIEW'})})
            } finally { $stream.Dispose() }
        }
    }
}
@{engine=$engine.RecognizerLanguage.LanguageTag;scale=3;readings=$rows;scope='OCR corroborates retained pixels; mismatches and empty results remain for manual review.'} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination -Encoding UTF8
Write-Output "Retained $($rows.Count) independent label readings."
