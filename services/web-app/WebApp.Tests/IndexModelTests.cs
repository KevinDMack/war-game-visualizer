using WebApp.Pages;

namespace WebApp.Tests;

/// <summary>
/// Unit tests for the Index Razor page model.
/// </summary>
public class IndexModelTests
{
    [Fact]
    public void OnGet_WithCesiumIonTokenEnvVar_SetsCesiumIonToken()
    {
        Environment.SetEnvironmentVariable("CESIUM_ION_TOKEN", "test-token-123");
        try
        {
            var model = new IndexModel();
            model.OnGet();

            Assert.Equal("test-token-123", model.CesiumIonToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CESIUM_ION_TOKEN", null);
        }
    }

    [Fact]
    public void OnGet_WithoutCesiumIonTokenEnvVar_SetsEmptyString()
    {
        Environment.SetEnvironmentVariable("CESIUM_ION_TOKEN", null);

        var model = new IndexModel();
        model.OnGet();

        Assert.Equal(string.Empty, model.CesiumIonToken);
    }

    [Fact]
    public void OnGet_WithEmptyCesiumIonTokenEnvVar_SetsEmptyString()
    {
        Environment.SetEnvironmentVariable("CESIUM_ION_TOKEN", "");
        try
        {
            var model = new IndexModel();
            model.OnGet();

            Assert.Equal(string.Empty, model.CesiumIonToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CESIUM_ION_TOKEN", null);
        }
    }
}
