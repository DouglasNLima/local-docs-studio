using System.Diagnostics;

namespace LensDocsStudio.Windows.Services;

internal static class NativeExplorerService
{
    public static object Reveal(string path, bool selectFile)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new NativeFileException("The selected location is unavailable.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            throw new NativeFileException("The selected location is unavailable.");
        }

        if (selectFile)
        {
            if (!File.Exists(fullPath))
            {
                throw new NativeFileException("The selected file is unavailable.");
            }

            var containingDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(containingDirectory) || !Directory.Exists(containingDirectory))
            {
                throw new NativeFileException("The selected file folder is unavailable.");
            }
        }
        else if (!Directory.Exists(fullPath))
        {
            throw new NativeFileException("The selected folder is unavailable.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add(selectFile ? $"/select,{fullPath}" : fullPath);

        try
        {
            Process.Start(startInfo);
        }
        catch
        {
            throw new NativeFileException("File Explorer could not open the selected location.");
        }

        return new
        {
            revealed = true,
            selected = selectFile,
            targetKind = selectFile ? "file" : "directory",
        };
    }
}
