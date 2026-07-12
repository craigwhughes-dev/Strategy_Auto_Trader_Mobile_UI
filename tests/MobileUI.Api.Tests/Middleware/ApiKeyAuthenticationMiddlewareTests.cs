using MobileUI.Api.Middleware;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Middleware;

[TestFixture]
public class ApiKeyAuthenticationMiddlewareTests
{
    private HttpClient _client = null!;
    private string _validApiKey = null!;

    [SetUp]
    public void Setup()
    {
        _validApiKey = "test-api-key-12345";
        Environment.SetEnvironmentVariable("STRATEGY_API_KEY", _validApiKey);

        var handler = new MockAuthHandler();
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
    }

    [TearDown]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("STRATEGY_API_KEY", "");
        _client?.Dispose();
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutApiKey_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/positions");
        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithValidApiKey_Proceeds()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/positions")
        {
            Headers = { { "X-Api-Key", _validApiKey } }
        };

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.Not.EqualTo(System.Net.HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithInvalidApiKey_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/positions")
        {
            Headers = { { "X-Api-Key", "wrong-key" } }
        };

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UnprotectedEndpoint_WithoutApiKey_Proceeds()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.Not.EqualTo(System.Net.HttpStatusCode.Unauthorized));
    }
}

internal class MockAuthHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var middleware = new ApiKeyAuthenticationMiddleware(
            async ctx => { ctx.Response.StatusCode = 200; await Task.Delay(0); },
            new MockLogger<ApiKeyAuthenticationMiddleware>()
        );

        var context = new DefaultHttpContext();
        context.Request.Method = request.Method.Method;
        context.Request.Path = request.RequestUri?.AbsolutePath ?? "/";
        context.Request.Headers.Clear();

        foreach (var header in request.Headers)
        {
            context.Request.Headers.Add(header.Key, header.Value.ToArray());
        }

        await middleware.InvokeAsync(context);

        return new HttpResponseMessage((System.Net.HttpStatusCode)context.Response.StatusCode);
    }
}
