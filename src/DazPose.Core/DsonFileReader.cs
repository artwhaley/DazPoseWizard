using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DazPose.Core;

public sealed class DazConversionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class DsonFileReader
{
    public static JsonDocument ReadJson(string path)
    {
        if (!File.Exists(path))
            throw new DazConversionException($"File not found: '{path}'.");

        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> signature = stackalloc byte[2];
            var read = file.Read(signature);
            file.Position = 0;

            Stream content = read == 2 && signature[0] == 0x1f && signature[1] == 0x8b
                ? new GZipStream(file, CompressionMode.Decompress)
                : file;

            using (content)
            using (var reader = new StreamReader(content, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true))
            {
                var json = reader.ReadToEnd();
                return JsonDocument.Parse(json);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or DecoderFallbackException or JsonException or UnauthorizedAccessException)
        {
            throw new DazConversionException($"Could not read '{Path.GetFileName(path)}' as valid UTF-8 DSON JSON: {ex.Message}", ex);
        }
    }
}
