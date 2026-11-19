using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ZeugnisManager.Admin.Services;

/// <summary>
/// JSON verisini ayni klasorde gecici dosyaya yazar, diske aktarir ve tek hamlede
/// hedef dosyanin yerine koyar. Mevcut dosya her basarili kayittan once .bak olur.
/// </summary>
internal static class AtomicJsonFile
{
    private static readonly ConcurrentDictionary<string, object> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Write(string path, string json)
    {
        // Bozuk veya eksik JSON'un gecerli dosyanin uzerine yazilmasini engelle.
        using (JsonDocument.Parse(json)) { }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Kayit klasoru belirlenemedi.");
        var gate = FileLocks.GetOrAdd(fullPath, static _ => new object());

        lock (gate)
        {
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            var backupPath = fullPath + ".bak";

            try
            {
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    16 * 1024,
                    FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(fullPath))
                    File.Copy(fullPath, backupPath, overwrite: true);

                File.Move(tempPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
