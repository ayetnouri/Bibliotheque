using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "BIBLIOTHECAIRE")]
    public class BibliothecaireController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}