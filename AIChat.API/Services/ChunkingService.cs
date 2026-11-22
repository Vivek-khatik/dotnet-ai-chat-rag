
namespace AIChat.API.Services;

public class ChunkingService
{
    public List<string> SplitIntoChunks(
        string text,
        int chunkSize = 30,
        int overlap = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        if (chunkSize <= 0)
            throw new ArgumentException(
                "Chunk size must be greater than zero.");

        if (overlap < 0 || overlap >= chunkSize)
            throw new ArgumentException(
                "Overlap must be non-negative and smaller than chunk size.");

        var words = text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);

        var chunks = new List<string>();

        int step = chunkSize - overlap;

        for (int i = 0; i < words.Length; i += step)
        {
            var chunkWords = words
                .Skip(i)
                .Take(chunkSize);

            chunks.Add(string.Join(" ", chunkWords));

            if (i + chunkSize >= words.Length)
                break;
        }

        return chunks;
    }
}
