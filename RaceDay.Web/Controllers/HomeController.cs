using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    public IActionResult Index()
    {
        // Logged-in users go straight to their own role's home page.
        return CurrentRole is null ? View() : HomeFor(CurrentRole);
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error() =>
        View(new ErrorViewModel("Something went wrong", "An unexpected problem occurred. Please try again."));
}
