
using AIChat.API.DTOs;
using AIChat.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmbeddingController : ControllerBase
{
    private readonly EmbeddingService _embeddingService;

    public EmbeddingController(EmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateEmbedding(
        [FromBody] EmbeddingRequest request)
    {
        var embedding = await _embeddingService
            .GenerateEmbeddingAsync(request.Text);

        return Ok(new
        {
            inputText = request.Text,
            dimensions = embedding.Length,
            preview = embedding.Take(10)
        });
    }
}


