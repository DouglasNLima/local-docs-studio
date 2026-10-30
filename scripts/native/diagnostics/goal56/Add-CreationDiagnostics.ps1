param([Parameter(Mandatory)][string]$Source)
$ErrorActionPreference = 'Stop'
$project = Join-Path $Source 'src/native/LensDocsStudio.Native.Validation'
$path = Join-Path $project 'MermaidCreationUiChecks.cs'
$text = [IO.File]::ReadAllText($path)
$set = 'owner.Patterns.Value.Pattern.SetValue('
$count = ([regex]::Matches($text, [regex]::Escape($set))).Count
if ($count -ne 7) { throw "Unexpected original SetValue count: $count" }
$text = [regex]::Replace($text, 'owner\.Patterns\.Value\.Pattern\.SetValue\(([^;]+)\);', 'CreationSetValueDiagnostics.SetValue(owner, $1, main!, automation, output);')
$save = 'Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);'
$text = $text.Replace($save, 'CreationSetValueDiagnostics.BeforeSave(owner, main!, automation, output); ' + $save)
[IO.File]::WriteAllText($path, $text)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CreationSetValueDiagnostics.cs') -Destination $project
