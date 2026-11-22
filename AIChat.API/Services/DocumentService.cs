using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AIChat.API.Services;

public class DocumentService
{
    private readonly IWebHostEnvironment _environment;

    public DocumentService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<(string StoredFileName, string ExtractedText)>
        SaveAndExtractTextAsync(IFormFile file)
    {
        var uploadFolder = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads");

        Directory.CreateDirectory(uploadFolder);

        // Preserve the uploaded file extension.
        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        var filePath = Path.Combine(
            uploadFolder,
            storedFileName);

        // Save the uploaded file.
        await using (var stream = new FileStream(
            filePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Extract text according to the file extension.
        var extractedText = extension switch
        {
            ".txt" => await ExtractTextFromTxtAsync(filePath),

            ".pdf" => ExtractTextFromPdf(filePath),

            ".docx" => ExtractTextFromDocx(filePath),

            _ => throw new NotSupportedException(
                "This file format is not supported.")
        };

        return (storedFileName, extractedText);
    }

    private async Task<string> ExtractTextFromTxtAsync(
        string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    private string ExtractTextFromPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var pagesText = new List<string>();

        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page);

            pagesText.Add(pageText);
        }

        return string.Join(
            Environment.NewLine,
            pagesText);
    }

    private string ExtractTextFromDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(
            filePath,
            false);

        var body = document.MainDocumentPart?.Document?.Body;

        if (body == null)
        {
            return string.Empty;
        }

        // Extract paragraphs from the document body.
        // This also includes paragraphs inside tables.
        var paragraphs = body
            .Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText);

        return string.Join(
            Environment.NewLine,
            paragraphs);
    }
}