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
        // Rejestrujemy komponent pomocniczy dla HeadContent, który potrafi przyjąć ChildContent
        ComponentFactories.Add<HeadContent, EmptyComponent>();
    }

    [Fact]
    public void LoginLayout_RendersBodyContentCorrectly()
    {
        // Arrange
        JSInterop.SetupVoid("eval", _ => true);

        // Act
        var cut = Render<LoginLayout>(parameters => parameters
            .Add(l => l.Body, builder => builder.AddMarkupContent(0, "<div id='test-page-content'>Witaj na ekranie logowania</div>"))
        );

        // Assert
        var bodyContent = cut.Find("#test-page-content");
        Assert.Equal("Witaj na ekranie logowania", bodyContent.TextContent.Trim());
    }

    [Fact]
    public void LoginLayout_ExecutesJavaScriptStylesOnFirstRender()
    {
        // Arrange
        var jsMock = JSInterop.SetupVoid("eval", _ => true);

        // Act
        var cut = Render<LoginLayout>(parameters => parameters
            .Add(l => l.Body, builder => builder.AddMarkupContent(0, "<p>Główna zawartość</p>"))
        );

        // Assert
        jsMock.VerifyInvoke("eval", calledTimes: 1);

        var invokedCall = JSInterop.Invocations["eval"].FirstOrDefault();
        Assert.NotNull(invokedCall);
        
        string jsArgument = invokedCall.Arguments[0]?.ToString() ?? "";
        Assert.Contains("login-page-body", jsArgument);
        Assert.Contains("fontFamily", jsArgument);
    }

    // KLUCZOWA POPRAWKA: Dodanie parametru ChildContent do atrapy
    private class EmptyComponent : ComponentBase 
    { 
        [Parameter] public RenderFragment? ChildContent { get; set; }
    }
}