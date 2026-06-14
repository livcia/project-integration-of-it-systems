using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace jira.Services;

public class WeatherService
{
    private const double Latitude = 54.5480;
    private const double Longitude = 18.5446;

    private static readonly string ApiUrl =
        $"https://api.open-meteo.com/v1/forecast" +
        $"?latitude=54.544306" +
        $"&longitude=18.546205" +
        $"&current=temperature_2m,weathercode,windspeed_10m,relativehumidity_2m" +
        $"&timezone=Europe%2FWarsaw";
    
    private readonly HttpClient _httpClient;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CurrentWeather?> GetCurrentWeatherAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OpenMeteoResponse>(ApiUrl);
            return response?.Current;
        }
        catch
        {
            return null;
        }
    }

    public static (string Description, string Emoji) GetWeatherDescription(int code) => code switch
    {
        0 => ("Bezchmurnie", "☀️"),
        1 => ("Przeważnie pogodnie", "🌤️"),
        2 => ("Częściowe zachmurzenie", "⛅"),
        3 => ("Pochmurno", "☁️"),
        45 or 48 => ("Mgła", "🌫️"),
        51 or 53 or 55 => ("Mżawka", "🌦️"),
        61 or 63 or 65 => ("Deszcz", "🌧️"),
        71 or 73 or 75 => ("Śnieg", "❄️"),
        80 or 81 or 82 => ("Przelotne opady", "🌦️"),
        95 => ("Burza", "⛈️"),
        96 or 99 => ("Burza z gradem", "⛈️"),
        _ => ("Nieznana pogoda", "🌡️"),
    };
}

public class OpenMeteoResponse
{
    [JsonPropertyName("current")] public CurrentWeather? Current { get; set; }
}

public class CurrentWeather
{
    [JsonPropertyName("temperature_2m")] public double Temperature { get; set; }

    [JsonPropertyName("weathercode")] public int WeatherCode { get; set; }

    [JsonPropertyName("windspeed_10m")] public double WindSpeed { get; set; }

    [JsonPropertyName("relativehumidity_2m")]
    public int Humidity { get; set; }
}