using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class StatistiquesController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public StatistiquesController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =========================================================
        // PAGE DES STATISTIQUES
        // =========================================================
        public IActionResult Index()
        {
            var statistiques = new Statistiques();

            var connection = _factory.GetConnection();
            connection.Open();

            try
            {
                // =================================================
                // UTILISATEURS
                // =================================================

                string sqlUtilisateurs = @"
                    SELECT
                        COUNT(*) AS TOTAL,
                        SUM(CASE WHEN ROLE = 'ADHERENT' THEN 1 ELSE 0 END) AS ADHERENTS,
                        SUM(CASE WHEN ROLE = 'BIBLIOTHECAIRE' THEN 1 ELSE 0 END) AS BIBLIOTHECAIRES,
                        SUM(CASE WHEN ROLE = 'ADMIN' THEN 1 ELSE 0 END) AS ADMINISTRATEURS
                    FROM UTILISATEUR
                ";

                var commandeUtilisateurs =
                    new OracleCommand(sqlUtilisateurs, connection);

                var readerUtilisateurs =
                    commandeUtilisateurs.ExecuteReader();

                if (readerUtilisateurs.Read())
                {
                    statistiques.TotalUtilisateurs =
                        Convert.ToInt32(readerUtilisateurs["TOTAL"]);

                    statistiques.TotalAdherents =
                        Convert.ToInt32(readerUtilisateurs["ADHERENTS"]);

                    statistiques.TotalBibliothecaires =
                        Convert.ToInt32(readerUtilisateurs["BIBLIOTHECAIRES"]);

                    statistiques.TotalAdministrateurs =
                        Convert.ToInt32(readerUtilisateurs["ADMINISTRATEURS"]);
                }

                readerUtilisateurs.Close();


                // =================================================
                // LIVRES
                // =================================================

                string sqlLivres = @"
                    SELECT
                        COUNT(*) AS TOTAL_LIVRES,
                        NVL(SUM(NOMBRE_EXEMPLAIRES), 0) AS TOTAL_EXEMPLAIRES,
                        NVL(SUM(EXEMPLAIRES_DISPONIBLES), 0) AS DISPONIBLES
                    FROM LIVRE
                ";

                var commandeLivres =
                    new OracleCommand(sqlLivres, connection);

                var readerLivres =
                    commandeLivres.ExecuteReader();

                if (readerLivres.Read())
                {
                    statistiques.TotalLivres =
                        Convert.ToInt32(readerLivres["TOTAL_LIVRES"]);

                    statistiques.TotalExemplaires =
                        Convert.ToInt32(readerLivres["TOTAL_EXEMPLAIRES"]);

                    statistiques.ExemplairesDisponibles =
                        Convert.ToInt32(readerLivres["DISPONIBLES"]);
                }

                readerLivres.Close();


                // =================================================
                // EMPRUNTS
                // =================================================

                string sqlEmprunts = @"
                    SELECT
                        SUM(CASE WHEN STATUT = 'EN_COURS' THEN 1 ELSE 0 END) AS EN_COURS,
                        SUM(CASE WHEN STATUT = 'RETOURNE' THEN 1 ELSE 0 END) AS RETOURNES,
                        SUM(CASE WHEN STATUT = 'EN_RETARD' THEN 1 ELSE 0 END) AS EN_RETARD
                    FROM EMPRUNT
                ";

                var commandeEmprunts =
                    new OracleCommand(sqlEmprunts, connection);

                var readerEmprunts =
                    commandeEmprunts.ExecuteReader();

                if (readerEmprunts.Read())
                {
                    statistiques.EmpruntsEnCours =
                        Convert.ToInt32(readerEmprunts["EN_COURS"]);

                    statistiques.EmpruntsRetournes =
                        Convert.ToInt32(readerEmprunts["RETOURNES"]);

                    statistiques.EmpruntsEnRetard =
                        Convert.ToInt32(readerEmprunts["EN_RETARD"]);
                }

                readerEmprunts.Close();


                connection.Close();

                return View(statistiques);
            }
            catch (Exception ex)
            {
                connection.Close();

                return Content(
                    "Une erreur est survenue : " + ex.Message
                );
            }
        }
    }
}