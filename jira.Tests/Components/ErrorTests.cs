using Bunit;
using jira.Components.Pages;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Xunit;

namespace jira.Tests.Components.Pages;

public class ErrorTests : TestContext
{
    private static IDisposable StartActivity(string operationName, out Activity activity)
    {
        var a = new Activity(operationName);
        a.Start();
        activity = a;
        return a;
    }

    [Fact]
    public void Error_Always_RendersStaticElementsProperly()
    {
        var cut = Render<Error>();

        var h1 = cut.Find("h1");
        Assert.Equal("Error.", h1.TextContent.Trim());
        Assert.Contains("text-danger", h1.ClassName);

        var h2 = cut.Find("h2");
        Assert.Contains("An error occurred", h2.TextContent);
        Assert.Contains("text-danger", h2.ClassName);

        var h3 = cut.Find("h3");
        Assert.Equal("Development Mode", h3.TextContent.Trim());

        Assert.Contains("ASPNETCORE_ENVIRONMENT", cut.Markup);
    }

    [Fact]
    public void Error_WhenNoActivityAndNoHttpContext_HidesRequestIdSection()
    {
        Assert.Null(Activity.Current);

        var cut = Render<Error>();

        Assert.DoesNotContain("Request ID", cut.Markup);
        Assert.Empty(cut.FindAll("code"));
    }

    [Fact]
    public void Error_WhenHttpContextTraceIdentifierIsEmpty_HidesRequestIdSection()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = ""
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        Assert.DoesNotContain("Request ID", cut.Markup);
    }

    [Fact]
    public void Error_WhenHttpContextHasTraceIdentifier_ShowsRequestIdSectionWithCorrectValue()
    {
        const string traceId = "00-abc123def456-01";

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceId
        };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        var paragraph = cut.Find("p:has(code)");

        var strong = paragraph.QuerySelector("strong");
        Assert.NotNull(strong);
        Assert.Equal("Request ID:", strong!.TextContent.Trim());

        var code = paragraph.QuerySelector("code");
        Assert.NotNull(code);
        Assert.Equal(traceId, code!.TextContent.Trim());
    }

    [Fact]
    public void Error_WhenActivityCurrentHasId_ShowsActivityId()
    {
        using var _ = StartActivity("TestOperation", out var activity);

        var cut = Render<Error>();

        Assert.NotNull(activity.Id);
        Assert.Contains(activity.Id!, cut.Markup);
        Assert.Contains("Request ID", cut.Markup);
    }

    [Fact]
    public void Error_WhenBothActivityAndHttpContext_ActivityIdTakesPriority()
    {
        using var _ = StartActivity("PriorityTest", out var activity);

        var httpContext = new DefaultHttpContext { TraceIdentifier = "http-trace-id" };

        var cut = Render<Error>(p => p.AddCascadingValue<HttpContext>(httpContext));

        var code = cut.Find("code");
        Assert.Equal(activity.Id!, code.TextContent.Trim());
        Assert.DoesNotContain("http-trace-id", code.TextContent);
    }
}