using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MatchmakingService.Data;
using MatchmakingService.Models;
using MatchmakingService.Queries;

namespace MatchmakingService.Tests.Queries;

/// <summary>
/// T512 — Unit tests for GetUserAnswersHandler.
/// </summary>
public class GetUserAnswersHandlerTests : IDisposable
{
    private readonly MatchmakingDbContext _context;

    public GetUserAnswersHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MatchmakingDbContext>()
            .UseInMemoryDatabase($"GetAnswersTests_{Guid.NewGuid()}")
            .Options;
        _context = new MatchmakingDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetUserAnswers_ReturnsAnswersForUser()
    {
        _context.UserQuestionAnswers.AddRange(
            new UserQuestionAnswer { KeycloakId = "alice", QuestionId = 1, Value = 5, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" },
            new UserQuestionAnswer { KeycloakId = "alice", QuestionId = 2, Value = 3, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" },
            new UserQuestionAnswer { KeycloakId = "bob",   QuestionId = 1, Value = 7, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" }
        );
        await _context.SaveChangesAsync();

        var handler = new GetUserAnswersHandler(_context);
        var result = await handler.Handle(new GetUserAnswersQuery("alice"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.All(result.Data.Answers, a => Assert.Equal("alice", result.Data.KeycloakId));
    }

    [Fact]
    public async Task GetUserAnswers_EmptyWhenUserHasNoAnswers()
    {
        var handler = new GetUserAnswersHandler(_context);
        var result = await handler.Handle(new GetUserAnswersQuery("nobody"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.Count);
        Assert.Empty(result.Data.Answers);
    }

    [Fact]
    public async Task GetUserAnswers_OrderedByQuestionId()
    {
        _context.UserQuestionAnswers.AddRange(
            new UserQuestionAnswer { KeycloakId = "carol", QuestionId = 10, Value = 4, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" },
            new UserQuestionAnswer { KeycloakId = "carol", QuestionId = 3,  Value = 2, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" },
            new UserQuestionAnswer { KeycloakId = "carol", QuestionId = 7,  Value = 6, AnsweredAt = DateTime.UtcNow, AnswerType = "tap" }
        );
        await _context.SaveChangesAsync();

        var handler = new GetUserAnswersHandler(_context);
        var result = await handler.Handle(new GetUserAnswersQuery("carol"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var ids = result.Data!.Answers.Select(a => a.QuestionId).ToList();
        Assert.Equal(new[] { 3, 7, 10 }, ids);
    }
}
