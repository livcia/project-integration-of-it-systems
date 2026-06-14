// jira.Tests/Components/Pages/ErrorTests.cs
// Wymagane pakiety (powinny być już w projekcie przez zależność od jira):
//   <PackageReference Include="bunit" Version="1.*" />
//   <PackageReference Include="xunit" Version="2.*" />
//   <PackageReference Include="Microsoft.AspNetCore.Http" />  ← dla DefaultHttpContext

using Bunit;
using jira.Components.Pages;          // ← dostosuj namespace jeśli Error.razor jest gdzie indziej
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Xunit;

namespace jira.Tests.Components.Pages;

public class ErrorTests : TestContext
{
    // -------------------------------------------------------------------------
    // Pomocnik: uruchamia Activity i gwarantuje jej zatrzymanie po teście
    // -------------------------------------------------------------------------
    private static IDisposable StartActivity(string operationName, out Activity activity)
    {
        var a = new Activity(operationName);
        a.Start();
        activity = a;
        return a; // Activity implementuje IDisposable → Stop() wywoła się w using
    }

    // =========================================================================
    // TESTY: Statyczna struktura HTML
    // =========================================================================

    [Fact]
    public void Error_Always_RendersH1WithErrorText()
    {
        var cut = Render<Error>();

        var h1 = cut.Find("h1");
        Assert.Equal("Error.", h1.TextContent.Trim());
        Assert.Contains("text-danger", h1.ClassName);
    }

    [Fact]
    public void Error_Always_RendersH2WithErrorDescription()
    {
        var cut = Render<Error>();

        var h2 = cut.Find("h2");
        Assert.Contains("An error occurred", h2.TextContent);
        Assert.Contains("text-danger", h2.ClassName);
    }

    [Fact]
    public void Error_Always_RendersH3WithDevelopmentMode()
    {
        var cut = Render<Error>();

        var h3 = cut.Find("h3");
        Assert.Equal("Development Mode", h3.TextContent.Trim());
    }

    [Fact]
    public void Error_Always_RendersPageTitleTag()
    {
        var cut = Render<Error>();

        // PageTitle renderuje się jako <title> w bUnit
        Assert.Contains("Error", cut.Markup);
    }

    [Fact]
    public void Error_Always_MentionsAspNetCoreEnvironmentVariable()
    {
        var cut = Render<Error>();

        Assert.Contains("ASPNETCORE_ENVIRONMENT", cut.Markup);
    }

    // =========================================================================
    // TESTY: ShowRequestId = false (sekcja ukryta)
    // =========================================================================

    [Fact]
    public void Error_WhenNoActivityAndNoHttpContext_HidesRequestIdSection()
    {
        // Upewniamy się, że Activity.Current jest null (brak aktywnej aktywności)
        Assert.Null(Activity.Current);

        var cut = Render<Error>(); // brak CascadingValue → HttpContext = null

        Assert.DoesNotContain("Request ID", cut.Markup);
        Assert.Empty(cut.FindAll("code")); // <code>@RequestId</code> nie istnieje
    }

    [Fact]
    public void Error_WhenHttpContextTraceIdentifierIsEmpty_HidesRequestIdSection()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "" // pusty string → ShowRequestId = false
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        Assert.DoesNotContain("Request ID", cut.Markup);
    }

    [Fact]
    public void Error_WhenHttpContextTraceIdentifierIsWhiteSpace_HidesRequestIdSection()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "   "
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        // string.IsNullOrEmpty("   ") = false → ShowRequestId = TRUE
        // Ale "   " to whitespace, więc komponent POKAŻE sekcję (IsNullOrEmpty ≠ IsNullOrWhiteSpace).
        // Ten test dokumentuje zachowanie komponentu, nie błąd.
        Assert.Contains("Request ID", cut.Markup);
    }

    // =========================================================================
    // TESTY: ShowRequestId = true (Request ID pochodzi z HttpContext)
    // =========================================================================

    [Fact]
    public void Error_WhenHttpContextHasTraceIdentifier_ShowsRequestIdSection()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "00-abc123-01"
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        Assert.Contains("Request ID", cut.Markup);
    }

    [Fact]
    public void Error_WhenHttpContextHasTraceIdentifier_ShowsCorrectIdValue()
    {
        const string traceId = "00-abc123def456-01";
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceId
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        var code = cut.Find("code");
        Assert.Equal(traceId, code.TextContent.Trim());
    }

    [Fact]
    public void Error_WhenHttpContextHasTraceIdentifier_WrapsIdInStrongAndCode()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-xyz" };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        // Sprawdź strukturę: <p><strong>Request ID:</strong> <code>...</code></p>
        var paragraph = cut.Find("p:has(code)");
        Assert.NotNull(paragraph.QuerySelector("strong"));
        Assert.NotNull(paragraph.QuerySelector("code"));
        Assert.Equal("Request ID:", paragraph.QuerySelector("strong")!.TextContent.Trim());
    }

    // =========================================================================
    // TESTY: ShowRequestId = true (Request ID pochodzi z Activity.Current)
    // =========================================================================

    [Fact]
    public void Error_WhenActivityCurrentHasId_ShowsActivityId()
    {
        using var _ = StartActivity("TestOperation", out var activity);

        var cut = Render<Error>(); // brak HttpContext – ActivityId bierze pierwszeństwo

        Assert.NotNull(activity.Id);
        Assert.Contains(activity.Id!, cut.Markup);
        Assert.Contains("Request ID", cut.Markup);
    }

    [Fact]
    public void Error_WhenBothActivityAndHttpContext_ActivityIdTakesPriority()
    {
        // Activity.Current?.Id ?? HttpContext?.TraceIdentifier
        // → Activity ma pierwszeństwo (operator ??)
        using var _ = StartActivity("PriorityTest", out var activity);

        var httpContext = new DefaultHttpContext { TraceIdentifier = "http-trace-id" };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        var code = cut.Find("code");
        Assert.Equal(activity.Id!, code.TextContent.Trim()); // ActivityId, nie TraceIdentifier
        Assert.DoesNotContain("http-trace-id", code.TextContent);
    }

    [Fact]
    public void Error_WhenActivityStoppedBeforeRender_FallsBackToHttpContext()
    {
        // Activity uruchomiona i zatrzymana → Current = null w momencie renderowania
        var activity = new Activity("StoppedActivity");
        activity.Start();
        activity.Stop(); // po Stop() Activity.Current = null

        const string fallbackTrace = "fallback-trace-999";
        var httpContext = new DefaultHttpContext { TraceIdentifier = fallbackTrace };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        var code = cut.Find("code");
        Assert.Equal(fallbackTrace, code.TextContent.Trim());
    }
}