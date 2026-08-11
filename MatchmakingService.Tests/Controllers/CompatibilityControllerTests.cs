using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MatchmakingService.Commands;
using MatchmakingService.Controllers;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Queries;
using MatchmakingService.Services;
using Moq;
using Xunit;

namespace MatchmakingService.Tests.Controllers;

public class CompatibilityControllerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly CompatibilityController _controller;
    private const string UserId = "user-keycloak-abc";

    public CompatibilityControllerTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"CompatibilityTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(options);

        // Build a mini DI container with real MediatR handlers
        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddSingleton<IRadarProfileCalculator, RadarProfileCalculator>();
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<GetCompatibilityQuestionsHandler>();
            cfg.RegisterServicesFromAssemblyContaining<SubmitAnswersHandler>();
            cfg.RegisterServicesFromAssemblyContaining<GetUserAnswersHandler>();
        });
        var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var mockUserClient = new Mock<IUserServiceClient>();
        var mockLogger = new Mock<ILogger<CompatibilityController>>();
        var radar = new RadarProfileCalculator(_context);
        _controller = new CompatibilityController(
            _context, mockUserClient.Object, mockLogger.Object, radar, sender);
        SetUser(UserId);

        // Seed a question
        _context.CompatibilityQuestions.Add(new CompatibilityQuestion
        {
            Id = 1, Category = QuestionCategory.Personality, Emoji = "😊",
            TextEn = "How social are you?", TextSv = "Hur social är du?",
            OptionsJson = "[]", SortOrder = 1, IsActive = true, Weight = 1.0
        });
        _context.CompatibilityQuestions.Add(new CompatibilityQuestion
        {
            Id = 2, Category = QuestionCategory.Values, Emoji = "💡",
            TextEn = "What do you value?", TextSv = "Vad värdesätter du?",
            OptionsJson = "[]", SortOrder = 2, IsActive = true, Weight = 1.0
        });
        _context.SaveChanges();
    }

    private void SetUser(string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ── GetQuestions ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestions_ReturnsActiveQuestionsGrouped()
    {
        var result = await _controller.GetQuestions();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("questions", json);
        Assert.Contains("grouped", json);
    }

    [Fact]
    public async Task GetQuestions_InactiveQuestionsExcluded()
    {
        _context.CompatibilityQuestions.Add(new CompatibilityQuestion
        {
            Id = 99, Category = QuestionCategory.Lifestyle, Emoji = "🏃",
            TextEn = "Inactive Q", TextSv = "Inaktiv F",
            OptionsJson = "[]", SortOrder = 99, IsActive = false, Weight = 1.0
        });
        await _context.SaveChangesAsync();

        var result = await _controller.GetQuestions();
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("Inactive Q", json);
    }

    // ── SubmitAnswers ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAnswers_ValidAnswers_Returns200()
    {
        var req = new SubmitAnswersRequest(new List<AnswerItem>
        {
            new(1, 5), new(2, 3)
        });
        var result = await _controller.SubmitAnswers(req);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"saved\":2", json);
        Assert.Contains("\"totalAnswered\":2", json);
    }

    [Fact]
    public async Task SubmitAnswers_Upsert_UpdatesExistingAnswer()
    {
        _context.UserQuestionAnswers.Add(new UserQuestionAnswer
        {
            KeycloakId = UserId, QuestionId = 1, Value = 1, AnsweredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var req = new SubmitAnswersRequest(new List<AnswerItem> { new(1, 7) });
        await _controller.SubmitAnswers(req);

        var ans = await _context.UserQuestionAnswers.FirstAsync(a => a.KeycloakId == UserId && a.QuestionId == 1);
        Assert.Equal(7, ans.Value);
        Assert.Equal(1, await _context.UserQuestionAnswers.CountAsync(a => a.KeycloakId == UserId));
    }

    [Fact]
    public async Task SubmitAnswers_UnknownQuestionId_Returns400()
    {
        var req = new SubmitAnswersRequest(new List<AnswerItem> { new(9999, 5) });
        var result = await _controller.SubmitAnswers(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SubmitAnswers_ValueOutOfRange_Returns400()
    {
        var req = new SubmitAnswersRequest(new List<AnswerItem> { new(1, 0) }); // value=0 invalid
        var result = await _controller.SubmitAnswers(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SubmitAnswers_EmptyList_Returns400()
    {
        var req = new SubmitAnswersRequest(new List<AnswerItem>());
        var result = await _controller.SubmitAnswers(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── GetAnswers ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAnswers_OwnAnswers_Returns200()
    {
        _context.UserQuestionAnswers.Add(new UserQuestionAnswer
        {
            KeycloakId = UserId, QuestionId = 1, Value = 4, AnsweredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _controller.GetAnswers(UserId);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"count\":1", json);
    }

    [Fact]
    public async Task GetAnswers_OtherUser_Returns403()
    {
        var result = await _controller.GetAnswers("other-user-id");
        Assert.IsType<ForbidResult>(result);
    }

    public void Dispose() => _context.Dispose();
}
