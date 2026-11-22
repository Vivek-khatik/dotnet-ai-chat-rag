
using AIChat.API.DTOs;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AIChat.API.Services;

public class QdrantService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly string _embeddingCollectionName;
    private readonly string _nomicCollectionName;

    public QdrantService(IConfiguration configuration)
    {
        var host = configuration["Qdrant:Host"]
            ?? throw new InvalidOperationException(
                "Qdrant host is missing.");

        var port = configuration.GetValue<int?>(
            "Qdrant:GrpcPort") ?? 6334;

        _collectionName =
            configuration["Qdrant:CollectionName"]
            ?? "rag_demo";

        _client = new QdrantClient(host, port);

        _embeddingCollectionName =
    configuration["Qdrant:EmbeddingCollectionName"]
    ?? "documents_embeddings";

        _nomicCollectionName =
    configuration["Qdrant:NomicCollectionName"]
    ?? "documents_nomic";
    }

    public async Task CreateCollectionAsync()
    {
        var exists = await _client.CollectionExistsAsync(
            _collectionName);

        if (exists)
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _collectionName,
            new VectorParams
            {
                Size = 4,
                Distance = Distance.Cosine
            });
    }

    public async Task InsertSamplePointsAsync()
    {
        var points = new List<PointStruct>
    {
        new()
        {
            Id = 1,
            Vectors = new float[] { 0.9f, 0.1f, 0.1f, 0.0f },
            Payload =
            {
                ["text"] = "I enjoy playing football"
            }
        },
        new()
        {
            Id = 2,
            Vectors = new float[] { 0.85f, 0.15f, 0.1f, 0.0f },
            Payload =
            {
                ["text"] = "I like watching soccer"
            }
        },
        new()
        {
            Id = 3,
            Vectors = new float[] { 0.05f, 0.1f, 0.9f, 0.8f },
            Payload =
            {
                ["text"] = "SQL database connection failed"
            }
        }
    };

        await _client.UpsertAsync(_collectionName, points);
    }

    public async Task<List<object>> SearchSamplePointsAsync()
    {
        var queryVector = new float[]
        {
        0.88f, 0.12f, 0.1f, 0.0f
        };

        var results = await _client.SearchAsync(
            _collectionName,
            queryVector,
            limit: 2
        );

        return results.Select(point => (object)new
        {
            Id = point.Id,
            Score = point.Score,
            Text = point.Payload["text"].StringValue
        }).ToList();
    }


    //EmbeddingCollection
    public async Task CreateEmbeddingCollectionAsync()
    {
        var exists = await _client.CollectionExistsAsync(
            _embeddingCollectionName);

        if (exists)
            return;

        await _client.CreateCollectionAsync(
            _embeddingCollectionName,
            new VectorParams
            {
                Size = 768,
                Distance = Distance.Cosine
            });
    }

    public async Task StoreTextAsync(string text, float[] embedding)
    {
        var point = new PointStruct
        {
            Id = Guid.NewGuid(),
            Vectors = embedding,
            Payload =
        {
            ["text"] = text
        }
        };

        await _client.UpsertAsync(
            _embeddingCollectionName,
            new List<PointStruct> { point });
    }

    //public async Task<List<object>> SearchByVectorAsync(
    //    float[] queryEmbedding)
    //{
    //    var results = await _client.SearchAsync(
    //        _embeddingCollectionName,
    //        queryEmbedding,
    //        limit: 3);

    //    return results
    //        .Where(point => point.Score >= 0.65)
    //        .Select(point => (object)new
    //    {
    //        Id = point.Id,
    //        Score = point.Score,
    //        Text = point.Payload["text"].StringValue
    //    }).ToList();
    //}


    public async Task<List<SearchChunkResult>> SearchChunksAsync(
        float[] queryEmbedding,
        int limit = 5,
        float scoreThreshold = 0.65f)
    {
        var results = await _client.SearchAsync(
            _embeddingCollectionName,
            queryEmbedding,
            limit: (ulong)limit);

        var relevantResults = results
    .Where(point =>
        point.Score >= scoreThreshold &&
        point.Payload.ContainsKey("documentId") &&
        point.Payload.ContainsKey("fileName") &&
        point.Payload.ContainsKey("chunkIndex") &&
        point.Payload.ContainsKey("text"))
    .Select(point => new SearchChunkResult
    {
        Score = point.Score,
        DocumentId = point.Payload["documentId"].StringValue,
        FileName = point.Payload["fileName"].StringValue,
        ChunkIndex = point.Payload["chunkIndex"].IntegerValue,
        Text = point.Payload["text"].StringValue
    })
    .ToList();

        return relevantResults;
    }

    public async Task StoreChunksAsync(
        Guid documentId,
        string fileName,
        List<string> chunks,
        List<float[]> embeddings)
    {
        if (chunks.Count != embeddings.Count)
        {
            throw new ArgumentException(
                "Each chunk must have a corresponding embedding.");
        }

        var points = new List<PointStruct>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var point = new PointStruct
            {
                Id = Guid.NewGuid(),
                Vectors = embeddings[i],
                Payload =
            {
                ["documentId"] = documentId.ToString(),
                ["fileName"] = fileName,
                ["chunkIndex"] = i,
                ["text"] = chunks[i]
            }
            };

            points.Add(point);
        }

        await _client.UpsertAsync(
            _embeddingCollectionName,
            points);
    }

    public async Task CreateNomicEmbeddingCollectionAsync()
    {
        var exists = await _client.CollectionExistsAsync(
            _nomicCollectionName);

        if (exists)
            return;

        await _client.CreateCollectionAsync(
            _nomicCollectionName,
            new VectorParams
            {
                Size = 768,
                Distance = Distance.Cosine
            });
    }

    public async Task StoreNomicChunksAsync(
    Guid documentId,
    string fileName,
    List<string> chunks,
    List<float[]> embeddings)
    {
        if (chunks.Count != embeddings.Count)
        {
            throw new ArgumentException(
                "Each chunk must have a corresponding embedding.");
        }

        var points = new List<PointStruct>();

        for (int i = 0; i < chunks.Count; i++)
        {
            if (embeddings[i].Length != 768)
            {
                throw new ArgumentException(
                    "Nomic embeddings must contain exactly 768 dimensions.");
            }

            var point = new PointStruct
            {
                Id = Guid.NewGuid(),
                Vectors = embeddings[i],
                Payload =
            {
                ["documentId"] = documentId.ToString(),
                ["fileName"] = fileName,
                ["chunkIndex"] = i,
                ["text"] = chunks[i]
            }
            };

            points.Add(point);
        }

        await _client.UpsertAsync(
            _nomicCollectionName,
            points);
    }

    public async Task<List<SearchChunkResult>> SearchNomicChunksAsync(
    float[] queryEmbedding,
    int limit = 5,
    float scoreThreshold = 0.65f)
    {
        if (queryEmbedding.Length != 768)
        {
            throw new ArgumentException(
                "Nomic query embeddings must contain exactly 768 dimensions.");
        }

        var results = await _client.SearchAsync(
            _nomicCollectionName,
            queryEmbedding,
            limit: (ulong)limit);

        var relevantResults = results
            .Where(point =>
                point.Score >= scoreThreshold &&
                point.Payload.ContainsKey("documentId") &&
                point.Payload.ContainsKey("fileName") &&
                point.Payload.ContainsKey("chunkIndex") &&
                point.Payload.ContainsKey("text"))
            .Select(point => new SearchChunkResult
            {
                Score = point.Score,
                DocumentId = point.Payload["documentId"].StringValue,
                FileName = point.Payload["fileName"].StringValue,
                ChunkIndex = point.Payload["chunkIndex"].IntegerValue,
                Text = point.Payload["text"].StringValue
            })
            .ToList();

        return relevantResults;
    }
}
