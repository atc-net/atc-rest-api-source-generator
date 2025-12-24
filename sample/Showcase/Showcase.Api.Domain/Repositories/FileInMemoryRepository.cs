namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// In-memory repository for file storage with sample files.
/// Demonstrates file upload/download operations.
/// </summary>
public sealed class FileInMemoryRepository
{
    private readonly Dictionary<string, StoredFile> files = new(StringComparer.OrdinalIgnoreCase);
    private long nextId = 1;

    public FileInMemoryRepository()
    {
        // Add sample text files
        AddSampleFile(
            "sample-readme.txt",
            "text/plain",
            """
            Welcome to the Showcase API File Storage!
            ==========================================

            This is a sample text file stored in the in-memory repository.
            You can use the file endpoints to:
            - GET /files/{id} - Retrieve a file by ID
            - POST /files/form-data/singleFile - Upload a single file
            - POST /files/form-data/multiFile - Upload multiple files
            - POST /files/form-data/singleObject - Upload file with metadata
            - POST /files/form-data/singleObjectMultiFile - Upload multiple files with metadata

            Happy coding!
            """u8.ToArray());

        AddSampleFile(
            "config-example.json",
            "application/json",
            """
            {
              "appName": "Showcase API",
              "version": "1.0.0",
              "settings": {
                "maxFileSize": 10485760,
                "allowedExtensions": [".txt", ".json", ".xml", ".png", ".jpg"],
                "storageType": "in-memory"
              },
              "features": {
                "fileUpload": true,
                "multiFileUpload": true,
                "formDataUpload": true
              }
            }
            """u8.ToArray());

        AddSampleFile(
            "data-sample.xml",
            "application/xml",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <files>
              <file id="1">
                <name>sample-readme.txt</name>
                <type>text/plain</type>
              </file>
              <file id="2">
                <name>config-example.json</name>
                <type>application/json</type>
              </file>
            </files>
            """u8.ToArray());

        // Add sample PNG image (100x100 gradient)
        AddSampleFile(
            "sample-image.png",
            "image/png",
            GenerateGradientPng(100, 100, (255, 100, 100), (100, 100, 255)));

        // Add another sample PNG (64x64 checkerboard)
        AddSampleFile(
            "checkerboard.png",
            "image/png",
            GenerateCheckerboardPng(64, 64, 8));
    }

    private void AddSampleFile(
        string fileName,
        string contentType,
        byte[] content)
    {
        var id = nextId++.ToString(CultureInfo.InvariantCulture);
        files[id] = new StoredFile(id, fileName, contentType, content, DateTimeOffset.UtcNow);
    }

    public Task<StoredFile?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        files.TryGetValue(id, out var file);
        return Task.FromResult(file);
    }

    public Task<StoredFile[]> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(files.Values.ToArray());
    }

    public Task<StoredFile> SaveAsync(
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var id = nextId++.ToString(CultureInfo.InvariantCulture);
        var file = new StoredFile(id, fileName, contentType, content, DateTimeOffset.UtcNow);
        files[id] = file;
        return Task.FromResult(file);
    }

    public Task<StoredFile[]> SaveMultipleAsync(
        IEnumerable<(string FileName, string ContentType, byte[] Content)> fileInfos,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var savedFiles = new List<StoredFile>();

        foreach (var (fileName, contentType, content) in fileInfos)
        {
            var id = nextId++.ToString(CultureInfo.InvariantCulture);
            var file = new StoredFile(id, fileName, contentType, content, DateTimeOffset.UtcNow);
            files[id] = file;
            savedFiles.Add(file);
        }

        return Task.FromResult(savedFiles.ToArray());
    }

    public Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(files.Remove(id));
    }

    /// <summary>
    /// Generates a PNG image with a gradient from one color to another.
    /// </summary>
    private static byte[] GenerateGradientPng(
        int width,
        int height,
        (byte R, byte G, byte B) colorStart,
        (byte R, byte G, byte B) colorEnd)
    {
        // Create raw RGB pixel data
        var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            var t = (float)y / (height - 1);
            var r = (byte)(colorStart.R + (t * (colorEnd.R - colorStart.R)));
            var g = (byte)(colorStart.G + (t * (colorEnd.G - colorStart.G)));
            var b = (byte)(colorStart.B + (t * (colorEnd.B - colorStart.B)));

            for (var x = 0; x < width; x++)
            {
                var idx = ((y * width) + x) * 3;
                pixels[idx] = r;
                pixels[idx + 1] = g;
                pixels[idx + 2] = b;
            }
        }

        return CreatePng(width, height, pixels);
    }

    /// <summary>
    /// Generates a PNG image with a checkerboard pattern.
    /// </summary>
    private static byte[] GenerateCheckerboardPng(
        int width,
        int height,
        int squareSize)
    {
        var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isWhite = ((x / squareSize) + (y / squareSize)) % 2 == 0;
                var color = isWhite ? (byte)255 : (byte)0;
                var idx = ((y * width) + x) * 3;
                pixels[idx] = color;
                pixels[idx + 1] = color;
                pixels[idx + 2] = color;
            }
        }

        return CreatePng(width, height, pixels);
    }

    /// <summary>
    /// Creates a valid PNG file from raw RGB pixel data.
    /// </summary>
    private static byte[] CreatePng(
        int width,
        int height,
        byte[] rgbPixels)
    {
        using var ms = new MemoryStream();

        // PNG signature
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR chunk
        WriteChunk(ms, "IHDR"u8, writer =>
        {
            writer.Write(ToBigEndian(width));
            writer.Write(ToBigEndian(height));
            writer.Write((byte)8);  // Bit depth
            writer.Write((byte)2);  // Color type (RGB)
            writer.Write((byte)0);  // Compression
            writer.Write((byte)0);  // Filter
            writer.Write((byte)0);  // Interlace
        });

        // IDAT chunk - compress the image data
        var rawData = new byte[(width * 3 * height) + height]; // +height for filter bytes
        var rawIdx = 0;
        for (var y = 0; y < height; y++)
        {
            rawData[rawIdx++] = 0; // Filter type: None
            for (var x = 0; x < width * 3; x++)
            {
                rawData[rawIdx++] = rgbPixels[(y * width * 3) + x];
            }
        }

        using (var compressedStream = new MemoryStream())
        {
            using (var deflate = new System.IO.Compression.DeflateStream(compressedStream, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(rawData, 0, rawData.Length);
            }

            var compressed = compressedStream.ToArray();

            // Write zlib header + compressed data + adler32
            WriteChunk(ms, "IDAT"u8, writer =>
            {
                writer.Write((byte)0x78); // zlib header
                writer.Write((byte)0x9C); // zlib header
                writer.Write(compressed);
                writer.Write(ToBigEndian(Adler32(rawData)));
            });
        }

        // IEND chunk
        WriteChunk(ms, "IEND"u8, _ => { });

        return ms.ToArray();
    }

    private static void WriteChunk(
        MemoryStream ms,
        ReadOnlySpan<byte> type,
        Action<BinaryWriter> writeData)
    {
        using var dataStream = new MemoryStream();
        using var writer = new BinaryWriter(dataStream);
        writeData(writer);
        writer.Flush();
        var data = dataStream.ToArray();

        // Length
        ms.Write(ToBigEndian(data.Length));

        // Type + Data for CRC
        var typeAndData = new byte[4 + data.Length];
        type.CopyTo(typeAndData);
        data.CopyTo(typeAndData, 4);

        ms.Write(typeAndData);

        // CRC32
        ms.Write(ToBigEndian((int)Crc32(typeAndData)));
    }

    private static byte[] ToBigEndian(int value)
        => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }

    private static int Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }

        return (int)((b << 16) | a);
    }
}