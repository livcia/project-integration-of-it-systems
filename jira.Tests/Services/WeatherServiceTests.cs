using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xunit;
using jira.Services;

namespace jira.Tests.Services;

public class WeatherServiceTests
{
    [Fact]
    public async Task GetCurrentWeatherAsync_SuccessfulResponse_ReturnsCurrentWeather()
    {
        // Arrange
        var expectedResponse = new OpenMeteoResponse
        {
            Current = new CurrentWeather
            {
                Temperature = 21.3,
                WeatherCode = 1,
                WindSpeed = 12.5,
                Humidity = 65
            }
        };

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(expectedResponse),
                    System.Text.Encoding.UTF8,
                    "application/json"
                )
            })
            .Verifiable();

        var httpClient = new HttpClient(handlerMock.Object);
        var weatherService = new WeatherService(httpClient);

        // Act
        var result = await weatherService.GetCurrentWeatherAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.Current.Temperature, result.Temperature);
        Assert.Equal(expectedResponse.Current.WeatherCode, result.WeatherCode);
        Assert.Equal(expectedResponse.Current.WindSpeed, result.WindSpeed);
        Assert.Equal(expectedResponse.Current.Humidity, result.Humidity);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_HttpErrorResponse_ReturnsNull()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            })
            .Verifiable();

        var httpClient = new HttpClient(handlerMock.Object);
        var weatherService = new WeatherService(httpClient);

        // Act
        var result = await weatherService.GetCurrentWeatherAsync();

        // Assert
        Assert.Null(result);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ExceptionThrown_ReturnsNull()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("API Unavailable"))
            .Verifiable();

        var httpClient = new HttpClient(handlerMock.Object);
        var weatherService = new WeatherService(httpClient);

        // Act
        var result = await weatherService.GetCurrentWeatherAsync();

        // Assert
        Assert.Null(result);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Theory]
    [InlineData(0, "Bezchmurnie", "☀️")]
    [InlineData(61, "Deszcz", "🌧️")]
    [InlineData(95, "Burza", "⛈️")]
    [InlineData(-1, "Nieznana pogoda", "🌡️")]
    [InlineData(999, "Nieznana pogoda", "🌡️")]
    public void GetWeatherDescription_ReturnsExpectedDescriptionAndEmoji(int code, string expectedDescription, string expectedEmoji)
    {
        // Act
        var (description, emoji) = WeatherService.GetWeatherDescription(code);

        // Assert
        Assert.Equal(expectedDescription, description);
        Assert.Equal(expectedEmoji, emoji);
    }
}
