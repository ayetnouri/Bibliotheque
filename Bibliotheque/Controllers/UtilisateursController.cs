using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UtilisateursController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        private static readonly string[] RolesAutorises =
        {
            "ADMIN",
            "BIBLIOTHECAIRE",
            "ADHERENT"
        };

        private static readonly string[] EtatsAutorises =
        {
            "ACTIF",
            "INACTIF"
        };

        public UtilisateursController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // Convertit une valeur Oracle en texte sans avertissement nullable.
        private static string LireTexte(
            OracleDataReader reader,
            string colonne)
        {
            return reader[colonne] == DBNull.Value
                ? string.Empty
                : Convert.ToString(reader[colonne]) ?? string.Empty;
        }

        private static DateTime? LireDate(
            OracleDataReader reader,
            string colonne)
        {
            return reader[colonne] == DBNull.Value
                ? null
                : Convert.ToDateTime(reader[colonne]);
        }

        private static Utilisateur LireUtilisateur(
            OracleDataReader reader)
        {
            return new Utilisateur
            {
                IdUtilisateur = Convert.ToInt32(
                    reader["ID_UTILISATEUR"]),

                Nom = LireTexte(reader, "NOM"),
                Prenom = LireTexte(reader, "PRENOM"),
                Email = LireTexte(reader, "EMAIL"),
                Role = LireTexte(reader, "ROLE"),
                Etat = LireTexte(reader, "ETAT"),
                DateCreation = LireDate(reader, "DATE_CREATION")
            };
        }

        private static bool RoleValide(string? role)
        {
            return !string.IsNullOrWhiteSpace(role)
                && RolesAutorises.Contains(
                    role.Trim(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static bool EtatValide(string? etat)
        {
            return !string.IsNullOrWhiteSpace(etat)
                && EtatsAutorises.Contains(
                    etat.Trim(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private int? IdUtilisateurConnecte()
        {
            string? id = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(id, out int resultat)
                ? resultat
                : null;
        }

        // =========================================================
        // LISTE ET RECHERCHE
        // =========================================================
        [HttpGet]
        public IActionResult Index(string? recherche)
        {
            var utilisateurs = new List<Utilisateur>();

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT
                    ID_UTILISATEUR,
                    NOM,
                    PRENOM,
                    EMAIL,
                    ROLE,
                    ETAT,
                    DATE_CREATION
                FROM UTILISATEUR
                WHERE
                    (:recherche IS NULL
                    OR UPPER(NOM) LIKE '%' || UPPER(:recherche) || '%'
                    OR UPPER(PRENOM) LIKE '%' || UPPER(:recherche) || '%'
                    OR UPPER(EMAIL) LIKE '%' || UPPER(:recherche) || '%'
                    OR UPPER(ROLE) LIKE '%' || UPPER(:recherche) || '%')
                ORDER BY NOM, PRENOM";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;

            commande.Parameters.Add(
                ":recherche",
                OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(recherche)
                    ? DBNull.Value
                    : recherche.Trim();

            using var reader = commande.ExecuteReader();

            while (reader.Read())
            {
                utilisateurs.Add(LireUtilisateur(reader));
            }

            ViewBag.Recherche = recherche;

            return View(utilisateurs);
        }

        // =========================================================
        // AJOUT : AFFICHER LE FORMULAIRE
        // =========================================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // =========================================================
        // AJOUT : ENREGISTRER
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(
            Utilisateur utilisateur,
            string? motDePasse)
        {
            utilisateur.Nom = utilisateur.Nom?.Trim() ?? string.Empty;
            utilisateur.Prenom = utilisateur.Prenom?.Trim() ?? string.Empty;
            utilisateur.Email = utilisateur.Email?.Trim() ?? string.Empty;
            utilisateur.Role = utilisateur.Role?.Trim().ToUpperInvariant()
                ?? string.Empty;
            utilisateur.Etat = string.IsNullOrWhiteSpace(utilisateur.Etat)
                ? "ACTIF"
                : utilisateur.Etat.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(utilisateur.Nom)
                || string.IsNullOrWhiteSpace(utilisateur.Prenom)
                || string.IsNullOrWhiteSpace(utilisateur.Email)
                || string.IsNullOrWhiteSpace(motDePasse)
                || !RoleValide(utilisateur.Role)
                || !EtatValide(utilisateur.Etat))
            {
                ViewBag.Erreur =
                    "Veuillez remplir les champs obligatoires et choisir un rôle et un état valides.";

                return View(utilisateur);
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Vérifier l'unicité de l'adresse e-mail.
                string sqlEmail = @"
                    SELECT COUNT(*)
                    FROM UTILISATEUR
                    WHERE UPPER(EMAIL) = UPPER(:email)";

                using var commandeEmail =
                    new OracleCommand(sqlEmail, connection);

                commandeEmail.Transaction = transaction;
                commandeEmail.BindByName = true;
                commandeEmail.Parameters.Add(
                    ":email", OracleDbType.Varchar2).Value =
                    utilisateur.Email;

                int emailExiste = Convert.ToInt32(
                    commandeEmail.ExecuteScalar());

                if (emailExiste > 0)
                {
                    transaction.Rollback();

                    ViewBag.Erreur =
                        "Cet email est déjà utilisé par un autre utilisateur.";

                    return View(utilisateur);
                }

                // Récupérer l'identifiant suivant.
                using var commandeId = new OracleCommand(
                    "SELECT SEQ_UTILISATEUR.NEXTVAL FROM DUAL",
                    connection);

                commandeId.Transaction = transaction;

                int idUtilisateur = Convert.ToInt32(
                    commandeId.ExecuteScalar());

                // Hacher le mot de passe avant son enregistrement.
                var utilisateurPourHash = new Utilisateur
                {
                    IdUtilisateur = idUtilisateur,
                    Nom = utilisateur.Nom,
                    Prenom = utilisateur.Prenom,
                    Email = utilisateur.Email,
                    Role = utilisateur.Role,
                    Etat = utilisateur.Etat
                };

                var hasher = new PasswordHasher<Utilisateur>();

                string motDePasseHash = hasher.HashPassword(
                    utilisateurPourHash,
                    motDePasse);

                string sqlUtilisateur = @"
                    INSERT INTO UTILISATEUR
                    (
                        ID_UTILISATEUR,
                        NOM,
                        PRENOM,
                        EMAIL,
                        MOT_DE_PASSE,
                        ROLE,
                        ETAT,
                        DATE_CREATION
                    )
                    VALUES
                    (
                        :id,
                        :nom,
                        :prenom,
                        :email,
                        :motDePasse,
                        :role,
                        :etat,
                        SYSDATE
                    )";

                using var commandeUtilisateur =
                    new OracleCommand(sqlUtilisateur, connection);

                commandeUtilisateur.Transaction = transaction;
                commandeUtilisateur.BindByName = true;

                commandeUtilisateur.Parameters.Add(
                    ":id", OracleDbType.Int32).Value = idUtilisateur;
                commandeUtilisateur.Parameters.Add(
                    ":nom", OracleDbType.Varchar2).Value = utilisateur.Nom;
                commandeUtilisateur.Parameters.Add(
                    ":prenom", OracleDbType.Varchar2).Value = utilisateur.Prenom;
                commandeUtilisateur.Parameters.Add(
                    ":email", OracleDbType.Varchar2).Value = utilisateur.Email;
                commandeUtilisateur.Parameters.Add(
                    ":motDePasse", OracleDbType.Varchar2).Value = motDePasseHash;
                commandeUtilisateur.Parameters.Add(
                    ":role", OracleDbType.Varchar2).Value = utilisateur.Role;
                commandeUtilisateur.Parameters.Add(
                    ":etat", OracleDbType.Varchar2).Value = utilisateur.Etat;

                commandeUtilisateur.ExecuteNonQuery();

                // Créer également la fiche ADHERENT si nécessaire.
                if (utilisateur.Role == "ADHERENT")
                {
                    string sqlAdherent = @"
                        INSERT INTO ADHERENT
                        (
                            ID_UTILISATEUR,
                            DATE_INSCRIPTION,
                            STATUT
                        )
                        VALUES
                        (
                            :idUtilisateur,
                            SYSDATE,
                            'ACTIF'
                        )";

                    using var commandeAdherent =
                        new OracleCommand(sqlAdherent, connection);

                    commandeAdherent.Transaction = transaction;
                    commandeAdherent.BindByName = true;
                    commandeAdherent.Parameters.Add(
                        ":idUtilisateur", OracleDbType.Int32).Value =
                        idUtilisateur;

                    commandeAdherent.ExecuteNonQuery();
                }

                transaction.Commit();

                TempData["Succes"] =
                    "L'utilisateur a été ajouté avec succès.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // La transaction peut déjà être terminée.
                }

                ViewBag.Erreur =
                    "Impossible d'ajouter l'utilisateur. Vérifiez les données et la connexion à la base de données.";

                return View(utilisateur);
            }
        }

        // =========================================================
        // DETAILS
        // =========================================================
        [HttpGet]
        public IActionResult Details(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT
                    ID_UTILISATEUR,
                    NOM,
                    PRENOM,
                    EMAIL,
                    ROLE,
                    ETAT,
                    DATE_CREATION
                FROM UTILISATEUR
                WHERE ID_UTILISATEUR = :id";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(
                ":id", OracleDbType.Int32).Value = id;

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
            {
                return NotFound();
            }

            return View(LireUtilisateur(reader));
        }

        // =========================================================
        // MODIFICATION : AFFICHER LE FORMULAIRE
        // =========================================================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT
                    ID_UTILISATEUR,
                    NOM,
                    PRENOM,
                    EMAIL,
                    ROLE,
                    ETAT,
                    DATE_CREATION
                FROM UTILISATEUR
                WHERE ID_UTILISATEUR = :id";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(
                ":id", OracleDbType.Int32).Value = id;

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
            {
                return NotFound();
            }

            return View(LireUtilisateur(reader));
        }

        // =========================================================
        // MODIFICATION : ENREGISTRER
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Utilisateur utilisateur)
        {
            utilisateur.Nom = utilisateur.Nom?.Trim() ?? string.Empty;
            utilisateur.Prenom = utilisateur.Prenom?.Trim() ?? string.Empty;
            utilisateur.Email = utilisateur.Email?.Trim() ?? string.Empty;
            utilisateur.Role = utilisateur.Role?.Trim().ToUpperInvariant()
                ?? string.Empty;
            utilisateur.Etat = utilisateur.Etat?.Trim().ToUpperInvariant()
                ?? string.Empty;

            if (utilisateur.IdUtilisateur <= 0
                || string.IsNullOrWhiteSpace(utilisateur.Nom)
                || string.IsNullOrWhiteSpace(utilisateur.Prenom)
                || string.IsNullOrWhiteSpace(utilisateur.Email)
                || !RoleValide(utilisateur.Role)
                || !EtatValide(utilisateur.Etat))
            {
                ViewBag.Erreur =
                    "Veuillez remplir les champs obligatoires et choisir un rôle et un état valides.";

                return View(utilisateur);
            }

            // Empêcher l'administrateur de modifier son propre rôle
            // ou de désactiver son propre compte par cette action.
            int? idConnecte = IdUtilisateurConnecte();

            if (idConnecte.HasValue
                && idConnecte.Value == utilisateur.IdUtilisateur)
            {
                using var verificationConnection = _factory.GetConnection();
                verificationConnection.Open();

                const string sqlRoleActuel = @"
                    SELECT ROLE
                    FROM UTILISATEUR
                    WHERE ID_UTILISATEUR = :id";

                using var verificationCommande =
                    new OracleCommand(sqlRoleActuel, verificationConnection);

                verificationCommande.BindByName = true;
                verificationCommande.Parameters.Add(
                    ":id", OracleDbType.Int32).Value =
                    utilisateur.IdUtilisateur;

                object? roleActuel = verificationCommande.ExecuteScalar();

                if (roleActuel == null
                    || roleActuel == DBNull.Value
                    || !string.Equals(
                        Convert.ToString(roleActuel),
                        "ADMIN",
                        StringComparison.OrdinalIgnoreCase)
                    || utilisateur.Role != "ADMIN"
                    || utilisateur.Etat != "ACTIF")
                {
                    ViewBag.Erreur =
                        "Vous ne pouvez pas modifier votre propre rôle ni désactiver votre compte.";

                    return View(utilisateur);
                }
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Vérifier que l'utilisateur existe.
                const string sqlExiste = @"
                    SELECT COUNT(*)
                    FROM UTILISATEUR
                    WHERE ID_UTILISATEUR = :id";

                using var commandeExiste =
                    new OracleCommand(sqlExiste, connection);

                commandeExiste.Transaction = transaction;
                commandeExiste.BindByName = true;
                commandeExiste.Parameters.Add(
                    ":id", OracleDbType.Int32).Value =
                    utilisateur.IdUtilisateur;

                int existe = Convert.ToInt32(
                    commandeExiste.ExecuteScalar());

                if (existe == 0)
                {
                    transaction.Rollback();
                    return NotFound();
                }

                // Vérifier que l'email n'appartient pas à un autre compte.
                const string sqlEmail = @"
                    SELECT COUNT(*)
                    FROM UTILISATEUR
                    WHERE UPPER(EMAIL) = UPPER(:email)
                    AND ID_UTILISATEUR <> :id";

                using var commandeEmail =
                    new OracleCommand(sqlEmail, connection);

                commandeEmail.Transaction = transaction;
                commandeEmail.BindByName = true;
                commandeEmail.Parameters.Add(
                    ":email", OracleDbType.Varchar2).Value =
                    utilisateur.Email;
                commandeEmail.Parameters.Add(
                    ":id", OracleDbType.Int32).Value =
                    utilisateur.IdUtilisateur;

                int emailExiste = Convert.ToInt32(
                    commandeEmail.ExecuteScalar());

                if (emailExiste > 0)
                {
                    transaction.Rollback();

                    ViewBag.Erreur =
                        "Cet email est déjà utilisé par un autre utilisateur.";

                    return View(utilisateur);
                }

                // Modifier les informations de l'utilisateur.
                string sqlUtilisateur = @"
                    UPDATE UTILISATEUR
                    SET
                        NOM = :nom,
                        PRENOM = :prenom,
                        EMAIL = :email,
                        ROLE = :role,
                        ETAT = :etat
                    WHERE ID_UTILISATEUR = :id";

                using var commandeUtilisateur =
                    new OracleCommand(sqlUtilisateur, connection);

                commandeUtilisateur.Transaction = transaction;
                commandeUtilisateur.BindByName = true;

                commandeUtilisateur.Parameters.Add(
                    ":nom", OracleDbType.Varchar2).Value = utilisateur.Nom;
                commandeUtilisateur.Parameters.Add(
                    ":prenom", OracleDbType.Varchar2).Value = utilisateur.Prenom;
                commandeUtilisateur.Parameters.Add(
                    ":email", OracleDbType.Varchar2).Value = utilisateur.Email;
                commandeUtilisateur.Parameters.Add(
                    ":role", OracleDbType.Varchar2).Value = utilisateur.Role;
                commandeUtilisateur.Parameters.Add(
                    ":etat", OracleDbType.Varchar2).Value = utilisateur.Etat;
                commandeUtilisateur.Parameters.Add(
                    ":id", OracleDbType.Int32).Value =
                    utilisateur.IdUtilisateur;

                commandeUtilisateur.ExecuteNonQuery();

                // Vérifier si une fiche ADHERENT existe.
                const string sqlAdherentExiste = @"
                    SELECT COUNT(*)
                    FROM ADHERENT
                    WHERE ID_UTILISATEUR = :id";

                using var commandeAdherentExiste =
                    new OracleCommand(sqlAdherentExiste, connection);

                commandeAdherentExiste.Transaction = transaction;
                commandeAdherentExiste.BindByName = true;
                commandeAdherentExiste.Parameters.Add(
                    ":id", OracleDbType.Int32).Value =
                    utilisateur.IdUtilisateur;

                int adherentExiste = Convert.ToInt32(
                    commandeAdherentExiste.ExecuteScalar());

                if (utilisateur.Role == "ADHERENT")
                {
                    // Créer la fiche si elle n'existe pas.
                    if (adherentExiste == 0)
                    {
                        const string sqlAjouterAdherent = @"
                            INSERT INTO ADHERENT
                            (
                                ID_UTILISATEUR,
                                DATE_INSCRIPTION,
                                STATUT
                            )
                            VALUES
                            (
                                :id,
                                SYSDATE,
                                'ACTIF'
                            )";

                        using var commandeAjouterAdherent =
                            new OracleCommand(
                                sqlAjouterAdherent, connection);

                        commandeAjouterAdherent.Transaction = transaction;
                        commandeAjouterAdherent.BindByName = true;
                        commandeAjouterAdherent.Parameters.Add(
                            ":id", OracleDbType.Int32).Value =
                            utilisateur.IdUtilisateur;

                        commandeAjouterAdherent.ExecuteNonQuery();
                    }
                }
                else if (adherentExiste > 0)
                {
                    // Supprimer la fiche ADHERENT si le rôle change.
                    const string sqlSupprimerAdherent = @"
                        DELETE FROM ADHERENT
                        WHERE ID_UTILISATEUR = :id";

                    using var commandeSupprimerAdherent =
                        new OracleCommand(
                            sqlSupprimerAdherent, connection);

                    commandeSupprimerAdherent.Transaction = transaction;
                    commandeSupprimerAdherent.BindByName = true;
                    commandeSupprimerAdherent.Parameters.Add(
                        ":id", OracleDbType.Int32).Value =
                        utilisateur.IdUtilisateur;

                    commandeSupprimerAdherent.ExecuteNonQuery();
                }

                transaction.Commit();

                TempData["Succes"] =
                    "L'utilisateur a été modifié avec succès.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // La transaction peut déjà être terminée.
                }

                ViewBag.Erreur =
                    "Impossible de modifier l'utilisateur. Vérifiez les données et la connexion à la base de données.";

                return View(utilisateur);
            }
        }

        // =========================================================
        // DESACTIVER UN UTILISATEUR
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Desactiver(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            int? idConnecte = IdUtilisateurConnecte();

            if (idConnecte.HasValue && idConnecte.Value == id)
            {
                TempData["Erreur"] =
                    "Vous ne pouvez pas désactiver votre propre compte.";

                return RedirectToAction(nameof(Index));
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            const string sql = @"
                UPDATE UTILISATEUR
                SET ETAT = 'INACTIF'
                WHERE ID_UTILISATEUR = :id";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(
                ":id", OracleDbType.Int32).Value = id;

            int lignesModifiees = commande.ExecuteNonQuery();

            if (lignesModifiees == 0)
            {
                return NotFound();
            }

            TempData["Succes"] =
                "L'utilisateur a été désactivé avec succès.";

            return RedirectToAction(nameof(Index));
        } 
    // =========================================================
// REACTIVER UN UTILISATEUR
// =========================================================
[HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reactiver(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            int? idConnecte = IdUtilisateurConnecte();

            // Empêcher l'administrateur de réactiver son propre compte
            // par cette action.
            if (idConnecte.HasValue && idConnecte.Value == id)
            {
                TempData["Erreur"] =
                    "Vous ne pouvez pas modifier l'état de votre propre compte.";

                return RedirectToAction(nameof(Index));
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            const string sql = @"
        UPDATE UTILISATEUR
        SET ETAT = 'ACTIF'
        WHERE ID_UTILISATEUR = :id
          AND ETAT = 'INACTIF'";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;

            commande.Parameters.Add(
                ":id", OracleDbType.Int32).Value = id;

            int lignesModifiees = commande.ExecuteNonQuery();

            if (lignesModifiees == 0)
            {
                TempData["Erreur"] =
                    "Utilisateur introuvable ou compte déjà actif.";

                return RedirectToAction(nameof(Index));
            }

            TempData["Succes"] =
                "L'utilisateur a été réactivé avec succès.";

            return RedirectToAction(nameof(Index));
        }
    }
}