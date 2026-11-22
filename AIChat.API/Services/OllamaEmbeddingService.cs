
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AIChat.API.Services;

public class OllamaEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OllamaEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public Task<float[]> GenerateDocumentEmbeddingAsync(string text)
    {
        return GenerateEmbeddingAsync($"search_document: {text}");
    }

    public Task<float[]> GenerateQueryEmbeddingAsync(string text)
    {
        return GenerateEmbeddingAsync($"search_query: {text}");
    }

    public async Task<float[]> GenerateEmbeddingAsync(string input)
    {
        var baseUrl = _configuration["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Ollama BaseUrl is missing.");

        var model = _configuration["Ollama:EmbeddingModel"]
            ?? throw new InvalidOperationException(
                "Ollama embedding model is missing.");

        var client = _httpClientFactory.CreateClient();

        var request = new
        {
            model,
            input
        };

        using var response = await client.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/embed",
            request);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Ollama embedding request failed: {responseBody}");
        }

        var result = System.Text.Json.JsonSerializer
            .Deserialize<EmbeddingResponse>(responseBody);

        var embedding = result?.Embeddings?.FirstOrDefault();

        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException(
                "Ollama returned an empty embedding.");
        }

        if (embedding.Length != 768)
        {
            throw new InvalidOperationException(
                $"Expected 768 embedding dimensions, but received {embedding.Length}.");
        }

        return embedding;
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }
}
