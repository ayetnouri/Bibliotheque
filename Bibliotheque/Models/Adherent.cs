namespace Bibliotheque.Models
{
    public class Adherent
    {
        public int IdUtilisateur { get; set; }

        // Informations venant de UTILISATEUR
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Etat { get; set; } = string.Empty;
        public DateTime? DateCreation { get; set; }

        // Informations venant de ADHERENT
        public string? Telephone { get; set; }
        public string? Adresse { get; set; }
        public DateTime? DateInscription { get; set; }
        public string? Statut { get; set; }
    }
}