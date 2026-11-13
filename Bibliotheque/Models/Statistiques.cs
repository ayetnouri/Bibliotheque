namespace Bibliotheque.Models
{
    public class Statistiques
    {
        // Utilisateurs
        public int TotalUtilisateurs { get; set; }
        public int TotalAdherents { get; set; }
        public int TotalBibliothecaires { get; set; }
        public int TotalAdministrateurs { get; set; }

        // Livres
        public int TotalLivres { get; set; }
        public int TotalExemplaires { get; set; }
        public int ExemplairesDisponibles { get; set; }

        // Emprunts
        public int EmpruntsEnCours { get; set; }
        public int EmpruntsRetournes { get; set; }
        public int EmpruntsEnRetard { get; set; }
    }
}