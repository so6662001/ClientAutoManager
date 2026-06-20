using Microsoft.Extensions.Logging.Abstractions;
using PuxunAppManager.Core.Backend;

namespace PuxunAppManager.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task Succeeds_FirstTry_NoRetry()
    {
        int calls = 0;
        var result = await RetryPolicy.ExecuteAsync(_ =>
        {
            calls++;
            return Task.FromResult(42);
        }, maxRetries: 4, NullLogger.Instance, CancellationToken.None, delayFn: (_, _) => Task.CompletedTask);

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retries_ThenSucceeds()
    {
        int calls = 0;
        var result = await RetryPolicy.ExecuteAsync(_ =>
        {
            calls++;
            if (calls < 3) throw new HttpRequestException("transient");
            return Task.FromResult("ok");
        }, maxRetries: 4, NullLogger.Instance, CancellationToken.None, delayFn: (_, _) => Task.CompletedTask);

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Throws_AfterExhaustingRetries()
    {
        int calls = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            RetryPolicy.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new HttpRequestException("always");
            }, maxRetries: 2, NullLogger.Instance, CancellationToken.None, delayFn: (_, _) => Task.CompletedTask));

        Assert.Equal(3, calls); // 1 次 + 2 次重试
    }
}
