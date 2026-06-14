using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

public class DiceBearIntegrationTests
{
    private readonly HttpClient _httpClient = new();

    [Fact]
    public async Task GetAvatar_ShouldReturnSvgImage()
    {
        string url = "https://api.dicebear.com/10.x/lorelei/svg?seed=Malwina";

        HttpResponseMessage response = await _httpClient.GetAsync(url);

        Assert.NotNull(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode);

        string content = await response.Content.ReadAsStringAsync();

        Assert.False(string.IsNullOrWhiteSpace(content));
        Assert.Contains("<svg", content);
        Assert.Contains("</svg>", content);

        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }
}