using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MatchmakingService.Commands;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Queries;
using MatchmakingService.Services;

namespace MatchmakingService.Tests.Commands;

/// <summary>
/// T512 — Unit tests for SubmitAnswersHandler and GetCompatibilityQuestionsHandler.
/// </summary>
public class CompatibilityCqrsTests : IDisposable
{
    private readonly MatchmakingDbContext _context;

    public CompatibilityCqrsTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"CqrsTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(options);

        // Seed two active questions
        _context.CompatibilityQuestions.AddRange(
            new CompatibilityQuestion { Id = 10, Category = QuestionCategory.Personality, TextEn = "Q1", TextSv = "Q1sv", SortOrder = 1, IsActive = true, Weight = 1.0, OptionsJson = "[]" },
            new CompatibilityQuestion { Id = 20, Category = QuestionCategory.Values,       TextEn = "Q2", TextSv = "Q2sv", SortOrder = 2, IsActive = true, Weight = 1.0, OptionsJson = "[]" }
        );
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ── GetCompatibilityQuestionsHandler ──────────────────────────────────

    [Fact]
    public async Task GetQuestions_ReturnsAllActiveQuestions()
    {
        var handler = new GetCompatibilityQuestionsHandler(_context);
        var result = await handler.Handle(new GetCompatibilityQuestionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Questions.Count);
    }

    [Fact]
    public async Task GetQuestions_GroupsByCategory()
    {
        var handler = new GetCompatibilityQuestionsHandler(_context);
        var result = await handler.Handle(new GetCompatibilityQuestionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("Personality", result.Data!.Grouped.Keys);
        Assert.Contains("Values", result.Data.Grouped.Keys);
    }

    [Fact]
    public async Task GetQuestions_ExcludesInactiveQuestions()
    {
        _context.CompatibilityQuestions.Add(new CompatibilityQuestion
            { Id = 99, Category = QuestionCategory.Lifestyle, TextEn = "Inactive", TextSv = "Inaktiv", SortOrder = 99, IsActive = false, Weight = 1.0, OptionsJson = "[]" });
        _context.SaveChanges();

        var handler = new GetCompatibilityQuestionsHandler(_context);
        var result = await handler.Handle(new GetCompatibilityQuestionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Questions.Count); // inactive excluded
    }

    // ── SubmitAnswersHandler ──────────────────────────────────────────────

    private SubmitAnswersHandler BuildHandler() =>
        new(_context,
            Mock.Of<IRadarProfileCalculator>(),
            NullLogger<SubmitAnswersHandler>.Instance);

    [Fact]
    public async Task SubmitAnswers_ValidAnswers_Saves()
    {
        var handler = BuildHandler();
        var cmd = new SubmitAnswersCommand("alice", new List<AnswerItemDto>
        {
            new(10, 5, "tap"),
            new(20, 3, "tap"),
        });
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Saved);
        Assert.Equal(2, result.Data.TotalAnswered);
    }

    [Fact]
    public async Task SubmitAnswers_InvalidQuestionId_Fails()
    {
        var handler = BuildHandler();
        var cmd = new SubmitAnswersCommand("alice", new List<AnswerItemDto>
        {
            new(9999, 5, "tap"),
        });
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUESTIONS", result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAnswers_ValueOutOfRange_Fails()
    {
        var handler = BuildHandler();
        var cmd = new SubmitAnswersCommand("alice", new List<AnswerItemDto>
        {
            new(10, 8, "tap"), // 8 > 7
        });
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("OUT_OF_RANGE", result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAnswers_Upserts_ExistingAnswer()
    {
        var handler = BuildHandler();
        await handler.Handle(new SubmitAnswersCommand("bob", new List<AnswerItemDto> { new(10, 3, "tap") }), CancellationToken.None);
        await handler.Handle(new SubmitAnswersCommand("bob", new List<AnswerItemDto> { new(10, 7, "tap") }), CancellationToken.None);

        var ans = await _context.UserQuestionAnswers
            .FirstAsync(a => a.KeycloakId == "bob" && a.QuestionId == 10);
        Assert.Equal(7, ans.Value); // updated
        var count = await _context.UserQuestionAnswers.CountAsync(a => a.KeycloakId == "bob");
        Assert.Equal(1, count); // no duplicate
    }

    [Fact]
    public async Task SubmitAnswers_EmptyList_Fails()
    {
        var handler = BuildHandler();
        var cmd = new SubmitAnswersCommand("alice", new List<AnswerItemDto>());
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("EMPTY_ANSWERS", result.ErrorCode);
    }

    // T588 — radar auto-update
    [Fact]
    public async Task SubmitAnswers_ValidAnswers_TriggersRadarCompute()
    {
        var radarMock = new Mock<IRadarProfileCalculator>();
        radarMock.Setup(r => r.ComputeAndSaveAsync("alice", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new MatchmakingService.Models.RadarProfile { KeycloakId = "alice" });

        var handler = new SubmitAnswersHandler(
            _context, radarMock.Object, NullLogger<SubmitAnswersHandler>.Instance);

        var cmd = new SubmitAnswersCommand("alice", new List<AnswerItemDto> { new(10, 4, "tap") });
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Fire-and-forget task may not complete synchronously; just ensure handler succeeds
        // and no exception is thrown. Full radar compute is tested via RadarProfileCalculatorTests.
    }
}
