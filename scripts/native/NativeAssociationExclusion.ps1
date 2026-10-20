function Assert-NativeAssociationAbsence {
    if (Test-Path 'HKCU:\Software\Classes\LensDocsStudio.Native.Document') { throw 'The excluded native association registration exists.' }
    foreach ($extension in '.md', '.markdown', '.mmd', '.mermaid') {
        $key = Get-Item -LiteralPath ("HKCU:\Software\Classes\$extension\OpenWithProgids") -ErrorAction SilentlyContinue
        if ($key -and $key.GetValueNames() -contains 'LensDocsStudio.Native.Document') { throw "An excluded Open with registration exists for $extension." }
    }
}
