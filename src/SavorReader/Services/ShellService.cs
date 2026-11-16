using System.Diagnostics;

namespace SavorReader.Services;

public sealed class ShellService
{
    public void OpenWithDefaultApp(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("书籍文件不存在。", filePath);
        }

        Process.Start(new ProcessStartInfo(filePath)
        {
            UseShellExecute = true,
        });
    }

    public void RevealInExplorer(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("书籍文件不存在。", filePath);
        }

        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"/select,\"{filePath}\"",
        });
    }

    public void OpenFolder(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            Arguments = $"\"{folderPath}\"",
        });
    }
}
