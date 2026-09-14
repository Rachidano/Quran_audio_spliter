using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quran.Infrastructure.Identity;

namespace Quran.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "تذكرني")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl ?? Url.Content("~/Admin");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/Admin");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email.Trim()) 
                   ?? await _userManager.FindByNameAsync(Input.Email.Trim());

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("Utilisateur {Email} connecté avec succès.", Input.Email);
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Compte {Email} verrouillé.", Input.Email);
            ModelState.AddModelError(string.Empty, "الحساب مقفل مؤقتا بسبب تكرار المحاولات الفاشلة.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
        return Page();
    }
}
