using System;
using System.IO;
using System.Text;
using FileOrganizer.Core.Extraction;
using Xunit;

namespace FileOrganizer.Core.Tests;

public sealed class ExtractionTests
{
    [Fact]
    public void FileTypeDetector_DetectsPdfSignature()
    {
        var path = CreateTempFile("sample.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 test"));
        var detector = new FileTypeDetector();

        var result = detector.Detect(path);

        Assert.Equal("application/pdf", result.DetectedMime);
        Assert.Equal("StructuredDocument", result.Category);
        Assert.True(result.SignatureMatched);
        Assert.True(result.Confidence > 0.9);
    }

    [Fact]
    public void FileTypeDetector_DetectsZipSignature()
    {
        var path = CreateTempFile("sample.zip", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 });
        var detector = new FileTypeDetector();

        var result = detector.Detect(path);

        Assert.Equal("application/zip", result.DetectedMime);
        Assert.Equal("Archive", result.Category);
        Assert.True(result.SignatureMatched);
    }

    [Theory]
    [InlineData("sample.cs", "text/plain", "CodeFile")]
    [InlineData("sample.json", "application/json", "StructuredDocument")]
    [InlineData("sample.txt", "text/plain", "TextDocument")]
    public void FileTypeDetector_FallsBackByExtension(string fileName, string expectedMime, string expectedCategory)
    {
        var path = CreateTempFile(fileName, Encoding.UTF8.GetBytes("hello"));
        var detector = new FileTypeDetector();

        var result = detector.Detect(path);

        Assert.Equal(expectedMime, result.DetectedMime);
        Assert.Equal(expectedCategory, result.Category);
        Assert.False(result.SignatureMatched);
        Assert.InRange(result.Confidence, 0.5, 0.7);
    }

    [Fact]
    public void TextFileHeuristics_RejectsObviousBinaryBytes()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0xFF };

        var result = TextFileHeuristics.LooksLikeText(bytes);

        Assert.False(result);
    }

    [Fact]
    public void TextFileExtractor_ExtractsPreviewAndLineCountFromUtf8Text()
    {
        var text = "# Header\nline one\nline two\n```csharp\nvar x = 1;\n```";
        var path = CreateTempFile("sample.md", Encoding.UTF8.GetBytes(text));
        var extractor = new TextFileExtractor();
        var detected = new DetectedFileType
        {
            Extension = ".md",
            DetectedMime = "text/markdown",
            Category = "TextDocument",
            Confidence = 0.6,
        };

        var result = extractor.Extract(path, detected);

        Assert.True(result.Status.Success);
        Assert.Equal(6, result.Content.LineCount);
        Assert.Contains("# Header", result.Content.TextPreview);
        Assert.Equal("utf-8", result.Content.Encoding);
        Assert.True(result.Structure.HasHeaders);
        Assert.True(result.Structure.HasCodeBlocks);
    }

    [Fact]
    public void TextFileExtractor_HandlesTruncatedReadsDeterministically()
    {
        const int MaxReadBytes = 256 * 1024;
        var content = new string('a', MaxReadBytes + 1024);
        var path = CreateTempFile("big.txt", Encoding.UTF8.GetBytes(content));
        var extractor = new TextFileExtractor();
        var detected = new DetectedFileType
        {
            Extension = ".txt",
            DetectedMime = "text/plain",
            Category = "TextDocument",
            Confidence = 0.6,
        };

        var result = extractor.Extract(path, detected);

        Assert.True(result.Status.Success);
        Assert.True(result.Status.Partial);
        Assert.NotNull(result.Content.TextPreview);
        Assert.Equal(4000, result.Content.TextPreview!.Length);
    }

    [Fact]
    public void ExtractionService_ReturnsFallbackArtifactWhenNoExtractorMatches()
    {
        var path = CreateTempFile("archive.bin", new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var detectedType = new DetectedFileType
        {
            Extension = ".bin",
            DetectedMime = "application/octet-stream",
            Category = "Unknown",
            Confidence = 0.1,
        };
        var detector = new StubDetector(detectedType);
        var dispatcher = new ExtractionDispatcher(Array.Empty<IFileExtractor>());
        var service = new ExtractionService(detector, dispatcher);

        var result = service.Extract(path);

        Assert.False(result.Status.Success);
        Assert.True(result.Status.Partial);
        Assert.Equal(".bin", result.FileType.Extension);
        Assert.Equal("application/octet-stream", result.FileType.DetectedMime);
        Assert.Equal("Unknown", result.FileType.Category);
        Assert.Equal(0.1, result.FileType.Confidence);
    }

    private static string CreateTempFile(string fileName, byte[] content)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private sealed class StubDetector : IFileTypeDetector
    {
        private readonly DetectedFileType _detectedType;

        public StubDetector(DetectedFileType detectedType)
        {
            _detectedType = detectedType;
        }

        public DetectedFileType Detect(string path) => _detectedType;
    }
}
