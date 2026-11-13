using Bibliotheque.Data;
using Bibliotheque.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace Bibliotheque.Controllers
{
    [Authorize(Roles = "ADMIN,BIBLIOTHECAIRE")]
    public class EmpruntsController : Controller
    {
        private readonly OracleConnectionFactory _factory;

        public EmpruntsController(OracleConnectionFactory factory)
        {
            _factory = factory;
        }

        // =========================
        // LISTE DES EMPRUNTS
        // =========================

        public IActionResult Index()
        {
            var emprunts = new List<Emprunt>();

            var connection = _factory.GetConnection();
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

                    U.NOM || ' ' || U.PRENOM AS NOM_ADHERENT,

                    L.TITRE AS TITRE_LIVRE

                FROM EMPRUNT E

                INNER JOIN UTILISATEUR U
                    ON E.ID_UTILISATEUR = U.ID_UTILISATEUR

                INNER JOIN LIVRE L
                    ON E.ID_LIVRE = L.ID_LIVRE

                ORDER BY E.DATE_EMPRUNT DESC
            ";

            var commande = new OracleCommand(sql, connection);

            var reader = commande.ExecuteReader();

            while (reader.Read())
            {
                var emprunt = new Emprunt
                {
                    IdEmprunt = Convert.ToInt32(
                        reader["ID_EMPRUNT"]),

                    IdUtilisateur = Convert.ToInt32(
                        reader["ID_UTILISATEUR"]),

                    IdLivre = Convert.ToInt32(
                        reader["ID_LIVRE"]),

                    DateEmprunt = Convert.ToDateTime(
                        reader["DATE_EMPRUNT"]),

                    DateRetourPrevue = Convert.ToDateTime(
                        reader["DATE_RETOUR_PREVUE"]),

                    DateRetourReelle = reader["DATE_RETOUR_REELLE"]
                        == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["DATE_RETOUR_REELLE"]),

                    Statut = reader["STATUT"].ToString() ?? string.Empty,

                    Penalite = reader["PENALITE"]
                        == DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["PENALITE"]),

                    NomAdherent = reader["NOM_ADHERENT"].ToString() ?? string.Empty,

                    TitreLivre = reader["TITRE_LIVRE"].ToString() ?? string.Empty
                };

                emprunts.Add(emprunt);
            }

            // Fermer le reader
            reader.Close();

            // Fermer la connexion
            connection.Close();

            // Envoyer la liste à la vue
            return View(emprunts);
        }
        // =========================
        // DÉTAILS D'UN EMPRUNT
        // =========================

        [HttpGet]
        public IActionResult Details(int id)
        {
            var connection = _factory.GetConnection();
            connection.Open();

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

            U.NOM || ' ' || U.PRENOM AS NOM_ADHERENT,

            L.TITRE AS TITRE_LIVRE

        FROM EMPRUNT E

        INNER JOIN UTILISATEUR U
            ON E.ID_UTILISATEUR = U.ID_UTILISATEUR

        INNER JOIN LIVRE L
            ON E.ID_LIVRE = L.ID_LIVRE

        WHERE E.ID_EMPRUNT = :id
    ";

            var commande = new OracleCommand(sql, connection);

            commande.Parameters.Add(
                ":id",
                OracleDbType.Int32
            ).Value = id;

            var reader = commande.ExecuteReader();

            if (!reader.Read())
            {
                reader.Close();
                connection.Close();

                return NotFound();
            }

            var emprunt = new Emprunt
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
                            reader["DATE_RETOUR_REELLE"]
                        ),

                Statut =
    reader["STATUT"].ToString() ?? string.Empty,


                Penalite =
                    reader["PENALITE"] == DBNull.Value
                        ? null
                        : Convert.ToDecimal(
                            reader["PENALITE"]
                        ),

                NomAdherent =
                reader["NOM_ADHERENT"].ToString() ?? string.Empty,

                TitreLivre =
                reader["TITRE_LIVRE"].ToString() ?? string.Empty
            };

            reader.Close();
            connection.Close();

            return View(emprunt);
        }
        // =========================
        // ENREGISTRER LE RETOUR
        // =========================

        [HttpGet]
        public IActionResult Retour(int id)
        {
            var connection = _factory.GetConnection();
            connection.Open();

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
            U.NOM || ' ' || U.PRENOM AS NOM_ADHERENT,
            L.TITRE AS TITRE_LIVRE
        FROM EMPRUNT E
        INNER JOIN UTILISATEUR U
            ON E.ID_UTILISATEUR = U.ID_UTILISATEUR
        INNER JOIN LIVRE L
            ON E.ID_LIVRE = L.ID_LIVRE
        WHERE E.ID_EMPRUNT = :id
    ";

            var commande = new OracleCommand(sql, connection);

            commande.Parameters.Add(
                ":id",
                OracleDbType.Int32
            ).Value = id;

            var reader = commande.ExecuteReader();

            if (!reader.Read())
            {
                reader.Close();
                connection.Close();

                return NotFound();
            }

            var emprunt = new Emprunt
            {
                IdEmprunt = Convert.ToInt32(reader["ID_EMPRUNT"]),

                IdUtilisateur = Convert.ToInt32(reader["ID_UTILISATEUR"]),

                IdLivre = Convert.ToInt32(reader["ID_LIVRE"]),

                DateEmprunt = Convert.ToDateTime(reader["DATE_EMPRUNT"]),

                DateRetourPrevue =
                    Convert.ToDateTime(reader["DATE_RETOUR_PREVUE"]),

                DateRetourReelle =
                    reader["DATE_RETOUR_REELLE"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["DATE_RETOUR_REELLE"]),

                Statut = reader["STATUT"].ToString() ?? string.Empty,

                Penalite =
                    reader["PENALITE"] == DBNull.Value
                    ? null
                    : Convert.ToDecimal(reader["PENALITE"]),

                NomAdherent =
                    reader["NOM_ADHERENT"].ToString(),

                TitreLivre =
                    reader["TITRE_LIVRE"].ToString()
            };

            reader.Close();
            connection.Close();

            // Seuls les emprunts actifs ou en retard peuvent être retournés
            if (emprunt.Statut != "EN_COURS" &&
                emprunt.Statut != "EN_RETARD")
            {
                return BadRequest(
                    "Cet emprunt a déjà été retourné ou ne peut pas être rendu."
                );
            }

            return View(emprunt);
        }
        // =========================
        // CONFIRMER LE RETOUR
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RetourConfirme(int id)
        {
            var connection = _factory.GetConnection();
            connection.Open();

            var transaction = connection.BeginTransaction();

            try
            {
                // =========================
                // 1. RÉCUPÉRER L'EMPRUNT
                // =========================

                string sqlEmprunt = @"
            SELECT
                ID_LIVRE,
                DATE_RETOUR_PREVUE,
                STATUT
            FROM EMPRUNT
            WHERE ID_EMPRUNT = :id
        ";

                var commandeEmprunt =
                    new OracleCommand(sqlEmprunt, connection);

                commandeEmprunt.Transaction = transaction;

                commandeEmprunt.Parameters.Add(
                    ":id",
                    OracleDbType.Int32
                ).Value = id;

                var reader = commandeEmprunt.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    transaction.Rollback();
                    connection.Close();

                    return NotFound();
                }

                int idLivre = Convert.ToInt32(
                    reader["ID_LIVRE"]
                );

                DateTime dateRetourPrevue =
                    Convert.ToDateTime(
                        reader["DATE_RETOUR_PREVUE"]
                    );

                string statut =
    reader["STATUT"].ToString() ?? string.Empty;

                reader.Close();


                // =========================
                // 2. VÉRIFIER LE STATUT
                // =========================

                if (statut != "EN_COURS" && statut != "EN_RETARD")
                {
                    transaction.Rollback();
                    connection.Close();

                    return BadRequest(
                        "Cet emprunt a déjà été retourné."
                    );
                }


                // =========================
                // 3. CALCULER LA PÉNALITÉ
                // =========================

                DateTime dateRetour = DateTime.Now;

                decimal penalite = 0;

                if (dateRetour.Date > dateRetourPrevue.Date)
                {
                    int joursRetard =
                        (dateRetour.Date - dateRetourPrevue.Date).Days;

                    // 1 unité monétaire par jour de retard
                    penalite = joursRetard * 1;
                }


                // =========================
                // 4. METTRE À JOUR L'EMPRUNT
                // =========================

                string sqlUpdateEmprunt = @"
    UPDATE EMPRUNT
    SET
        DATE_RETOUR_REELLE = :dateRetour,
        STATUT = 'RETOURNE',
        PENALITE = :penalite
    WHERE ID_EMPRUNT = :id
      AND STATUT IN ('EN_COURS', 'EN_RETARD')
";

                var commandeUpdateEmprunt =
                    new OracleCommand(
                        sqlUpdateEmprunt,
                        connection
                    );

                commandeUpdateEmprunt.Transaction =
                    transaction;

                commandeUpdateEmprunt.Parameters.Add(
                    ":dateRetour",
                    OracleDbType.Date
                ).Value = dateRetour;

                commandeUpdateEmprunt.Parameters.Add(
                    ":penalite",
                    OracleDbType.Decimal
                ).Value = penalite;

                commandeUpdateEmprunt.Parameters.Add(
                    ":id",
                    OracleDbType.Int32
                ).Value = id;

                int empruntsModifies =
     commandeUpdateEmprunt.ExecuteNonQuery();

                if (empruntsModifies != 1)
                {
                    transaction.Rollback();
                    connection.Close();

                    return BadRequest(
                        "Cet emprunt a déjà été retourné ou a été modifié."
                    );
                }


                // =========================
                // 5. AUGMENTER LE STOCK
                // =========================

                string sqlStock = @"
    UPDATE LIVRE
    SET EXEMPLAIRES_DISPONIBLES =
        EXEMPLAIRES_DISPONIBLES + 1
    WHERE ID_LIVRE = :idLivre
    AND EXEMPLAIRES_DISPONIBLES < NOMBRE_EXEMPLAIRES
";

                var commandeStock =
                    new OracleCommand(
                        sqlStock,
                        connection
                    );

                commandeStock.Transaction =
                    transaction;

                commandeStock.Parameters.Add(
                    ":idLivre",
                    OracleDbType.Int32
                ).Value = idLivre;

                int livresModifies = commandeStock.ExecuteNonQuery();

                if (livresModifies != 1)
                {
                    transaction.Rollback();
                    connection.Close();

                    return BadRequest(
                        "Le stock du livre n'a pas pu être mis à jour. Le retour a été annulé."
                    );
                }


                // =========================
                // 6. VALIDER LA TRANSACTION
                // =========================

                transaction.Commit();

                connection.Close();

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                transaction.Rollback();
                connection.Close();

                return Content(
                    "Une erreur est survenue lors de l'enregistrement du retour."
                );
            }
        }

        // =========================
        // FORMULAIRE NOUVEL EMPRUNT
        // =========================

        [HttpGet]
        public IActionResult Create()
        {
            var connection = _factory.GetConnection();
            connection.Open();

            // =========================
            // RÉCUPÉRER LES ADHÉRENTS
            // =========================

            var adherents = new List<dynamic>();

            string sqlAdherents = @"
                SELECT ID_UTILISATEUR, NOM, PRENOM
                FROM UTILISATEUR
                WHERE ROLE = 'ADHERENT'
                AND ETAT = 'ACTIF'
                ORDER BY NOM, PRENOM
            ";

            var commandeAdherents =
                new OracleCommand(sqlAdherents, connection);

            var readerAdherents =
                commandeAdherents.ExecuteReader();

            while (readerAdherents.Read())
            {
                adherents.Add(new
                {
                    Id = Convert.ToInt32(
                        readerAdherents["ID_UTILISATEUR"]),

                    Nom = readerAdherents["NOM"].ToString(),

                    Prenom = readerAdherents["PRENOM"].ToString()
                });
            }

            readerAdherents.Close();


            // =========================
            // RÉCUPÉRER LES LIVRES DISPONIBLES
            // =========================

            var livres = new List<dynamic>();

            string sqlLivres = @"
                SELECT
                    ID_LIVRE,
                    TITRE,
                    EXEMPLAIRES_DISPONIBLES
                FROM LIVRE
                WHERE EXEMPLAIRES_DISPONIBLES > 0
                ORDER BY TITRE
            ";

            var commandeLivres =
                new OracleCommand(sqlLivres, connection);

            var readerLivres =
                commandeLivres.ExecuteReader();

            while (readerLivres.Read())
            {
                livres.Add(new
                {
                    Id = Convert.ToInt32(
                        readerLivres["ID_LIVRE"]),

                    Titre = readerLivres["TITRE"].ToString(),

                    Disponibles = Convert.ToInt32(
                        readerLivres["EXEMPLAIRES_DISPONIBLES"])
                });
            }

            readerLivres.Close();

            // Fermer la connexion
            connection.Close();


            // =========================
            // ENVOYER LES DONNÉES À LA VUE
            // =========================

            ViewBag.Adherents = adherents;
            ViewBag.Livres = livres;
            var emprunt = new Emprunt
            {
                DateEmprunt = DateTime.Today,
                DateRetourPrevue = DateTime.Today.AddDays(14)
            };


            return View(emprunt);
        }
        // =========================
        // ENREGISTRER UN NOUVEL EMPRUNT
        // =========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Emprunt emprunt)
        {
            // Vérifier les données reçues du formulaire
            if (!ModelState.IsValid)
            {
                ChargerListesCreate();
                return View(emprunt);
            }

            if (emprunt.DateEmprunt.Date > DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(emprunt.DateEmprunt),
                    "La date d'emprunt ne peut pas être dans le futur."
                );
            }

            if (emprunt.DateRetourPrevue.Date <= emprunt.DateEmprunt.Date)
            {
                ModelState.AddModelError(
                    nameof(emprunt.DateRetourPrevue),
                    "La date de retour prévue doit être postérieure à la date d'emprunt."
                );
            }

            if (!ModelState.IsValid)
            {
                ChargerListesCreate();
                return View(emprunt);
            }

            var connection = _factory.GetConnection();
            connection.Open();

            // On utilise une transaction :
            // soit les deux opérations réussissent,
            // soit aucune n'est enregistrée.
            var transaction = connection.BeginTransaction();

            try
            {
                // =========================
                // 1. VÉRIFIER L'ADHÉRENT
                // =========================

                string sqlAdherent = @"
            SELECT COUNT(*)
            FROM UTILISATEUR
            WHERE ID_UTILISATEUR = :id
            AND ROLE = 'ADHERENT'
            AND ETAT = 'ACTIF'
        ";

                var commandeAdherent =
                    new OracleCommand(sqlAdherent, connection);

                commandeAdherent.Transaction = transaction;

                commandeAdherent.Parameters.Add(
                    ":id",
                    OracleDbType.Int32
                ).Value = emprunt.IdUtilisateur;

                int adherentExiste =
                    Convert.ToInt32(commandeAdherent.ExecuteScalar());

                if (adherentExiste == 0)
                {
                    transaction.Rollback();
                    connection.Close();

                    ModelState.AddModelError(
                        "",
                        "L'adhérent sélectionné n'existe pas ou n'est pas actif."
                    );
                    ChargerListesCreate();

                    return View(emprunt);
                }


                // =========================
                // 2. VÉRIFIER LE LIVRE
                // =========================

                string sqlLivre = @"
            SELECT EXEMPLAIRES_DISPONIBLES
            FROM LIVRE
            WHERE ID_LIVRE = :id
        ";

                var commandeLivre =
                    new OracleCommand(sqlLivre, connection);

                commandeLivre.Transaction = transaction;

                commandeLivre.Parameters.Add(
                    ":id",
                    OracleDbType.Int32
                ).Value = emprunt.IdLivre;

                object? resultatLivre =
    commandeLivre.ExecuteScalar();

                if (resultatLivre == null || resultatLivre == DBNull.Value)
                {
                    transaction.Rollback();
                    connection.Close();

                    ModelState.AddModelError(
                        "",
                        "Le livre sélectionné n'existe pas."
                    );
                    ChargerListesCreate();

                    return View(emprunt);
                }

                int exemplairesDisponibles =
                    Convert.ToInt32(resultatLivre);

                if (exemplairesDisponibles <= 0)
                {
                    transaction.Rollback();
                    connection.Close();

                    ModelState.AddModelError(
                        "",
                        "Ce livre n'est plus disponible."
                    );
                    ChargerListesCreate();

                    return View(emprunt);
                }


                // =========================
                // 3. INSÉRER L'EMPRUNT
                // =========================

                string sqlEmprunt = @"
            INSERT INTO EMPRUNT
            (
                ID_EMPRUNT,
                ID_UTILISATEUR,
                ID_LIVRE,
                DATE_EMPRUNT,
                DATE_RETOUR_PREVUE,
                STATUT,
                PENALITE
            )
            VALUES
            (
                SEQ_EMPRUNT.NEXTVAL,
                :idUtilisateur,
                :idLivre,
                :dateEmprunt,
                :dateRetourPrevue,
                'EN_COURS',
                0
            )
        ";

                var commandeEmprunt =
                    new OracleCommand(sqlEmprunt, connection);

                commandeEmprunt.Transaction = transaction;

                commandeEmprunt.Parameters.Add(
                    ":idUtilisateur",
                    OracleDbType.Int32
                ).Value = emprunt.IdUtilisateur;

                commandeEmprunt.Parameters.Add(
                    ":idLivre",
                    OracleDbType.Int32
                ).Value = emprunt.IdLivre;

                commandeEmprunt.Parameters.Add(
                    ":dateEmprunt",
                    OracleDbType.Date
                ).Value = emprunt.DateEmprunt;

                commandeEmprunt.Parameters.Add(
                    ":dateRetourPrevue",
                    OracleDbType.Date
                ).Value = emprunt.DateRetourPrevue;

                commandeEmprunt.ExecuteNonQuery();


                // =========================
                // 4. DIMINUER LE STOCK
                // =========================

                string sqlStock = @"
            UPDATE LIVRE
            SET EXEMPLAIRES_DISPONIBLES =
                EXEMPLAIRES_DISPONIBLES - 1
            WHERE ID_LIVRE = :idLivre
            AND EXEMPLAIRES_DISPONIBLES > 0
        ";

                var commandeStock =
                    new OracleCommand(sqlStock, connection);

                commandeStock.Transaction = transaction;

                commandeStock.Parameters.Add(
                    ":idLivre",
                    OracleDbType.Int32
                ).Value = emprunt.IdLivre;

                int lignesModifiees =
                    commandeStock.ExecuteNonQuery();

                if (lignesModifiees != 1)
                {
                    transaction.Rollback();
                    connection.Close();

                    ModelState.AddModelError(
                        "",
                        "Impossible de mettre à jour la disponibilité du livre."
                    );
                    ChargerListesCreate();

                    return View(emprunt);
                }


                // =========================
                // 5. VALIDER LES MODIFICATIONS
                // =========================

                transaction.Commit();

                connection.Close();

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                transaction.Rollback();
                connection.Close();

                ModelState.AddModelError(
                    "",
                    "Une erreur est survenue lors de l'enregistrement de l'emprunt."
                );
                ChargerListesCreate();
                return View(emprunt);
            }

        }
        // =========================
        // RECHARGER LES LISTES DU FORMULAIRE
        // =========================

        private void ChargerListesCreate()
        {
            var connection = _factory.GetConnection();
            connection.Open();

            // =========================
            // ADHÉRENTS ACTIFS
            // =========================

            var adherents = new List<dynamic>();

            string sqlAdherents = @"
                SELECT ID_UTILISATEUR, NOM, PRENOM
                FROM UTILISATEUR
                WHERE ROLE = 'ADHERENT'
                AND ETAT = 'ACTIF'
                ORDER BY NOM, PRENOM
            ";

            var commandeAdherents =
                new OracleCommand(sqlAdherents, connection);

            var readerAdherents =
                commandeAdherents.ExecuteReader();

            while (readerAdherents.Read())
            {
                adherents.Add(new
                {
                    Id = Convert.ToInt32(
                        readerAdherents["ID_UTILISATEUR"]),

                    Nom = readerAdherents["NOM"].ToString(),

                    Prenom = readerAdherents["PRENOM"].ToString()
                });
            }

            readerAdherents.Close();


            // =========================
            // LIVRES DISPONIBLES
            // =========================

            var livres = new List<dynamic>();

            string sqlLivres = @"
                SELECT
                    ID_LIVRE,
                    TITRE,
                    EXEMPLAIRES_DISPONIBLES
                FROM LIVRE
                WHERE EXEMPLAIRES_DISPONIBLES > 0
                ORDER BY TITRE
            ";

            var commandeLivres =
                new OracleCommand(sqlLivres, connection);

            var readerLivres =
                commandeLivres.ExecuteReader();

            while (readerLivres.Read())
            {
                livres.Add(new
                {
                    Id = Convert.ToInt32(
                        readerLivres["ID_LIVRE"]),

                    Titre = readerLivres["TITRE"].ToString(),

                    Disponibles = Convert.ToInt32(
                        readerLivres["EXEMPLAIRES_DISPONIBLES"])
                });
            }

            readerLivres.Close();

            connection.Close();

            ViewBag.Adherents = adherents;
            ViewBag.Livres = livres;
        }
        private void MettreAJourEmpruntsEnRetard(OracleConnection connection)
        {
            string sql = @"
        UPDATE EMPRUNT
        SET STATUT = 'EN_RETARD'
        WHERE STATUT = 'EN_COURS'
       AND TRUNC(DATE_RETOUR_PREVUE) < TRUNC(SYSDATE)
    ";

            using var commande = new OracleCommand(sql, connection);

            commande.ExecuteNonQuery();
        }
    }
}
    