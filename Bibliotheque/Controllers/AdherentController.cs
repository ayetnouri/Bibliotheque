using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "ADHERENT")]
    public class AdherentController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public AdherentController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =========================================================
        // ACCUEIL ADHERENT
        // =========================================================
        public IActionResult Index()
        {
            return View();
        }

        // =========================================================
        // RECUPERER L'ID DE L'ADHERENT CONNECTE
        // =========================================================
        private bool TryGetIdUtilisateur(out int idUtilisateur)
        {
            idUtilisateur = 0;

            string? idString =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(idString, out idUtilisateur);
        }

        // =========================================================
        // MES EMPRUNTS
        // =========================================================
        public IActionResult MesEmprunts()
        {
            if (!TryGetIdUtilisateur(out int idUtilisateur))
                return Unauthorized();

            var emprunts = new List<Emprunt>();

            using var connection = _factory.GetConnection();
            connection.Open();

            MettreAJourEmpruntsEnRetard(connection);

            string sql = @"
                SELECT
                    E.ID_EMPRUNT,
                    E.ID_UTILISATEUR,
                    E.ID_LIVRE,
                    E.DATE_EMPRUNT,
                    E.DATE_RETOUR_PREVUE,
                    E.DATE_RETOUR_REELLE,
                    E.STATUT,
                    E.PENALITE,
                    L.TITRE
                FROM EMPRUNT E
                INNER JOIN LIVRE L
                    ON E.ID_LIVRE = L.ID_LIVRE
                WHERE E.ID_UTILISATEUR = :idUtilisateur
                ORDER BY E.DATE_EMPRUNT DESC";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;

            commande.Parameters.Add(
                ":idUtilisateur", idUtilisateur);

            using var reader = commande.ExecuteReader();

            while (reader.Read())
            {
                emprunts.Add(new Emprunt
                {
                    IdEmprunt =
                        Convert.ToInt32(reader["ID_EMPRUNT"]),

                    IdUtilisateur =
                        Convert.ToInt32(reader["ID_UTILISATEUR"]),

                    IdLivre =
                        Convert.ToInt32(reader["ID_LIVRE"]),

                    DateEmprunt =
                        Convert.ToDateTime(reader["DATE_EMPRUNT"]),

                    DateRetourPrevue =
                        Convert.ToDateTime(reader["DATE_RETOUR_PREVUE"]),

                    DateRetourReelle =
                        reader["DATE_RETOUR_REELLE"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(
                                reader["DATE_RETOUR_REELLE"]),

                    Statut =
                        reader["STATUT"]?.ToString() ?? string.Empty,

                    Penalite =
                        reader["PENALITE"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["PENALITE"]),

                    TitreLivre =
                        reader["TITRE"]?.ToString() ?? string.Empty
                });
            }

            return View(emprunts);
        }

        // =========================================================
        // EMPRUNTER UN LIVRE
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Emprunter(int idLivre)
        {
            if (!TryGetIdUtilisateur(out int idUtilisateur))
                return Unauthorized();

            if (idLivre <= 0)
                return BadRequest();

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Verrouiller la ligne du livre pour proteger le stock
                string sqlLivre = @"
                    SELECT EXEMPLAIRES_DISPONIBLES
                    FROM LIVRE
                    WHERE ID_LIVRE = :idLivre
                    FOR UPDATE";

                using var commandeLivre =
                    new OracleCommand(sqlLivre, connection);

                commandeLivre.Transaction = transaction;
                commandeLivre.BindByName = true;
                commandeLivre.Parameters.Add(":idLivre", idLivre);

                object? resultatStock = commandeLivre.ExecuteScalar();

                if (resultatStock == null ||
                    resultatStock == DBNull.Value)
                {
                    transaction.Rollback();

                    TempData["Erreur"] = "Livre introuvable.";

                    return RedirectToAction(
                        "Details", "Livres", new { id = idLivre });
                }

                int exemplairesDisponibles =
                    Convert.ToInt32(resultatStock);

                if (exemplairesDisponibles <= 0)
                {
                    transaction.Rollback();

                    TempData["Erreur"] =
                        "Ce livre n'est actuellement plus disponible.";

                    return RedirectToAction(
                        "Details", "Livres", new { id = idLivre });
                }

                // 2. Verifier si l'adherent possede deja ce livre
                string sqlEmpruntExiste = @"
                    SELECT COUNT(*)
                    FROM EMPRUNT
                    WHERE ID_UTILISATEUR = :idUtilisateur
                      AND ID_LIVRE = :idLivre
                      AND STATUT IN ('EN_COURS', 'EN_RETARD')";

                using var commandeExiste =
                    new OracleCommand(sqlEmpruntExiste, connection);

                commandeExiste.Transaction = transaction;
                commandeExiste.BindByName = true;

                commandeExiste.Parameters.Add(
                    ":idUtilisateur", idUtilisateur);

                commandeExiste.Parameters.Add(
                    ":idLivre", idLivre);

                int empruntExiste = Convert.ToInt32(
                    commandeExiste.ExecuteScalar());

                if (empruntExiste > 0)
                {
                    transaction.Rollback();

                    TempData["Erreur"] =
                        "Vous avez déjà un emprunt en cours pour ce livre.";

                    return RedirectToAction(
                        "Details", "Livres", new { id = idLivre });
                }

                // 3. Creer l'emprunt
                string sqlEmprunt = @"
                    INSERT INTO EMPRUNT
                    (
                        ID_EMPRUNT,
                        ID_UTILISATEUR,
                        ID_LIVRE,
                        DATE_EMPRUNT,
                        DATE_RETOUR_PREVUE,
                        DATE_RETOUR_REELLE,
                        STATUT,
                        PENALITE
                    )
                    VALUES
                    (
                        SEQ_EMPRUNT.NEXTVAL,
                        :idUtilisateur,
                        :idLivre,
                        SYSDATE,
                        SYSDATE + 14,
                        NULL,
                        'EN_COURS',
                        0
                    )";

                using var commandeEmprunt =
                    new OracleCommand(sqlEmprunt, connection);

                commandeEmprunt.Transaction = transaction;
                commandeEmprunt.BindByName = true;

                commandeEmprunt.Parameters.Add(
                    ":idUtilisateur", idUtilisateur);

                commandeEmprunt.Parameters.Add(
                    ":idLivre", idLivre);

                commandeEmprunt.ExecuteNonQuery();

                // 4. Diminuer le stock uniquement s'il reste disponible
                string sqlStock = @"
                    UPDATE LIVRE
                    SET EXEMPLAIRES_DISPONIBLES =
                        EXEMPLAIRES_DISPONIBLES - 1
                    WHERE ID_LIVRE = :idLivre
                      AND EXEMPLAIRES_DISPONIBLES > 0";

                using var commandeStock =
                    new OracleCommand(sqlStock, connection);

                commandeStock.Transaction = transaction;
                commandeStock.BindByName = true;
                commandeStock.Parameters.Add(":idLivre", idLivre);

                int lignesModifiees =
                    commandeStock.ExecuteNonQuery();

                if (lignesModifiees != 1)
                {
                    transaction.Rollback();

                    TempData["Erreur"] =
                        "Le stock a changé. Veuillez réessayer.";

                    return RedirectToAction(
                        "Details", "Livres", new { id = idLivre });
                }

                transaction.Commit();

                TempData["Succes"] =
                    "Le livre a été emprunté avec succès.";

                return RedirectToAction(
                    "Details", "Livres", new { id = idLivre });
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // La transaction peut deja etre terminee.
                }

                TempData["Erreur"] =
                    "Une erreur est survenue pendant l'emprunt. Veuillez réessayer.";

                return RedirectToAction(
                    "Details", "Livres", new { id = idLivre });
            }
        }

        // =========================================================
        // MON PROFIL
        // =========================================================
        public IActionResult MonProfil()
        {
            if (!TryGetIdUtilisateur(out int idUtilisateur))
                return Unauthorized();

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
                WHERE U.ID_UTILISATEUR = :idUtilisateur
                  AND U.ROLE = 'ADHERENT'";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(
                ":idUtilisateur", idUtilisateur);

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
                return NotFound();

            var adherent = new Adherent
            {
                IdUtilisateur =
                    Convert.ToInt32(reader["ID_UTILISATEUR"]),

                Nom = reader["NOM"]?.ToString() ?? string.Empty,
                Prenom = reader["PRENOM"]?.ToString() ?? string.Empty,
                Email = reader["EMAIL"]?.ToString() ?? string.Empty,
                Etat = reader["ETAT"]?.ToString() ?? string.Empty,

                DateCreation =
                    reader["DATE_CREATION"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["DATE_CREATION"]),

                Telephone =
                    reader["TELEPHONE"] == DBNull.Value
                        ? null
                        : reader["TELEPHONE"]?.ToString(),

                Adresse =
                    reader["ADRESSE"] == DBNull.Value
                        ? null
                        : reader["ADRESSE"]?.ToString(),

                DateInscription =
                    reader["DATE_INSCRIPTION"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["DATE_INSCRIPTION"]),

                Statut =
                    reader["STATUT"] == DBNull.Value
                        ? null
                        : reader["STATUT"]?.ToString()
            };

            return View(adherent);
        }

        // =========================================================
        // MODIFIER MON PROFIL - AFFICHER LE FORMULAIRE
        // =========================================================
        [HttpGet]
        public IActionResult ModifierProfil()
        {
            if (!TryGetIdUtilisateur(out int idUtilisateur))
                return Unauthorized();

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT
                    U.ID_UTILISATEUR,
                    U.NOM,
                    U.PRENOM,
                    U.EMAIL,
                    A.TELEPHONE,
                    A.ADRESSE
                FROM UTILISATEUR U
                INNER JOIN ADHERENT A
                    ON U.ID_UTILISATEUR = A.ID_UTILISATEUR
                WHERE U.ID_UTILISATEUR = :idUtilisateur
                  AND U.ROLE = 'ADHERENT'";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(
                ":idUtilisateur", idUtilisateur);

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
                return NotFound();

            var adherent = new Adherent
            {
                IdUtilisateur =
                    Convert.ToInt32(reader["ID_UTILISATEUR"]),

                Nom = reader["NOM"]?.ToString() ?? string.Empty,
                Prenom = reader["PRENOM"]?.ToString() ?? string.Empty,
                Email = reader["EMAIL"]?.ToString() ?? string.Empty,

                Telephone =
                    reader["TELEPHONE"] == DBNull.Value
                        ? null
                        : reader["TELEPHONE"]?.ToString(),

                Adresse =
                    reader["ADRESSE"] == DBNull.Value
                        ? null
                        : reader["ADRESSE"]?.ToString()
            };

            return View(adherent);
        }

        // =========================================================
        // MODIFIER MON PROFIL - ENREGISTRER
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ModifierProfil(Adherent adherent)
        {
            if (!TryGetIdUtilisateur(out int idUtilisateur))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(adherent.Nom) ||
                string.IsNullOrWhiteSpace(adherent.Prenom) ||
                string.IsNullOrWhiteSpace(adherent.Email))
            {
                ViewBag.Erreur =
                    "Le nom, le prénom et l'adresse e-mail sont obligatoires.";

                return View(adherent);
            }

            string nom = adherent.Nom.Trim();
            string prenom = adherent.Prenom.Trim();
            string email = adherent.Email.Trim();

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Verifier si l'email est deja utilise
                string sqlEmail = @"
                    SELECT COUNT(*)
                    FROM UTILISATEUR
                    WHERE UPPER(EMAIL) = UPPER(:email)
                      AND ID_UTILISATEUR <> :idUtilisateur";

                using var commandeEmail =
                    new OracleCommand(sqlEmail, connection);

                commandeEmail.Transaction = transaction;
                commandeEmail.BindByName = true;

                commandeEmail.Parameters.Add(":email", email);
                commandeEmail.Parameters.Add(
                    ":idUtilisateur", idUtilisateur);

                int emailExiste = Convert.ToInt32(
                    commandeEmail.ExecuteScalar());

                if (emailExiste > 0)
                {
                    transaction.Rollback();

                    ViewBag.Erreur =
                        "Cette adresse e-mail est déjà utilisée.";

                    return View(adherent);
                }

                // 2. Modifier UTILISATEUR
                string sqlUtilisateur = @"
                    UPDATE UTILISATEUR
                    SET NOM = :nom,
                        PRENOM = :prenom,
                        EMAIL = :email
                    WHERE ID_UTILISATEUR = :idUtilisateur
                      AND ROLE = 'ADHERENT'";

                using var commandeUtilisateur =
                    new OracleCommand(sqlUtilisateur, connection);

                commandeUtilisateur.Transaction = transaction;
                commandeUtilisateur.BindByName = true;

                commandeUtilisateur.Parameters.Add(":nom", nom);
                commandeUtilisateur.Parameters.Add(":prenom", prenom);
                commandeUtilisateur.Parameters.Add(":email", email);
                commandeUtilisateur.Parameters.Add(
                    ":idUtilisateur", idUtilisateur);

                int utilisateursModifies =
                    commandeUtilisateur.ExecuteNonQuery();

                if (utilisateursModifies != 1)
                {
                    transaction.Rollback();
                    return NotFound();
                }

                // 3. Modifier ADHERENT
                string sqlAdherent = @"
                    UPDATE ADHERENT
                    SET TELEPHONE = :telephone,
                        ADRESSE = :adresse
                    WHERE ID_UTILISATEUR = :idUtilisateur";

                using var commandeAdherent =
                    new OracleCommand(sqlAdherent, connection);

                commandeAdherent.Transaction = transaction;
                commandeAdherent.BindByName = true;

                commandeAdherent.Parameters
                    .Add(":telephone", OracleDbType.Varchar2)
                    .Value = string.IsNullOrWhiteSpace(adherent.Telephone)
                        ? DBNull.Value
                        : adherent.Telephone.Trim();

                commandeAdherent.Parameters
                    .Add(":adresse", OracleDbType.Varchar2)
                    .Value = string.IsNullOrWhiteSpace(adherent.Adresse)
                        ? DBNull.Value
                        : adherent.Adresse.Trim();

                commandeAdherent.Parameters.Add(
                    ":idUtilisateur", idUtilisateur);

                commandeAdherent.ExecuteNonQuery();

                transaction.Commit();

                TempData["Succes"] =
                    "Votre profil a été modifié avec succès.";

                return RedirectToAction(nameof(MonProfil));
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // La transaction peut deja etre terminee.
                }

                ViewBag.Erreur =
                    "Une erreur est survenue lors de la modification du profil.";

                return View(adherent);
            }
        }

        // =========================================================
        // METTRE A JOUR LES EMPRUNTS EN RETARD
        // =========================================================
        private void MettreAJourEmpruntsEnRetard(
            OracleConnection connection)
        {
            string sql = @"
                UPDATE EMPRUNT
                SET STATUT = 'EN_RETARD'
                WHERE STATUT = 'EN_COURS'
                  AND TRUNC(DATE_RETOUR_PREVUE) < TRUNC(SYSDATE)";

            using var commande = new OracleCommand(sql, connection);
            commande.ExecuteNonQuery();
        }
    }
}