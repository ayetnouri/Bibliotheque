namespace Bibliotheque.Models
{
    public class Categorie
    {
        public int IdCategorie { get; set; }

        public string Nom { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}