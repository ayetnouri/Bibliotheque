using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Bibliotheque.Controllers
{
    public class LivresController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public LivresController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =====================================================
        // 1. CATALOGUE PUBLIC AVEC RECHERCHE
        // =====================================================
        [AllowAnonymous]
        public IActionResult Index(string? recherche)
        {
            try
            {
                var livres = RechercherLivres(recherche);

                ViewBag.Recherche = recherche?.Trim();

                return View(livres);
            }
            catch (Exception)
            {
                TempData["Erreur"] =
                    "Impossible de charger le catalogue pour le moment.";

                return View(new List<Livre>());
            }
        }

        // =====================================================
        // 2. DETAILS D'UN LIVRE
        // =====================================================
        [AllowAnonymous]
        public IActionResult Details(int id)
        {
            if (id <= 0)
                return NotFound();

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
                SELECT ID_LIVRE,
                       ISBN,
                       TITRE,
                       RESUME,
                       NOMBRE_PAGES,
                       ANNEE_PUBLICATION,
                       NOMBRE_EXEMPLAIRES,
                       EXEMPLAIRES_DISPONIBLES,
                       EMPLACEMENT,
                       ID_CATEGORIE,
                       ID_LANGUE,
                       ID_EDITEUR
                FROM LIVRE
                WHERE ID_LIVRE = :id";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;
            commande.Parameters.Add(":id", OracleDbType.Int32).Value = id;

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
                return NotFound();

            Livre livre = LireLivre(reader);

            return View(livre);
        }

        // =====================================================
        // 3. GESTION DES LIVRES
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Gestion(string? recherche)
        {
            try
            {
                var livres = RechercherLivres(recherche);

                ViewBag.Recherche = recherche?.Trim();

                return View(livres);
            }
            catch (Exception)
            {
                TempData["Erreur"] =
                    "Impossible de charger la gestion des livres.";

                return View(new List<Livre>());
            }
        }

        // =====================================================
        // 4. AJOUTER UN LIVRE - GET
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Create()
        {
            try
            {
                ChargerListesCreate();
                return View();
            }
            catch (Exception)
            {
                ViewBag.Erreur =
                    "Impossible de charger les catégories, langues et éditeurs.";

                return View();
            }
        }

        // =====================================================
        // 5. AJOUTER UN LIVRE - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Create(Livre livre)
        {
            string? erreur = ValiderLivre(livre, true);

            if (erreur != null)
            {
                ViewBag.Erreur = erreur;
                ChargerListesCreate();
                return View(livre);
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                string sql = @"
                    INSERT INTO LIVRE
                    (
                        ID_LIVRE,
                        ISBN,
                        TITRE,
                        RESUME,
                        NOMBRE_PAGES,
                        ANNEE_PUBLICATION,
                        NOMBRE_EXEMPLAIRES,
                        EXEMPLAIRES_DISPONIBLES,
                        EMPLACEMENT,
                        ID_CATEGORIE,
                        ID_LANGUE,
                        ID_EDITEUR
                    )
                    VALUES
                    (
                        SEQ_LIVRE.NEXTVAL,
                        :isbn,
                        :titre,
                        :resume,
                        :pages,
                        :annee,
                        :total,
                        :disponibles,
                        :emplacement,
                        :categorie,
                        :langue,
                        :editeur
                    )";

                using var commande = new OracleCommand(sql, connection);
                commande.Transaction = transaction;
                commande.BindByName = true;

                AjouterParametresLivre(commande, livre);

                commande.ExecuteNonQuery();

                transaction.Commit();

                TempData["Succes"] =
                    "Le livre a été ajouté avec succès.";

                return RedirectToAction(nameof(Gestion));
            }
            catch (Exception)
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
                    "L'ajout du livre a échoué. Vérifiez les données saisies et les contraintes de la base de données.";

                ChargerListesCreate();
                return View(livre);
            }
        }

        // =====================================================
        // 6. MODIFIER UN LIVRE - GET
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Edit(int id)
        {
            if (id <= 0)
                return NotFound();

            using var connection = _factory.GetConnection();
            connection.Open();

            Livre? livre = ObtenirLivre(connection, id);

            if (livre == null)
                return NotFound();

            ChargerListesCreate();

            return View(livre);
        }

        // =====================================================
        // 7. MODIFIER UN LIVRE - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Edit(Livre livre)
        {
            string? erreur = ValiderLivre(livre, false);

            if (erreur != null)
            {
                ViewBag.Erreur = erreur;
                ChargerListesCreate();
                return View(livre);
            }

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Verrouiller la ligne du livre pendant la modification.
                string sqlVerrou = @"
                    SELECT ID_LIVRE
                    FROM LIVRE
                    WHERE ID_LIVRE = :id
                    FOR UPDATE";

                using (var commandeVerrou =
                    new OracleCommand(sqlVerrou, connection))
                {
                    commandeVerrou.Transaction = transaction;
                    commandeVerrou.BindByName = true;

                    commandeVerrou.Parameters.Add(
                        ":id", OracleDbType.Int32).Value = livre.IdLivre;

                    object? resultat = commandeVerrou.ExecuteScalar();

                    if (resultat == null || resultat == DBNull.Value)
                    {
                        transaction.Rollback();
                        return NotFound();
                    }
                }

                // Compter les emprunts actifs.
                string sqlEmprunts = @"
                    SELECT COUNT(*)
                    FROM EMPRUNT
                    WHERE ID_LIVRE = :id
                      AND STATUT IN ('EN_COURS', 'EN_RETARD')";

                int empruntsActifs;

                using (var commandeEmprunts =
                    new OracleCommand(sqlEmprunts, connection))
                {
                    commandeEmprunts.Transaction = transaction;
                    commandeEmprunts.BindByName = true;

                    commandeEmprunts.Parameters.Add(
                        ":id", OracleDbType.Int32).Value = livre.IdLivre;

                    empruntsActifs = Convert.ToInt32(
                        commandeEmprunts.ExecuteScalar());
                }

                // Le total doit couvrir tous les emprunts actifs.
                if (livre.NombreExemplaires!.Value < empruntsActifs)
                {
                    transaction.Rollback();

                    ViewBag.Erreur =
                        $"Impossible de fixer le total à {livre.NombreExemplaires.Value} : " +
                        $"{empruntsActifs} exemplaire(s) sont actuellement empruntés.";

                    ChargerListesCreate();
                    return View(livre);
                }

                // Le stock disponible est recalculé.
                int disponibles =
                    livre.NombreExemplaires.Value - empruntsActifs;

                string sql = @"
                    UPDATE LIVRE
                    SET ISBN = :isbn,
                        TITRE = :titre,
                        RESUME = :resume,
                        NOMBRE_PAGES = :pages,
                        ANNEE_PUBLICATION = :annee,
                        NOMBRE_EXEMPLAIRES = :total,
                        EXEMPLAIRES_DISPONIBLES = :disponibles,
                        EMPLACEMENT = :emplacement,
                        ID_CATEGORIE = :categorie,
                        ID_LANGUE = :langue,
                        ID_EDITEUR = :editeur
                    WHERE ID_LIVRE = :id";

                using var commande = new OracleCommand(sql, connection);
                commande.Transaction = transaction;
                commande.BindByName = true;

                AjouterParametresLivre(commande, livre);

                commande.Parameters.Add(
                    ":disponibles", OracleDbType.Int32).Value = disponibles;

                commande.Parameters.Add(
                    ":id", OracleDbType.Int32).Value = livre.IdLivre;

                int lignes = commande.ExecuteNonQuery();

                if (lignes == 0)
                {
                    transaction.Rollback();
                    return NotFound();
                }

                transaction.Commit();

                TempData["Succes"] =
                    "Le livre a été modifié avec succès.";

                return RedirectToAction(nameof(Gestion));
            }
            catch (Exception)
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
                    "La modification du livre a échoué. Vérifiez les données et réessayez.";

                ChargerListesCreate();
                return View(livre);
            }
        }

        // =====================================================
        // 8. SUPPRIMER UN LIVRE - GET
        // =====================================================
        [HttpGet]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult Delete(int id)
        {
            if (id <= 0)
                return NotFound();

            using var connection = _factory.GetConnection();
            connection.Open();

            Livre? livre = ObtenirLivre(connection, id);

            if (livre == null)
                return NotFound();

            return View(livre);
        }

        // =====================================================
        // 9. CONFIRMER LA SUPPRESSION - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
        public IActionResult DeleteConfirmed(
            [FromForm(Name = "IdLivre")] int id)
        {
            if (id <= 0)
                return NotFound();

            using var connection = _factory.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Interdire la suppression si un emprunt existe,
                // même s'il est déjà retourné.
                string sqlVerifier = @"
                    SELECT COUNT(*)
                    FROM EMPRUNT
                    WHERE ID_LIVRE = :id";

                using (var commandeVerifier =
                    new OracleCommand(sqlVerifier, connection))
                {
                    commandeVerifier.Transaction = transaction;
                    commandeVerifier.BindByName = true;

                    commandeVerifier.Parameters.Add(
                        ":id", OracleDbType.Int32).Value = id;

                    int nombreEmprunts = Convert.ToInt32(
                        commandeVerifier.ExecuteScalar());

                    if (nombreEmprunts > 0)
                    {
                        transaction.Rollback();

                        TempData["Erreur"] =
                            "Impossible de supprimer ce livre : il possède des emprunts enregistrés.";

                        return RedirectToAction(nameof(Gestion));
                    }
                }

                // Supprimer les associations avec les auteurs.
                string sqlAuteur = @"
                    DELETE FROM LIVRE_AUTEUR
                    WHERE ID_LIVRE = :id";

                using (var commandeAuteur =
                    new OracleCommand(sqlAuteur, connection))
                {
                    commandeAuteur.Transaction = transaction;
                    commandeAuteur.BindByName = true;

                    commandeAuteur.Parameters.Add(
                        ":id", OracleDbType.Int32).Value = id;

                    commandeAuteur.ExecuteNonQuery();
                }

                // Supprimer le livre.
                string sqlLivre = @"
                    DELETE FROM LIVRE
                    WHERE ID_LIVRE = :id";

                using (var commandeLivre =
                    new OracleCommand(sqlLivre, connection))
                {
                    commandeLivre.Transaction = transaction;
                    commandeLivre.BindByName = true;

                    commandeLivre.Parameters.Add(
                        ":id", OracleDbType.Int32).Value = id;

                    int lignes = commandeLivre.ExecuteNonQuery();

                    if (lignes == 0)
                    {
                        transaction.Rollback();
                        return NotFound();
                    }
                }

                transaction.Commit();

                TempData["Succes"] =
                    "Le livre a été supprimé avec succès.";

                return RedirectToAction(nameof(Gestion));
            }
            catch (Exception)
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // La transaction peut déjà être terminée.
                }

                TempData["Erreur"] =
                    "La suppression a échoué. Vérifiez les contraintes de la base de données.";

                return RedirectToAction(nameof(Gestion));
            }
        }

        // =====================================================
        // 10. RECHERCHE PARTAGEE ENTRE INDEX ET GESTION
        // =====================================================
        private List<Livre> RechercherLivres(string? recherche)
        {
            var livres = new List<Livre>();

            using var connection = _factory.GetConnection();
            connection.Open();

            string sql = @"
    SELECT DISTINCT
           L.ID_LIVRE,
           L.ISBN,
           L.TITRE,
           L.RESUME,
           L.NOMBRE_PAGES,
           L.ANNEE_PUBLICATION,
           L.NOMBRE_EXEMPLAIRES,
           L.EXEMPLAIRES_DISPONIBLES,
           L.EMPLACEMENT,
           L.ID_CATEGORIE,
           L.ID_LANGUE,
           L.ID_EDITEUR
    FROM LIVRE L
    LEFT JOIN CATEGORIE C
        ON L.ID_CATEGORIE = C.ID_CATEGORIE
    LEFT JOIN LIVRE_AUTEUR LA
        ON L.ID_LIVRE = LA.ID_LIVRE
    LEFT JOIN AUTEUR A
        ON LA.ID_AUTEUR = A.ID_AUTEUR
    WHERE
        (:recherche IS NULL
         OR TRIM(:recherche) IS NULL
         OR UPPER(L.TITRE) LIKE '%' || UPPER(TRIM(:recherche)) || '%'
         OR UPPER(A.NOM) LIKE '%' || UPPER(TRIM(:recherche)) || '%'
         OR UPPER(A.PRENOM) LIKE '%' || UPPER(TRIM(:recherche)) || '%'
         OR UPPER(C.NOM) LIKE '%' || UPPER(TRIM(:recherche)) || '%')
    ORDER BY L.TITRE";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;

            commande.Parameters.Add(
                ":recherche", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(recherche)
                    ? DBNull.Value
                    : recherche.Trim();

            using var reader = commande.ExecuteReader();

            while (reader.Read())
            {
                livres.Add(LireLivre(reader));
            }

            return livres;
        }

        // =====================================================
        // 11. LIRE UN LIVRE A PARTIR D'UN READER
        // =====================================================
        private Livre LireLivre(OracleDataReader reader)
        {
            return new Livre
            {
                IdLivre = Convert.ToInt32(reader["ID_LIVRE"]),
                ISBN = reader["ISBN"] == DBNull.Value
                    ? string.Empty
                    : reader["ISBN"].ToString()!,
                Titre = reader["TITRE"] == DBNull.Value
                    ? string.Empty
                    : reader["TITRE"].ToString()!,

                Resume = reader["RESUME"] == DBNull.Value
                    ? null
                    : reader["RESUME"].ToString(),

                NombrePages = reader["NOMBRE_PAGES"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["NOMBRE_PAGES"]),

                AnneePublication = reader["ANNEE_PUBLICATION"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["ANNEE_PUBLICATION"]),

                NombreExemplaires = reader["NOMBRE_EXEMPLAIRES"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["NOMBRE_EXEMPLAIRES"]),

                ExemplairesDisponibles =
                    reader["EXEMPLAIRES_DISPONIBLES"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["EXEMPLAIRES_DISPONIBLES"]),

                Emplacement = reader["EMPLACEMENT"] == DBNull.Value
                    ? null
                    : reader["EMPLACEMENT"].ToString(),

                IdCategorie = Convert.ToInt32(reader["ID_CATEGORIE"]),
                IdLangue = Convert.ToInt32(reader["ID_LANGUE"]),
                IdEditeur = Convert.ToInt32(reader["ID_EDITEUR"])
            };
        }

        // =====================================================
        // 12. OBTENIR UN LIVRE PAR SON IDENTIFIANT
        // =====================================================
        private Livre? ObtenirLivre(
            OracleConnection connection, int id)
        {
            string sql = @"
                SELECT ID_LIVRE,
                       ISBN,
                       TITRE,
                       RESUME,
                       NOMBRE_PAGES,
                       ANNEE_PUBLICATION,
                       NOMBRE_EXEMPLAIRES,
                       EXEMPLAIRES_DISPONIBLES,
                       EMPLACEMENT,
                       ID_CATEGORIE,
                       ID_LANGUE,
                       ID_EDITEUR
                FROM LIVRE
                WHERE ID_LIVRE = :id";

            using var commande = new OracleCommand(sql, connection);
            commande.BindByName = true;

            commande.Parameters.Add(
                ":id", OracleDbType.Int32).Value = id;

            using var reader = commande.ExecuteReader();

            if (!reader.Read())
                return null;

            return LireLivre(reader);
        }

        // =====================================================
        // 13. VALIDER LES DONNEES D'UN LIVRE
        // =====================================================
        private string? ValiderLivre(Livre livre, bool creation)
        {
            if (string.IsNullOrWhiteSpace(livre.ISBN) ||
                string.IsNullOrWhiteSpace(livre.Titre))
            {
                return "L'ISBN et le titre sont obligatoires.";
            }

            if (livre.IdLivre < 0)
            {
                return "L'identifiant du livre est invalide.";
            }

            if (livre.NombrePages.HasValue &&
                livre.NombrePages.Value < 0)
            {
                return "Le nombre de pages ne peut pas être négatif.";
            }

            if (livre.AnneePublication.HasValue &&
                livre.AnneePublication.Value < 0)
            {
                return "L'année de publication ne peut pas être négative.";
            }

            if (livre.NombreExemplaires == null ||
                livre.NombreExemplaires.Value < 0)
            {
                return "Le nombre total d'exemplaires est obligatoire et ne peut pas être négatif.";
            }

            if (creation &&
                livre.ExemplairesDisponibles == null)
            {
                return "Le nombre d'exemplaires disponibles est obligatoire.";
            }

            if (creation &&
                livre.ExemplairesDisponibles.HasValue &&
                livre.ExemplairesDisponibles.Value < 0)
            {
                return "Le nombre d'exemplaires disponibles ne peut pas être négatif.";
            }

            if (creation &&
                livre.ExemplairesDisponibles.HasValue &&
                livre.ExemplairesDisponibles.Value >
                livre.NombreExemplaires.Value)
            {
                return "Les exemplaires disponibles ne peuvent pas dépasser le nombre total.";
            }

            if (livre.IdCategorie <= 0 ||
                livre.IdLangue <= 0 ||
                livre.IdEditeur <= 0)
            {
                return "Veuillez sélectionner une catégorie, une langue et un éditeur.";
            }

            return null;
        }

        // =====================================================
        // 14. AJOUTER LES PARAMETRES DU LIVRE
        // =====================================================
        private void AjouterParametresLivre(
            OracleCommand commande, Livre livre)
        {
            commande.Parameters.Add(
                ":isbn", OracleDbType.Varchar2).Value =
                livre.ISBN.Trim();

            commande.Parameters.Add(
                ":titre", OracleDbType.Varchar2).Value =
                livre.Titre.Trim();

            commande.Parameters.Add(
                ":resume", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(livre.Resume)
                    ? DBNull.Value
                    : livre.Resume.Trim();

            commande.Parameters.Add(
                ":pages", OracleDbType.Int32).Value =
                livre.NombrePages.HasValue
                    ? livre.NombrePages.Value
                    : DBNull.Value;

            commande.Parameters.Add(
                ":annee", OracleDbType.Int32).Value =
                livre.AnneePublication.HasValue
                    ? livre.AnneePublication.Value
                    : DBNull.Value;

            commande.Parameters.Add(
                ":total", OracleDbType.Int32).Value =
                livre.NombreExemplaires.HasValue
                    ? livre.NombreExemplaires.Value
                    : DBNull.Value;

            // Utilisé uniquement par la création.
            // En modification, ce paramètre est ajouté séparément
            // après le calcul du stock disponible.
            if (commande.CommandText.Contains("INSERT INTO LIVRE"))
            {
                commande.Parameters.Add(
                    ":disponibles", OracleDbType.Int32).Value =
                    livre.ExemplairesDisponibles.HasValue
                        ? livre.ExemplairesDisponibles.Value
                        : DBNull.Value;
            }

            commande.Parameters.Add(
                ":emplacement", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(livre.Emplacement)
                    ? DBNull.Value
                    : livre.Emplacement.Trim();

            commande.Parameters.Add(
                ":categorie", OracleDbType.Int32).Value =
                livre.IdCategorie;

            commande.Parameters.Add(
                ":langue", OracleDbType.Int32).Value =
                livre.IdLangue;

            commande.Parameters.Add(
                ":editeur", OracleDbType.Int32).Value =
                livre.IdEditeur;
        }

        // =====================================================
        // 15. CHARGER LES LISTES POUR CREATE ET EDIT
        // =====================================================
        private void ChargerListesCreate()
        {
            var categories = new List<Categorie>();
            var langues = new List<Langue>();
            var editeurs = new List<Editeur>();

            using var connection = _factory.GetConnection();
            connection.Open();

            // Catégories
            string sqlCategories = @"
                SELECT ID_CATEGORIE, NOM
                FROM CATEGORIE
                ORDER BY NOM";

            using (var commande =
                new OracleCommand(sqlCategories, connection))
            using (var reader = commande.ExecuteReader())
            {
                while (reader.Read())
                {
                    categories.Add(new Categorie
                    {
                        IdCategorie =
                            Convert.ToInt32(reader["ID_CATEGORIE"]),
                        Nom = reader["NOM"].ToString()!
                    });
                }
            }

            // Langues
            string sqlLangues = @"
                SELECT ID_LANGUE, NOM, CODE
                FROM LANGUE
                ORDER BY NOM";

            using (var commande =
                new OracleCommand(sqlLangues, connection))
            using (var reader = commande.ExecuteReader())
            {
                while (reader.Read())
                {
                    langues.Add(new Langue
                    {
                        IdLangue =
                            Convert.ToInt32(reader["ID_LANGUE"]),
                        Nom = reader["NOM"].ToString()!,
                        Code = reader["CODE"].ToString()!
                    });
                }
            }

            // Éditeurs
            string sqlEditeurs = @"
                SELECT ID_EDITEUR, NOM
                FROM EDITEUR
                ORDER BY NOM";

            using (var commande =
                new OracleCommand(sqlEditeurs, connection))
            using (var reader = commande.ExecuteReader())
            {
                while (reader.Read())
                {
                    editeurs.Add(new Editeur
                    {
                        IdEditeur =
                            Convert.ToInt32(reader["ID_EDITEUR"]),
                        Nom = reader["NOM"].ToString()!
                    });
                }
            }

            ViewBag.Categories = categories;
            ViewBag.Langues = langues;
            ViewBag.Editeurs = editeurs;
        }
    }
}