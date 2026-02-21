using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using MatchmakingService.Services;

namespace MatchmakingService.Tests.Services;

public class SwipeServiceClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly SwipeServiceClient _client;

    public SwipeServiceClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8087")
        };

        _client = new SwipeServiceClient(
            httpClient,
            Mock.Of<ILogger<SwipeServiceClient>>());
    }

    private void SetupResponse(HttpStatusCode statusCode, object? body = null)
    {
        var content = body != null
            ? new StringContent(JsonSerializer.Serialize(body))
            : new StringContent("");

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content
            });
    }

    // ===== GetSwipedUserIdsAsync =====

    [Fact]
    public async Task GetSwipedUserIds_Success_ReturnsIds()
    {
        var apiResponse = new
        {
            success = true,
            data = new
            {
                userId = 1,
                swipes = new[]
                {
                    new { id = 1, targetUserId = 10, isLike = true, createdAt = DateTime.UtcNow },
                    new { id = 2, targetUserId = 20, isLike = false, createdAt = DateTime.UtcNow },
                    new { id = 3, targetUserId = 30, isLike = true, createdAt = DateTime.UtcNow }
                },
                totalSwipes = 3,
                totalLikes = 2,
                totalPasses = 1
            }
        };
        SetupResponse(HttpStatusCode.OK, apiResponse);

        var result = await _client.GetSwipedUserIdsAsync(1);

        Assert.Contains(10, result);
        Assert.Contains(20, result);
        Assert.Contains(30, result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetSwipedUserIds_HttpError_ReturnsEmpty()
    {
        SetupResponse(HttpStatusCode.InternalServerError);

        var result = await _client.GetSwipedUserIdsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSwipedUserIds_NetworkException_ReturnsEmpty()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _client.GetSwipedUserIdsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSwipedUserIds_EmptySwipes_ReturnsEmpty()
    {
        var apiResponse = new
        {
            success = true,
            data = new
            {
                userId = 1,
                swipes = Array.Empty<object>(),
                totalSwipes = 0,
                totalLikes = 0,
                totalPasses = 0
            }
        };
        SetupResponse(HttpStatusCode.OK, apiResponse);

        var result = await _client.GetSwipedUserIdsAsync(1);

        Assert.Empty(result);
    }

    // ===== GetSwipeTrustScoreAsync =====

    [Fact]
    public async Task GetTrustScore_Success_ReturnsScore()
    {
        SetupResponse(HttpStatusCode.OK, new { userId = 1, trustScore = 85.5m });

        var result = await _client.GetSwipeTrustScoreAsync(1);

        Assert.Equal(85.5m, result);
    }

    [Fact]
    public async Task GetTrustScore_HttpError_ReturnsDefault100()
    {
        SetupResponse(HttpStatusCode.InternalServerError);

        var result = await _client.GetSwipeTrustScoreAsync(1);

        Assert.Equal(100m, result);
    }

    [Fact]
    public async Task GetTrustScore_Exception_ReturnsDefault100()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _client.GetSwipeTrustScoreAsync(1);

        Assert.Equal(100m, result);
    }

    // ===== GetBatchTrustScoresAsync =====

    [Fact]
    public async Task GetBatchTrustScores_Success_ReturnsScores()
    {
        var scores = new[]
        {
            new { userId = 1, trustScore = 90m },
            new { userId = 2, trustScore = 75m }
        };
        SetupResponse(HttpStatusCode.OK, scores);

        var result = await _client.GetBatchTrustScoresAsync(new[] { 1, 2, 3 });

        Assert.Equal(90m, result[1]);
        Assert.Equal(75m, result[2]);
        Assert.Equal(100m, result[3]); // Missing → default 100
    }

    [Fact]
    public async Task GetBatchTrustScores_EmptyInput_ReturnsEmpty()
    {
        var result = await _client.GetBatchTrustScoresAsync(Array.Empty<int>());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBatchTrustScores_HttpError_ReturnsDefaultsAll100()
    {
        SetupResponse(HttpStatusCode.InternalServerError);

        var result = await _client.GetBatchTrustScoresAsync(new[] { 1, 2 });

        Assert.Equal(100m, result[1]);
        Assert.Equal(100m, result[2]);
    }

    [Fact]
    public async Task GetBatchTrustScores_Exception_ReturnsDefaultsAll100()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _client.GetBatchTrustScoresAsync(new[] { 5, 10 });

        Assert.Equal(100m, result[5]);
        Assert.Equal(100m, result[10]);
    }
}
