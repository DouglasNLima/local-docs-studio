"""Reject any local viewport assertion difference beyond the declared DPI setup."""
from pathlib import Path
import hashlib

root = Path(__file__).resolve().parents[3]
original = root / 'src/native/LensDocsStudio.Native.Validation/NativeWorkspaceViewportChecks.cs'
adapter = Path(__file__).resolve().parent / 'LocalOriginalControls/LocalViewportChecks.cs'
source = original.read_text(encoding='utf-8')
expected = source[source.index('    private static void CheckWorkspaceViewportOwnership'):]
expected = expected.replace('private static void CheckWorkspaceViewportOwnership', 'public static void CheckWorkspaceViewportOwnership', 1)
expected = expected.replace('window.Patterns.Transform.Pattern.Resize(1280, 800);',
    '// Preserve 1280 × 800 logical pixels on the local scaled desktop. Assertions below are unchanged.\n'
    '                var scale = GetDpiForWindow(new IntPtr(window.Properties.NativeWindowHandle.Value)) / 96d;\n'
    '                window.Patterns.Transform.Pattern.Resize(1280 * scale, 800 * scale);')
actual = adapter.read_text(encoding='utf-8')
actual = actual[actual.index('    public static void CheckWorkspaceViewportOwnership'):]
assert actual == expected, 'Original viewport assertions or execution order changed'
print('PASS: every original viewport assertion retained; only local DPI setup differs')
