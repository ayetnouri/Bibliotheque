using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bibliotheque.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            if (User.IsInRole("ADMIN"))
            {
                return RedirectToAction("Index", "Admin");
            }

            if (User.IsInRole("BIBLIOTHECAIRE"))
            {
                return RedirectToAction("Index", "Bibliothecaire");
            }

            if (User.IsInRole("ADHERENT"))
            {
                return RedirectToAction("Index", "Adherent");
            }

            return Forbid();
        }
    }
}