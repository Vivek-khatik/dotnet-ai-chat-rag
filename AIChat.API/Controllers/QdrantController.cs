
using Microsoft.AspNetCore.Mvc;
using AIChat.API.Services;
using AIChat.API.DTOs;

namespace AIChat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QdrantController : ControllerBase
{
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingService _embeddingService;

    public QdrantController(QdrantService qdrantService, EmbeddingService embeddingService)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
    }

    [HttpPost("create-collection")]
    public async Task<IActionResult> CreateCollection()
    {
        try
        {
            await _qdrantService.CreateCollectionAsync();

            return Ok(new
            {
                message = "Qdrant collection is ready!"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Unable to create Qdrant collection.",
                details = ex.Message
            });
        }
    }


    [HttpPost("insert-samples")]
    public async Task<IActionResult> InsertSamples()
    {
        await _qdrantService.InsertSamplePointsAsync();

        return Ok(new
        {
            message = "Sample points inserted successfully!"
        });
    }

    [HttpGet("search-samples")]
    public async Task<IActionResult> SearchSamples()
    {
        var results = await _qdrantService.SearchSamplePointsAsync();

        return Ok(results);
    }


    [HttpPost("create-embedding-collection")]
    public async Task<IActionResult> CreateEmbeddingCollection()
    {
        await _qdrantService.CreateEmbeddingCollectionAsync();

        return Ok(new
        {
            message = "Embedding collection created successfully!"
        });
    }

    [HttpPost("store-text")]
    public async Task<IActionResult> StoreText(
        [FromBody] EmbeddingRequest request)
    {
        var embedding = await _embeddingService
            .GenerateEmbeddingAsync(request.Text);

        await _qdrantService.StoreTextAsync(
            request.Text, embedding);

        return Ok(new
        {
            message = "Text and embedding stored successfully!",
            dimensions = embedding.Length
        });
    }

    //[HttpGet("search-text")]
    //public async Task<IActionResult> SearchText(
    //    [FromQuery] string query)
    //{
    //    var queryEmbedding = await _embeddingService
    //        .GenerateEmbeddingAsync(query);

    //    var results = await _qdrantService
    //        .SearchByVectorAsync(queryEmbedding);

    //    return Ok(results);
    //}


}
