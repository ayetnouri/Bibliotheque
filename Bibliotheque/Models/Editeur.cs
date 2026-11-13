namespace Bibliotheque.Models
{
    public class Editeur
    {
        public int IdEditeur { get; set; }

        public string Nom { get; set; } = string.Empty;

        public string? Pays { get; set; }

        public string? Email { get; set; }

        public string? Telephone { get; set; }

        public string? SiteWeb { get; set; }
    }
}
