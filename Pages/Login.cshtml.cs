using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CardCollector.Pages;

public sealed class LoginModel : PageModel
{
    private readonly IConfiguration _config;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public LoginModel(IConfiguration config) => _config = config;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var expectedUsername = _config["Auth:Username"];
        var hash = _config["Auth:PasswordHash"];

        if (string.IsNullOrEmpty(hash)
            || !string.Equals(Username, expectedUsername, StringComparison.OrdinalIgnoreCase)
            || !BCrypt.Net.BCrypt.Verify(Password, hash))
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, "CardCollectorCookie");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("CardCollectorCookie", principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
