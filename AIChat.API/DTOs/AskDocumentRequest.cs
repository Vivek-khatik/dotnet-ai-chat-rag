namespace AIChat.API.DTOs;

public class AskDocumentRequest
{
    public string Question { get; set; } = string.Empty;

    public int Limit { get; set; } = 5;

    public float ScoreThreshold { get; set; } = 0.65f;
}