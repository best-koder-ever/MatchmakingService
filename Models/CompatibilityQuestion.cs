namespace MatchmakingService.Models;

public class CompatibilityQuestion
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
