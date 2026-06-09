using System.Text;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LensDocsStudio.Windows.Services;

public sealed class NativeFileService
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".mmd",
        ".mermaid",
        ".txt",
    };

    private readonly nint ownerWindowHandle;
    private readonly Dictionary<string, string> nativeHandles = new(StringComparer.Ordinal);
    private readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public NativeFileService(Window ownerWindow)
    {
        ownerWindowHandle = WindowNative.GetWindowHandle(ownerWindow);
    }

    public async Task<object> OpenFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".mmd");
        picker.FileTypeFilter.Add(".mermaid");
        picker.FileTypeFilter.Add(".txt");
        InitializeWithWindow.Initialize(picker, ownerWindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return new { cancelled = true };
        }

        ValidatePath(file.Path);
        var info = new FileInfo(file.Path);
        if (info.Length > MaxFileBytes)
        {
            throw new NativeFileException("Choose a UTF-8 text file up to 5 MB.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(file.Path, strictUtf8);
        }
        catch (DecoderFallbackException)
        {
            throw new NativeFileException("Choose a UTF-8 encoded text file.");
        }

        var handleId = CreateHandle(file.Path);
        return new
        {
            cancelled = false,
            name = file.Name,
            displayName = file.Name,
            extension = Path.GetExtension(file.Name).ToLowerInvariant(),
            encoding = "utf-8",
            content,
            nativeHandleId = handleId,
        };
    }

    public async Task<object> SaveFileAsync(string? nativeHandleId, string? content)
    {
        if (string.IsNullOrWhiteSpace(nativeHandleId) || !nativeHandles.TryGetValue(nativeHandleId, out var path))
        {
            throw new NativeFileException("The Windows file handle is no longer available. Use Save as.");
        }

        ValidatePath(path);
        ValidateContent(content);
        await File.WriteAllTextAsync(path, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var name = Path.GetFileName(path);
        return new
        {
            saved = true,
            name,
            displayName = name,
            encoding = "utf-8",
        };
    }

    public async Task<object> SaveFileAsAsync(string? suggestedName, string? content)
    {
        ValidateContent(content);
        var safeSuggestedName = GetSafeSuggestedName(suggestedName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(safeSuggestedName),
            DefaultFileExtension = Path.GetExtension(safeSuggestedName),
        };
        picker.FileTypeChoices.Add("Markdown files", new List<string> { ".md", ".markdown" });
        picker.FileTypeChoices.Add("Mermaid files", new List<string> { ".mmd", ".mermaid" });
        picker.FileTypeChoices.Add("Text files", new List<string> { ".txt" });
        InitializeWithWindow.Initialize(picker, ownerWindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return new { cancelled = true };
        }

        ValidatePath(file.Path);
        await File.WriteAllTextAsync(file.Path, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var handleId = CreateHandle(file.Path);
        return new
        {
            cancelled = false,
            saved = true,
            name = file.Name,
            displayName = file.Name,
            encoding = "utf-8",
            nativeHandleId = handleId,
        };
    }

    private static void ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new NativeFileException("The selected file is unavailable.");
        }

        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NativeFileException("Choose a .md, .markdown, .mmd, .mermaid, or .txt file.");
        }
    }

    private static void ValidateContent(string? content)
    {
        var byteCount = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (byteCount > MaxFileBytes)
        {
            throw new NativeFileException("Save a UTF-8 text file up to 5 MB.");
        }
    }

    private static string GetSafeSuggestedName(string? suggestedName)
    {
        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(suggestedName) ? "document.md" : suggestedName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document.md";
        }

        return SupportedExtensions.Contains(Path.GetExtension(fileName))
            ? fileName
            : $"{Path.GetFileNameWithoutExtension(fileName)}.md";
    }

    private string CreateHandle(string path)
    {
        var handleId = Guid.NewGuid().ToString("N");
        nativeHandles[handleId] = path;
        return handleId;
    }
}

public sealed class NativeFileException : Exception
{
    public NativeFileException(string message)
        : base(message)
    {
    }
}
