using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApp.Pages;

public class IndexModel : PageModel
{
    public string CesiumIonToken { get; private set; } = string.Empty;

    public void OnGet()
    {
        CesiumIonToken = Environment.GetEnvironmentVariable("CESIUM_ION_TOKEN") ?? string.Empty;
    }
}
