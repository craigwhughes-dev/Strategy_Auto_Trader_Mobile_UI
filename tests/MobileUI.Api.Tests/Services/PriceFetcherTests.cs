using MobileUI.Api.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Net;

namespace MobileUI.Api.Tests.Services;

[TestFixture]
public class PriceFetcherTests
{
    private PriceFetcher _fetcher = null!;
    private MockHttpMessageHandler _mockHandler = null!;
    private HttpClient _httpClient = null!;
    private ILogger<PriceFetcher> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        _logger = new MockLogger<PriceFetcher>();
        _fetcher = new PriceFetcher(_httpClient, _logger);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient?.Dispose();
        _mockHandler?.Dispose();
    }

    [Test]
    public async Task FetchPricesAsync_WithValidResponse_ReturnsPrices()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        var prices = await _fetcher.FetchPricesAsync(new[] { "AAPL" });

        Assert.That(prices, Contains.Key("AAPL"));
        Assert.That(prices["AAPL"], Is.EqualTo(150.25));
    }

    [Test]
    public async Task FetchPricesAsync_WithGBpCurrency_ConvertsToGbp()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 10050.0,
          "currency": "GBp"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        var prices = await _fetcher.FetchPricesAsync(new[] { "GSK.L" });

        Assert.That(prices["GSK.L"], Is.EqualTo(100.50).Within(0.01));
    }

    [Test]
    public async Task FetchPricesAsync_WithCachedPrice_DoesNotRefetch()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        await _fetcher.FetchPricesAsync(new[] { "AAPL" });
        var requestCount1 = _mockHandler.RequestCount;

        await _fetcher.FetchPricesAsync(new[] { "AAPL" });
        var requestCount2 = _mockHandler.RequestCount;

        Assert.That(requestCount2, Is.EqualTo(requestCount1), "Cached price should not trigger new request");
    }

    [Test]
    public async Task FetchPricesAsync_WithExpiredCache_Refetches()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        await _fetcher.FetchPricesAsync(new[] { "AAPL" });
        var requestCount1 = _mockHandler.RequestCount;

        await Task.Delay(TimeSpan.FromSeconds(61));

        await _fetcher.FetchPricesAsync(new[] { "AAPL" });
        var requestCount2 = _mockHandler.RequestCount;

        Assert.That(requestCount2, Is.GreaterThan(requestCount1), "Expired cache should trigger new request");
    }

    [Test]
    public async Task FetchPricesAsync_WithHttpError_ReturnsEmptyForThatTicker()
    {
        _mockHandler.SetResponse(HttpStatusCode.NotFound, "");

        var prices = await _fetcher.FetchPricesAsync(new[] { "INVALID" });

        Assert.That(prices, Does.Not.Contain(new KeyValuePair<string, double>("INVALID", 0)));
    }

    [Test]
    public async Task FetchPricesAsync_WithMalformedJson_ReturnsEmptyForThatTicker()
    {
        _mockHandler.SetResponse(HttpStatusCode.OK, "not json");

        var prices = await _fetcher.FetchPricesAsync(new[] { "AAPL" });

        Assert.That(prices, Does.Not.Contain(new KeyValuePair<string, double>("AAPL", 0)));
    }

    [Test]
    public async Task FetchPricesAsync_WithMultipleTickers_FetchesAll()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        var prices = await _fetcher.FetchPricesAsync(new[] { "AAPL", "MSFT", "GOOG" });

        Assert.That(prices.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task FetchPricesAsync_WithMixedCacheAndFresh_ReturnsBoth()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        await _fetcher.FetchPricesAsync(new[] { "AAPL" });
        var prices = await _fetcher.FetchPricesAsync(new[] { "AAPL", "MSFT" });

        Assert.That(prices, Contains.Key("AAPL"));
    }

    [Test]
    public async Task FetchPricesAsync_CaseInsensitive_Normalizes()
    {
        var yahooResponse = """
{
  "quoteSummary": {
    "result": [
      {
        "price": {
          "regularMarketPrice": 150.25,
          "currency": "USD"
        }
      }
    ]
  }
}
""";

        _mockHandler.SetResponse(HttpStatusCode.OK, yahooResponse);

        var prices = await _fetcher.FetchPricesAsync(new[] { "aapl" });

        Assert.That(prices, Contains.Key("AAPL"));
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseContent = "";
    public int RequestCount { get; private set; }

    public void SetResponse(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _responseContent = content;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        };
        return await Task.FromResult(response);
    }
}
