function Test-NativeMermaidResources {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$NativeAssemblyPath, [Parameter(Mandatory)][string]$CoreAssemblyPath)
    Add-Type -AssemblyName System.Reflection.Metadata
    function Read-MermaidMetadata([string]$Path, [bool]$ReadXaml) {
        $stream = [IO.File]::OpenRead($Path)
        try {
            $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe, [System.Reflection.Metadata.MetadataReaderOptions]::Default, $null)
                $types = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                foreach ($handle in $metadata.TypeDefinitions) {
                    $definition = $metadata.GetTypeDefinition($handle)
                    [void]$types.Add($metadata.GetString($definition.Namespace) + '.' + $metadata.GetString($definition.Name))
                }
                $resources = [Collections.Generic.List[string]]::new()
                if ($ReadXaml) {
                    [byte[]]$data = $pe.GetSectionData($pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent()
                    foreach ($handle in $metadata.ManifestResources) {
                        $resource = $metadata.GetManifestResource($handle)
                        if (-not $resource.Implementation.IsNil -or $metadata.GetString($resource.Name) -ne 'LensDocsStudio.Native.g.resources') { continue }
                        $offset = [int]$resource.Offset
                        if ($offset -lt 0 -or $offset -gt $data.Length - 4) { throw 'Invalid compiled native resource offset.' }
                        $length = [BitConverter]::ToInt32($data, $offset)
                        if ($length -le 0 -or $length -gt 16MB -or [long]$offset + 4 + $length -gt $data.Length) { throw 'Invalid compiled native resource length.' }
                        $resourceStream = [IO.MemoryStream]::new($data, $offset + 4, $length, $false)
                        try {
                            $reader = [Resources.ResourceReader]::new($resourceStream)
                            try {
                                $entries = $reader.GetEnumerator()
                                # Read keys only. No resource value, assembly or application code is executed.
                                while ($entries.MoveNext()) { $resources.Add([string]$entries.Key) }
                            } finally { $reader.Dispose() }
                        } finally { $resourceStream.Dispose() }
                    }
                }
                return @{ Types=$types; Resources=$resources }
            } finally { $pe.Dispose() }
        } finally { $stream.Dispose() }
    }
    $native = Read-MermaidMetadata $NativeAssemblyPath $true
    $core = Read-MermaidMetadata $CoreAssemblyPath $false
    $requiredNative = @('LensDocsStudio.Native.MermaidVisualEditorWindow', 'LensDocsStudio.Native.SequenceVisualEditorWindow', 'LensDocsStudio.Native.Rendering.NativeSequencePresentation', 'LensDocsStudio.Native.StateVisualEditorWindow', 'LensDocsStudio.Native.Rendering.NativeStatePresentation', 'LensDocsStudio.Native.Editor.MermaidDocumentEditSession', 'LensDocsStudio.Native.Editor.MermaidSourceHighlighting')
    $requiredCore = @('LensDocsStudio.Core.Mermaid.MermaidDiagramModel', 'LensDocsStudio.Core.Mermaid.MermaidSourceParser', 'LensDocsStudio.Core.Mermaid.MermaidTransformationService', 'LensDocsStudio.Core.Mermaid.MermaidVisualDraft', 'LensDocsStudio.Core.Mermaid.MermaidGraphLayout',
        'LensDocsStudio.Core.Mermaid.MermaidCapabilityAnalysis', 'LensDocsStudio.Core.Mermaid.MermaidOperationCapability', 'LensDocsStudio.Core.Mermaid.MermaidOperationKind', 'LensDocsStudio.Core.Mermaid.MermaidSemanticValidation',
        'LensDocsStudio.Core.Sequence.SequenceDiagramModel', 'LensDocsStudio.Core.Sequence.SequenceSourceParser', 'LensDocsStudio.Core.Sequence.SequenceTransformationService', 'LensDocsStudio.Core.Sequence.SequenceVisualDraft',
        'LensDocsStudio.Core.StateDiagrams.StateDiagramModel', 'LensDocsStudio.Core.StateDiagrams.StateDiagramSourceParser', 'LensDocsStudio.Core.StateDiagrams.StateTransformationService', 'LensDocsStudio.Core.StateDiagrams.StateVisualDraft', 'LensDocsStudio.Core.StateDiagrams.StateCapabilityAnalysis', 'LensDocsStudio.Core.StateDiagrams.StateSemanticValidation')
    $requiredNative += @('LensDocsStudio.Native.ErVisualEditorWindow', 'LensDocsStudio.Native.Rendering.NativeErPresentation')
    $requiredCore += @('ErDiagramModel', 'ErEntityModel', 'ErAttributeModel', 'ErRelationshipModel', 'ErDiagramSourceParser', 'ErEditOperation', 'ErEditPlan', 'ErTransformationService', 'ErVisualDraft', 'ErCapabilityAnalysis', 'ErSemanticValidation') | ForEach-Object { 'LensDocsStudio.Core.ErDiagrams.' + $_ }
    $requiredNative += @('LensDocsStudio.Native.ClassVisualEditorWindow', 'LensDocsStudio.Native.Rendering.NativeClassPresentation')
    $requiredCore += @('ClassDiagramModel', 'ClassDiagramClass', 'ClassDiagramMember', 'ClassDiagramRelationship', 'ClassDiagramNamespace', 'ClassDiagramNote', 'ClassDiagramSourceParser', 'ClassDiagramEditOperation', 'ClassEditPlan', 'ClassDiagramTransformationService', 'ClassDiagramVisualDraft', 'ClassDiagramCapabilityAnalysis', 'ClassDiagramSemanticValidation') | ForEach-Object { 'LensDocsStudio.Core.ClassDiagrams.' + $_ }
    foreach ($name in $requiredNative) { if (-not $native.Types.Contains($name)) { throw "The native Mermaid editor type is missing: $name" } }
    if (-not $native.Resources.Contains('classvisualeditorwindow.baml')) { throw 'The compiled native Class editor resource is missing.' }
    if (-not $native.Resources.Contains('ervisualeditorwindow.baml')) { throw 'The compiled native ER editor resource is missing.' }
    foreach ($name in $requiredCore) { if (-not $core.Types.Contains($name)) { throw "The Core Mermaid editor type is missing: $name" } }
    foreach ($name in @('mermaidvisualeditorwindow.baml', 'sequencevisualeditorwindow.baml', 'statevisualeditorwindow.baml', 'resources/desktopstyles.baml', 'resources/formstyles.baml', 'resources/commandicons.baml', 'resources/mermaideditorstyles.baml', 'resources/brand/application.ico')) {
        if (-not $native.Resources.Contains($name)) { throw "The compiled native Mermaid editor resource is missing: $name" }
    }
    return [ordered]@{ status='PASS'; executionPerformed=$false; nativeTypes=$requiredNative; coreTypes=$requiredCore; editorXaml='mermaidvisualeditorwindow.baml'; erEditorXaml='ervisualeditorwindow.baml'; erTypesVerified=$true; classEditorXaml='classvisualeditorwindow.baml'; classTypesVerified=$true; originalIcon='resources/brand/application.ico' }
}
