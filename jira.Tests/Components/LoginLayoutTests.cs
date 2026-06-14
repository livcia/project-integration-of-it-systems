using System;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using jira.Components.Layout;

namespace jira.Tests.Components.Layout;

public class LoginLayoutTests : BunitContext
{
    public LoginLayoutTests()
    {
        ComponentFactories.Add<HeadContent, EmptyComponent>();
    }

    [Fact]
    public void LoginLayout_RendersBodyContentCorrectly()
    {
        JSInterop.SetupVoid("eval", _ => true);

        var cut = Render<LoginLayout>(parameters => parameters
            .Add(l => l.Body,
                builder => builder.AddMarkupContent(0, "<div id='test-page-content'>Witaj na ekranie logowania</div>"))
        );

        var bodyContent = cut.Find("#test-page-content");
        Assert.Equal("Witaj na ekranie logowania", bodyContent.TextContent.Trim());
    }

    [Fact]
    public void LoginLayout_ExecutesJavaScriptStylesOnFirstRender()
    {
        var jsMock = JSInterop.SetupVoid("eval", _ => true);
        var cut = Render<LoginLayout>(parameters => parameters
            .Add(l => l.Body, builder => builder.AddMarkupContent(0, "<p>Główna zawartość</p>"))
        );

        jsMock.VerifyInvoke("eval", calledTimes: 1);

        var invokedCall = JSInterop.Invocations["eval"].FirstOrDefault();
        Assert.NotNull(invokedCall);

        string jsArgument = invokedCall.Arguments[0]?.ToString() ?? "";
        Assert.Contains("login-page-body", jsArgument);
        Assert.Contains("fontFamily", jsArgument);
    }

    private class EmptyComponent : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }
    }
}