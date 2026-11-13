namespace Bibliotheque.Models
{
    public class Livre
    {
        public int IdLivre { get; set; }

        public string ISBN { get; set; } = string.Empty;

        public string Titre { get; set; } = string.Empty;

        public string? Resume { get; set; }

        public int? NombrePages { get; set; }

        public int? AnneePublication { get; set; }

        public int? NombreExemplaires { get; set; }

        public int? ExemplairesDisponibles { get; set; }

        public string? Emplacement { get; set; }

        public int IdCategorie { get; set; }

        public int IdLangue { get; set; }

        public int IdEditeur { get; set; }
    }
}