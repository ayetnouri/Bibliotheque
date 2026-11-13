namespace Bibliotheque.Models
{
    public class Utilisateur
    {
        public int IdUtilisateur { get; set; }

        public string Nom { get; set; } = string.Empty;

        public string Prenom { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Etat { get; set; } = string.Empty;

        public DateTime? DateCreation { get; set; }
    }
}