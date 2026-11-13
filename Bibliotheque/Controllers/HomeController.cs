using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Diagnostics;

namespace Bibliotheque.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        private readonly OracleConnectionFactory _factory;
        public HomeController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }
        public IActionResult TestConnexion()
        { var connection =_factory.GetConnection();
            connection.Open();
            var commande = new OracleCommand(
         "SELECT COUNT(*) FROM LIVRE",connection);

            var resultat = commande.ExecuteScalar();

            return Content("Connexion Oracle réussie ! Nombre de livres : " + resultat);
        }
    }

    }
