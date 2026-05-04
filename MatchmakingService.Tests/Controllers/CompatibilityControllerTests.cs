using System.Security.Claims;
using MatchmakingService.Controllers;
using MatchmakingService.Data;
using MatchmakingService.DTOs;
using MatchmakingService.Models;
using MatchmakingService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MatchmakingService.Tests.Controllers;

/// <summary>
/// Unit tests for CompatibilityController.
/// Covers: GET /api/compatibility/questions, POST /api/compatibility/answers,
/// GET /api/compatibility/answers/{keycloakId}, GET /api/compatibility/score/{otherKeycloakId}.
/// T518 — spec 005.
/// </summary>
public class CompatibilityControllerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;
    private readonly Mock<ICompatibilityScorer> _scorerMock;

    public CompatibilityControllerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CompatibilityCtrl_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(dbOptions);
        _scorerMock = new Mock<ICompatibilityScorer>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    private CompatibilityController CreateController(string? keycloakId = null)
    {
        var controller = new CompatibilityController(
            _context,
            _scorerMock.Object,
            NullLogger<CompatibilityController>.Instance);

        var httpContext = new DefaultHttpContext();

        if (keycloakId != null)
        {
            var claims = new[] { new Claim("sub", keycloakId) };
            var identity = new ClaimsIdentity(claims, "Bearer");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private async Task<CompatibilityQuestion> SeedQuestion(
        string text = "Sample question", bool isActive = true, int sortOrder = 1)
    {
        var q = new CompatibilityQuestion
        {
            QuestionText = text,
            IsActive = isActive,
            SortOrder = sortOrder
        };
        _context.CompatibilityQuestions.Add(q);
        await _context.SaveChangesAsync();
        return q;
    }

    private async Task<CompatibilityAnswer> SeedAnswer(int questionId, string keycloakId, int value = 3)
    {
        var a = new CompatibilityAnswer
        {
            QuestionId = questionId,
            KeycloakId = keycloakId,
            AnswerValue = value
        };
        _context.CompatibilityAnswers.Add(a);
        await _context.SaveChangesAsync();
        return a;
    }

    // ─── GET /api/compatibility/questions ───────────────────────────────────

    [Fact]
    public async Task GetQuestions_Unauthenticated_Returns401()
    {
        var controller = CreateController(keycloakId: null); // no claims
        var result = await controller.GetQuestions();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetQuestions_Authenticated_ReturnsActiveQuestionsInSortOrder()
    {
        await SeedQuestion("Q3", isActive: true, sortOrder: 3);
        await SeedQuestion("Q1", isActive: true, sortOrder: 1);
        await SeedQuestion("Q2", isActive: true, sortOrder: 2);
        await SeedQuestion("Hidden", isActive: false, sortOrder: 0);

        var controller = CreateController("user-a");
        var result = await controller.GetQuestions();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<CompatibilityQuestion>>(ok.Value);
        var ordered = list.ToList();

        Assert.Equal(3, ordered.Count); // inactive question excluded
        Assert.Equal("Q1", ordered[0].QuestionText);
        Assert.Equal("Q2", ordered[1].QuestionText);
        Assert.Equal("Q3", ordered[2].QuestionText);
    }

    [Fact]
    public async Task GetQuestions_ExcludesInactiveQuestions()
    {
        await SeedQuestion("Active", isActive: true, sortOrder: 1);
        await SeedQuestion("Inactive", isActive: false, sortOrder: 2);

        var controller = CreateController("user-x");
        var result = await controller.GetQuestions();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<CompatibilityQuestion>>(ok.Value).ToList();
        Assert.All(list, q => Assert.True(q.IsActive));
    }

    // ─── POST /api/compatibility/answers ────────────────────────────────────

    [Fact]
    public async Task UpsertAnswer_Unauthenticated_Returns401()
    {
        var controller = CreateController(keycloakId: null);
        var result = await controller.UpsertAnswer(new UpsertAnswerRequest(1, 3));
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpsertAnswer_UnknownQuestionId_Returns404()
    {
        var controller = CreateController("user-b");
        var result = await controller.UpsertAnswer(new UpsertAnswerRequest(9999, 3));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpsertAnswer_FirstCall_InsertsNewRow()
    {
        var q = await SeedQuestion();
        var controller = CreateController("user-c");

        var result = await controller.UpsertAnswer(new UpsertAnswerRequest(q.Id, 4));

        Assert.IsType<OkResult>(result);
        var answer = await _context.CompatibilityAnswers
            .FirstOrDefaultAsync(a => a.KeycloakId == "user-c" && a.QuestionId == q.Id);
        Assert.NotNull(answer);
        Assert.Equal(4, answer.AnswerValue);
    }

    [Fact]
    public async Task UpsertAnswer_SecondCall_UpdatesSameRow()
    {
        var q = await SeedQuestion();
        var controller = CreateController("user-d");

        // First call — insert
        await controller.UpsertAnswer(new UpsertAnswerRequest(q.Id, 2));
        var countAfterInsert = await _context.CompatibilityAnswers
            .CountAsync(a => a.KeycloakId == "user-d" && a.QuestionId == q.Id);
        Assert.Equal(1, countAfterInsert);

        // Second call — should update, not insert
        await controller.UpsertAnswer(new UpsertAnswerRequest(q.Id, 5));
        var countAfterUpdate = await _context.CompatibilityAnswers
            .CountAsync(a => a.KeycloakId == "user-d" && a.QuestionId == q.Id);
        Assert.Equal(1, countAfterUpdate); // still only 1 row

        var updated = await _context.CompatibilityAnswers
            .FirstAsync(a => a.KeycloakId == "user-d" && a.QuestionId == q.Id);
        Assert.Equal(5, updated.AnswerValue);
    }

    // ─── GET /api/compatibility/answers/{keycloakId} ────────────────────────

    [Fact]
    public async Task GetAnswers_Unauthenticated_Returns401()
    {
        var controller = CreateController(keycloakId: null);
        var result = await controller.GetAnswers("some-user");
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetAnswers_OtherUser_Returns403()
    {
        var controller = CreateController("user-e"); // caller is user-e
        var result = await controller.GetAnswers("user-f"); // requesting user-f's answers
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetAnswers_OwnId_ReturnsCallerAnswers()
    {
        var q1 = await SeedQuestion("Q1", sortOrder: 1);
        var q2 = await SeedQuestion("Q2", sortOrder: 2);
        await SeedAnswer(q1.Id, "user-g", value: 3);
        await SeedAnswer(q2.Id, "user-g", value: 5);
        await SeedAnswer(q1.Id, "other-user", value: 1); // another user's answer — should not appear

        var controller = CreateController("user-g");
        var result = await controller.GetAnswers("user-g");

        var ok = Assert.IsType<OkObjectResult>(result);
        var answers = Assert.IsAssignableFrom<IEnumerable<CompatibilityAnswer>>(ok.Value).ToList();
        Assert.Equal(2, answers.Count);
        Assert.All(answers, a => Assert.Equal("user-g", a.KeycloakId));
    }

    // ─── GET /api/compatibility/score/{otherKeycloakId} ─────────────────────

    [Fact]
    public async Task GetScore_Unauthenticated_Returns401()
    {
        var controller = CreateController(keycloakId: null);
        var result = await controller.GetScore("user-z");
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetScore_BothUsersHaveAnswers_ReturnsScorerResult()
    {
        var q = await SeedQuestion();
        await SeedAnswer(q.Id, "user-h", value: 4);
        await SeedAnswer(q.Id, "user-i", value: 3);

        var expectedDto = new CompatibilityScoreDto
        {
            UserId1 = "user-h",
            UserId2 = "user-i",
            OverallScore = 85,
            InterestsScore = 80,
            LocationScore = 90,
            PreferenceScore = 70
        };

        _scorerMock
            .Setup(s => s.Score(
                "user-h",
                "user-i",
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>(),
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>()))
            .Returns(expectedDto);

        var controller = CreateController("user-h");
        var result = await controller.GetScore("user-i");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CompatibilityScoreDto>(ok.Value);
        Assert.Equal(85, dto.OverallScore);
        Assert.Equal("user-h", dto.UserId1);
        Assert.Equal("user-i", dto.UserId2);
    }

    [Fact]
    public async Task GetScore_NoAnswers_ReturnsNeutralScoreFromScorer()
    {
        var neutralDto = new CompatibilityScoreDto
        {
            UserId1 = "user-j",
            UserId2 = "user-k",
            OverallScore = 50,
            InterestsScore = 50,
            LocationScore = 50,
            PreferenceScore = 50
        };

        _scorerMock
            .Setup(s => s.Score(
                "user-j",
                "user-k",
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>(),
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>()))
            .Returns(neutralDto);

        var controller = CreateController("user-j");
        var result = await controller.GetScore("user-k");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CompatibilityScoreDto>(ok.Value);
        Assert.Equal(50, dto.OverallScore);
    }

    [Fact]
    public async Task GetScore_PassesCorrectAnswersToScorer()
    {
        var q = await SeedQuestion();
        await SeedAnswer(q.Id, "user-l", value: 2);
        await SeedAnswer(q.Id, "user-m", value: 4);

        _scorerMock
            .Setup(s => s.Score(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>(),
                It.IsAny<IReadOnlyList<CompatibilityAnswer>>()))
            .Returns(new CompatibilityScoreDto { UserId1 = "user-l", UserId2 = "user-m" });

        var controller = CreateController("user-l");
        await controller.GetScore("user-m");

        _scorerMock.Verify(s => s.Score(
            "user-l",
            "user-m",
            It.Is<IReadOnlyList<CompatibilityAnswer>>(list => list.Count == 1 && list[0].KeycloakId == "user-l"),
            It.Is<IReadOnlyList<CompatibilityAnswer>>(list => list.Count == 1 && list[0].KeycloakId == "user-m")),
            Times.Once);
    }
}
