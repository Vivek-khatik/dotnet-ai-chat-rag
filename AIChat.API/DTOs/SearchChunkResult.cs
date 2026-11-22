namespace AIChat.API.DTOs;

public class SearchChunkResult
{
    public float Score { get; set; }
    public string? DocumentId { get; set; }
    public string? FileName { get; set; }
    public long? ChunkIndex { get; set; }
    public string? Text { get; set; }
}