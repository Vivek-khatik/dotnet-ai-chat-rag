
using AIChat.API.DTOs;
using AIChat.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChat.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly ChunkingService _chunkingService;
    private readonly EmbeddingService _embeddingService;
    private readonly QdrantService _qdrantService;
    private readonly AIService _aiService;
    private readonly OllamaEmbeddingService _ollamaEmbeddingService;
    private readonly OllamaAIService _ollamaAIService;

    public DocumentController(
    DocumentService documentService,
    ChunkingService chunkingService,
    EmbeddingService embeddingService,
    QdrantService qdrantService,
    AIService aiService,
    OllamaEmbeddingService ollamaEmbeddingService,
    OllamaAIService ollamaAIService)
    {
        _documentService = documentService;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _aiService = aiService;
        _ollamaEmbeddingService = ollamaEmbeddingService;
        _ollamaAIService = ollamaAIService;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                error = "Please select a file."
            });
        }

        var allowedExtensions = new[] { ".txt", ".pdf", ".docx" };

        var extension = Path.GetExtension(file.FileName);

        if (!allowedExtensions.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "Only TXT, PDF and DOCX files are supported."
            });
        }

        var result = await _documentService
            .SaveAndExtractTextAsync(file);

        if (string.IsNullOrWhiteSpace(result.ExtractedText))
        {
            return BadRequest(new
            {
                error = "No readable text was found in the uploaded document."
            });
        }

        var chunks = _chunkingService.SplitIntoChunks(
    result.ExtractedText);


        // Generate a unique ID for this uploaded document.
        var documentId = Guid.NewGuid();

        // Generate an embedding for every chunk.
        var embeddings = new List<float[]>();

        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService
                .GenerateEmbeddingAsync(chunk);

            embeddings.Add(embedding);
        }

        // Ensure the embedding collection exists.
        await _qdrantService.CreateEmbeddingCollectionAsync();

        // Store chunks, embeddings and metadata in Qdrant.
        await _qdrantService.StoreChunksAsync(
            documentId,
            Path.GetFileName(file.FileName),
            chunks,
            embeddings);


        return Ok(new
        {
            message = "Document uploaded and indexed successfully!",
            documentId = documentId,
            originalFileName = Path.GetFileName(file.FileName),
            totalChunks = chunks.Count,
            totalEmbeddings = embeddings.Count,
            embeddingDimensions = embeddings.FirstOrDefault()?.Length ?? 0,
            chunks = chunks
        });
    }


    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 5,
        [FromQuery] float scoreThreshold = 0.65f)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new
            {
                error = "Search query cannot be empty."
            });
        }

        if (limit < 1 || limit > 20)
        {
            return BadRequest(new
            {
                error = "Limit must be between 1 and 20."
            });
        }

        if (scoreThreshold < 0 || scoreThreshold > 1)
        {
            return BadRequest(new
            {
                error = "Score threshold must be between 0 and 1."
            });
        }

        // Convert user's question into an embedding.
        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(query);

        // Search for matching chunks in Qdrant.
        var results = await _qdrantService.SearchChunksAsync(
            queryEmbedding,
            limit,
            scoreThreshold);

        return Ok(new
        {
            query,
            totalResults = results.Count,
            results
        });
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(
    [FromBody] AskDocumentRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new
            {
                error = "Please provide a question."
            });
        }

        if (request.Limit < 1 || request.Limit > 20)
        {
            return BadRequest(new
            {
                error = "Limit must be between 1 and 20."
            });
        }

        if (request.ScoreThreshold < 0 ||
            request.ScoreThreshold > 1)
        {
            return BadRequest(new
            {
                error = "Score threshold must be between 0 and 1."
            });
        }

        // Step 1: Convert the user's question into an embedding.
        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(request.Question);

        // Step 2: Retrieve relevant chunks from Qdrant.
        var retrievedChunks = await _qdrantService.SearchChunksAsync(
            queryEmbedding,
            request.Limit,
            request.ScoreThreshold);

        // Step 3: Send the question and retrieved context to Gemini.
        var answer = await _aiService.GetRagResponseAsync(
            request.Question,
            retrievedChunks);

        // Step 4: Return the answer along with its source chunks.
        return Ok(new
        {
            question = request.Question,
            answer,
            totalSources = retrievedChunks.Count,
            sources = retrievedChunks
        });
    }

    [HttpPost("upload-nomic")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadNomic(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                error = "Please select a file."
            });
        }

        var allowedExtensions = new[] { ".txt", ".pdf", ".docx" };

        var extension = Path.GetExtension(file.FileName);

        if (!allowedExtensions.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "Only TXT, PDF and DOCX files are supported."
            });
        }

        var result = await _documentService.SaveAndExtractTextAsync(file);

        if (string.IsNullOrWhiteSpace(result.ExtractedText))
        {
            return BadRequest(new
            {
                error = "No readable text was found in the uploaded document."
            });
        }

        var chunks = _chunkingService.SplitIntoChunks(
            result.ExtractedText);

        var documentId = Guid.NewGuid();

        var embeddings = new List<float[]>();

        foreach (var chunk in chunks)
        {
            var embedding = await _ollamaEmbeddingService
                .GenerateEmbeddingAsync(chunk);

            embeddings.Add(embedding);
        }

        await _qdrantService.CreateNomicEmbeddingCollectionAsync();

        await _qdrantService.StoreNomicChunksAsync(
            documentId,
            Path.GetFileName(file.FileName),
            chunks,
            embeddings);

        return Ok(new
        {
            message = "Document uploaded and indexed successfully using Ollama!",
            documentId,
            originalFileName = Path.GetFileName(file.FileName),
            totalChunks = chunks.Count,
            totalEmbeddings = embeddings.Count,
            embeddingDimensions = embeddings.FirstOrDefault()?.Length ?? 0,
            chunks
        });
    }
    [HttpGet("search-nomic")]
    public async Task<IActionResult> SearchNomic(
    [FromQuery] string query,
    [FromQuery] int limit = 5,
    [FromQuery] float scoreThreshold = 0.65f)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new
            {
                error = "Search query cannot be empty."
            });
        }

        if (limit < 1 || limit > 20)
        {
            return BadRequest(new
            {
                error = "Limit must be between 1 and 20."
            });
        }

        if (scoreThreshold < 0 || scoreThreshold > 1)
        {
            return BadRequest(new
            {
                error = "Score threshold must be between 0 and 1."
            });
        }

        var queryEmbedding = await _ollamaEmbeddingService
            .GenerateEmbeddingAsync(query);

        var results = await _qdrantService.SearchNomicChunksAsync(
            queryEmbedding,
            limit,
            scoreThreshold);

        return Ok(new
        {
            query,
            totalResults = results.Count,
            results
        });
    }
    [HttpPost("ask-nomic")]
    public async Task<IActionResult> AskNomic(
    [FromBody] AskDocumentRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new
            {
                error = "Please provide a question."
            });
        }

        if (request.Limit < 1 || request.Limit > 20)
        {
            return BadRequest(new
            {
                error = "Limit must be between 1 and 20."
            });
        }

        if (request.ScoreThreshold < 0 ||
            request.ScoreThreshold > 1)
        {
            return BadRequest(new
            {
                error = "Score threshold must be between 0 and 1."
            });
        }

        // Step 1: Generate query embedding using Ollama.
        var queryEmbedding = await _ollamaEmbeddingService
            .GenerateEmbeddingAsync(request.Question);

        // Step 2: Retrieve relevant chunks from Qdrant.
        List<SearchChunkResult> retrievedChunks =
            await _qdrantService.SearchNomicChunksAsync(
                queryEmbedding,
                request.Limit,
                request.ScoreThreshold);

        if (retrievedChunks.Count == 0)
        {
            return Ok(new
            {
                question = request.Question, 
                answer = "I couldn't find relevant information in the uploaded documents.",
                totalSources = 0,
                sources = retrievedChunks
            });
        }

        // Step 3: Convert retrieved chunks into a single context string.
        var context = string.Join(
            "\n\n",
            retrievedChunks.Select((chunk, index) =>
                $"Source {index + 1}:\n{chunk.Text}"));

        // Step 4: Generate answer using Ollama.
        var answer = await _ollamaAIService.GetRagResponseAsync(
            request.Question,
            context);

        // Step 5: Return answer and source chunks.
        return Ok(new
        {
            question = request.Question,
            answer,
            totalSources = retrievedChunks.Count,
            sources = retrievedChunks
        });
    }
}
