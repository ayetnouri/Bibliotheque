using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "BIBLIOTHECAIRE")]
    public class AdherentsController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public AdherentsController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =========================
        // LISTE DES ADHÉRENTS
        // =========================
        [HttpGet]
        public IActionResult Index()
        {
            var adherents = new List<Adherent>();

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT
                    U.ID_UTILISATEUR,
                    U.NOM,
                    U.PRENOM,
                    U.EMAIL,
                    U.ETAT,
                    U.DATE_CREATION,
                    A.TELEPHONE,
                    A.ADRESSE,
                    A.DATE_INSCRIPTION,
                    A.STATUT
                FROM UTILISATEUR U
                INNER JOIN ADHERENT A
                    ON U.ID_UTILISATEUR = A.ID_UTILISATEUR
                WHERE U.ROLE = 'ADHERENT'
                ORDER BY U.NOM, U.PRENOM";

            using var commande = new OracleCommand(sql, connection);
            using var reader = commande.ExecuteReader();

            while (reader.Read())
            {
                var adherent = new Adherent
                {
                    IdUtilisateur = Convert.ToInt32(
                        reader["ID_UTILISATEUR"]),

                    Nom = LireTexte(reader, "NOM"),
                    Prenom = LireTexte(reader, "PRENOM"),
                    Email = LireTexte(reader, "EMAIL"),
                    Etat = LireTexte(reader, "ETAT"),

                    DateCreation = LireDate(
                        reader, "DATE_CREATION"),

                    Telephone = LireTexteNullable(
                        reader, "TELEPHONE"),

                    Adresse = LireTexteNullable(
                        reader, "ADRESSE"),

                    DateInscription = LireDate(
                        reader, "DATE_INSCRIPTION"),

                    Statut = LireTexteNullable(
                        reader, "STATUT")
                };

                adherents.Add(adherent);
            }

            return View(adherents);
        }

        // =========================
        // LECTURE SÉCURISÉE DES VALEURS
        // =========================
        private static string LireTexte(
            OracleDataReader reader,
            string colonne)
        {
            return reader[colonne] == DBNull.Value
                ? string.Empty
                : Convert.ToString(reader[colonne]) ?? string.Empty;
        }

        private static string? LireTexteNullable(
            OracleDataReader reader,
            string colonne)
        {
            return reader[colonne] == DBNull.Value
                ? null
                : Convert.ToString(reader[colonne]);
        }

        private static DateTime? LireDate(
            OracleDataReader reader,
            string colonne)
        {
            return reader[colonne] == DBNull.Value
                ? null
                : Convert.ToDateTime(reader[colonne]);
        }
    }
}