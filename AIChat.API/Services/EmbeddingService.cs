
using Google.GenAI;
using Google.GenAI.Types;

namespace AIChat.API.Services;

public class EmbeddingService
{
    private readonly Client _client;

    public EmbeddingService(IConfiguration configuration)
    {
        var apiKey = configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is missing.");
        }

        _client = new Client(apiKey: apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _client.Models.EmbedContentAsync(
            model: "gemini-embedding-2",
            contents: $"title: none | text: {text}",
            config: new EmbedContentConfig
            {
                OutputDimensionality = 768
            }
        );

        return response.Embeddings[0].Values
            .Select(value => (float)value)
            .ToArray();
    }
}
