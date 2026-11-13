using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;

namespace Bibliotheque.Controllers
{
    public class AccountController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public AccountController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =========================================================
        // LOGIN - AFFICHER LA PAGE
        // =========================================================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // =========================================================
        // LOGIN - TRAITER LA CONNEXION
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Erreur = "Veuillez remplir tous les champs.";
                return View();
            }

            try
            {
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
                    MOT_DE_PASSE
                FROM UTILISATEUR
                WHERE UPPER(EMAIL) = UPPER(:email)";

                using var commande = new OracleCommand(sql, connection);
                commande.BindByName = true;
                commande.Parameters.Add(":email", OracleDbType.Varchar2).Value =
                    email.Trim();

                int idUtilisateur;
                string nom;
                string prenom;
                string emailUtilisateur;
                string role;
                string etat;
                string motDePasseStocke;

                using (var reader = commande.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        ViewBag.Erreur =
                            "Email ou mot de passe incorrect.";
                        return View();
                    }

                    idUtilisateur = Convert.ToInt32(reader["ID_UTILISATEUR"]);
                    nom = reader["NOM"].ToString() ?? "";
                    prenom = reader["PRENOM"].ToString() ?? "";
                    emailUtilisateur = reader["EMAIL"].ToString() ?? "";
                    role = reader["ROLE"].ToString() ?? "";
                    etat = reader["ETAT"].ToString() ?? "";
                    motDePasseStocke =
                        reader["MOT_DE_PASSE"].ToString() ?? "";
                }

                // Vérifier que le compte est actif.
                if (!string.Equals(
                        etat, "ACTIF",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ViewBag.Erreur =
                        "Votre compte est désactivé. Veuillez contacter l'administrateur.";
                    return View();
                }

                // Créer l'utilisateur pour la vérification du mot de passe.
                var utilisateur = new Utilisateur
                {
                    IdUtilisateur = idUtilisateur,
                    Nom = nom,
                    Prenom = prenom,
                    Email = emailUtilisateur,
                    Role = role,
                    Etat = etat
                };

                var hasher = new PasswordHasher<Utilisateur>();
                bool motDePasseCorrect = false;

                // Vérifier les mots de passe déjà hachés.
                // Les anciens comptes de test en clair sont migrés
                // vers un hash lors d'une connexion réussie.
                bool motDePasseDejaHash =
                    motDePasseStocke.StartsWith(
                        "AQAAAA",
                        StringComparison.Ordinal);

                if (motDePasseDejaHash)
                {
                    var resultat = hasher.VerifyHashedPassword(
                        utilisateur,
                        motDePasseStocke,
                        password);

                    motDePasseCorrect =
                        resultat == PasswordVerificationResult.Success ||
                        resultat == PasswordVerificationResult.SuccessRehashNeeded;

                    // Mettre à jour les hashes qui nécessitent une migration.
                    if (resultat ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        string nouveauHash =
                            hasher.HashPassword(utilisateur, password);

                        using var commandeHash = new OracleCommand(@"
                        UPDATE UTILISATEUR
                        SET MOT_DE_PASSE = :motDePasse
                        WHERE ID_UTILISATEUR = :idUtilisateur",
                            connection);

                        commandeHash.BindByName = true;
                        commandeHash.Parameters.Add(
                            ":motDePasse", OracleDbType.Varchar2).Value =
                            nouveauHash;
                        commandeHash.Parameters.Add(
                            ":idUtilisateur", OracleDbType.Int32).Value =
                            idUtilisateur;

                        commandeHash.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Compatibilité temporaire avec les anciens comptes
                    // dont le mot de passe est encore stocké en clair.
                    if (motDePasseStocke == password)
                    {
                        motDePasseCorrect = true;

                        string nouveauHash =
                            hasher.HashPassword(utilisateur, password);

                        using var commandeHash = new OracleCommand(@"
                        UPDATE UTILISATEUR
                        SET MOT_DE_PASSE = :motDePasse
                        WHERE ID_UTILISATEUR = :idUtilisateur",
                            connection);

                        commandeHash.BindByName = true;
                        commandeHash.Parameters.Add(
                            ":motDePasse", OracleDbType.Varchar2).Value =
                            nouveauHash;
                        commandeHash.Parameters.Add(
                            ":idUtilisateur", OracleDbType.Int32).Value =
                            idUtilisateur;

                        commandeHash.ExecuteNonQuery();
                    }
                }

                if (!motDePasseCorrect)
                {
                    ViewBag.Erreur =
                        "Email ou mot de passe incorrect.";
                    return View();
                }

                // Vérifier que le rôle est reconnu avant de créer la session.
                if (role != "ADMIN" &&
                    role != "BIBLIOTHECAIRE" &&
                    role != "ADHERENT")
                {
                    ViewBag.Erreur =
                        "Le rôle de ce compte n'est pas reconnu. Contactez l'administrateur.";
                    return View();
                }

                // Créer les claims de l'utilisateur connecté.
                var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    idUtilisateur.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    prenom + " " + nom),

                new Claim(
                    ClaimTypes.Role,
                    role)
            };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal);

                // Rediriger selon le rôle.
                if (role == "ADMIN")
                    return RedirectToAction("Index", "Admin");

                if (role == "BIBLIOTHECAIRE")
                    return RedirectToAction("Index", "Bibliothecaire");

                return RedirectToAction("Index", "Adherent");
            }
            catch (Exception)
            {
                ViewBag.Erreur =
                    "Une erreur est survenue lors de la connexion. Veuillez réessayer.";
                return View();
            }
        }

        // =========================================================
        // REGISTER - AFFICHER LA PAGE D'INSCRIPTION
        // =========================================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // =========================================================
        // REGISTER - CRÉER UN NOUVEAU COMPTE ADHÉRENT
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(
            string nom,
            string prenom,
            string email,
            string password,
            string confirmationPassword,
            string telephone,
            string adresse)
        {
            if (string.IsNullOrWhiteSpace(nom) ||
                string.IsNullOrWhiteSpace(prenom) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmationPassword))
            {
                ViewBag.Erreur =
                    "Veuillez remplir tous les champs obligatoires.";
                return View();
            }

            if (password != confirmationPassword)
            {
                ViewBag.Erreur =
                    "Les mots de passe ne correspondent pas.";
                return View();
            }

            using var connection = _factory.GetConnection();

            try
            {
                connection.Open();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Vérifier si l'e-mail existe déjà.
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
                        email.Trim();

                    int emailExiste =
                        Convert.ToInt32(commandeEmail.ExecuteScalar());

                    if (emailExiste > 0)
                    {
                        transaction.Rollback();
                        ViewBag.Erreur =
                            "Cette adresse e-mail est déjà utilisée.";
                        return View();
                    }

                    // Générer l'identifiant.
                    using var commandeId = new OracleCommand(
                        "SELECT SEQ_UTILISATEUR.NEXTVAL FROM DUAL",
                        connection);

                    commandeId.Transaction = transaction;

                    int idUtilisateur =
                        Convert.ToInt32(commandeId.ExecuteScalar());

                    // Préparer l'utilisateur et hacher le mot de passe.
                    var utilisateur = new Utilisateur
                    {
                        IdUtilisateur = idUtilisateur,
                        Nom = nom.Trim(),
                        Prenom = prenom.Trim(),
                        Email = email.Trim(),
                        Role = "ADHERENT",
                        Etat = "ACTIF"
                    };

                    var hasher = new PasswordHasher<Utilisateur>();

                    string motDePasseHash =
                        hasher.HashPassword(utilisateur, password);

                    // Insérer l'utilisateur.
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
                        :idUtilisateur,
                        :nom,
                        :prenom,
                        :email,
                        :motDePasse,
                        'ADHERENT',
                        'ACTIF',
                        SYSDATE
                    )";

                    using var commandeUtilisateur =
                        new OracleCommand(sqlUtilisateur, connection);

                    commandeUtilisateur.Transaction = transaction;
                    commandeUtilisateur.BindByName = true;

                    commandeUtilisateur.Parameters.Add(
                        ":idUtilisateur", OracleDbType.Int32).Value =
                        idUtilisateur;
                    commandeUtilisateur.Parameters.Add(
                        ":nom", OracleDbType.Varchar2).Value = nom.Trim();
                    commandeUtilisateur.Parameters.Add(
                        ":prenom", OracleDbType.Varchar2).Value = prenom.Trim();
                    commandeUtilisateur.Parameters.Add(
                        ":email", OracleDbType.Varchar2).Value = email.Trim();
                    commandeUtilisateur.Parameters.Add(
                        ":motDePasse", OracleDbType.Varchar2).Value =
                        motDePasseHash;

                    commandeUtilisateur.ExecuteNonQuery();

                    // Insérer les informations de l'adhérent.
                    string sqlAdherent = @"
                    INSERT INTO ADHERENT
                    (
                        ID_UTILISATEUR,
                        TELEPHONE,
                        ADRESSE,
                        DATE_INSCRIPTION,
                        STATUT
                    )
                    VALUES
                    (
                        :idUtilisateur,
                        :telephone,
                        :adresse,
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

                    commandeAdherent.Parameters.Add(
                        ":telephone", OracleDbType.Varchar2).Value =
                        string.IsNullOrWhiteSpace(telephone)
                            ? DBNull.Value
                            : telephone.Trim();

                    commandeAdherent.Parameters.Add(
                        ":adresse", OracleDbType.Varchar2).Value =
                        string.IsNullOrWhiteSpace(adresse)
                            ? DBNull.Value
                            : adresse.Trim();

                    commandeAdherent.ExecuteNonQuery();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                TempData["Succes"] =
                    "Votre compte a été créé avec succès. Vous pouvez maintenant vous connecter.";

                return RedirectToAction("Login");
            }
            catch (Exception)
            {
                ViewBag.Erreur =
                    "Une erreur est survenue lors de la création du compte. Veuillez réessayer.";
                return View();
            }
        }

        // =========================================================
        // LOGOUT - DÉCONNEXION
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }
    }

}