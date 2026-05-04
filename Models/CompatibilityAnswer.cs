namespace MatchmakingService.Models;

public class CompatibilityAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string KeycloakId { get; set; } = string.Empty;
    public int AnswerValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
