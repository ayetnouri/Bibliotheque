using System;
using System.ComponentModel.DataAnnotations;

namespace Bibliotheque.Models
{
    public class Emprunt
    {
        public int IdEmprunt { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Veuillez sélectionner un adhérent.")]
        public int IdUtilisateur { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Veuillez sélectionner un livre.")]
        public int IdLivre { get; set; }

        [Required(ErrorMessage = "La date d'emprunt est obligatoire.")]
        public DateTime DateEmprunt { get; set; }

        [Required(ErrorMessage = "La date de retour prévue est obligatoire.")]
        public DateTime DateRetourPrevue { get; set; }

        public DateTime? DateRetourReelle { get; set; }

        public string Statut { get; set; } = string.Empty;

        public decimal? Penalite { get; set; }

        // Informations supplémentaires pour l'affichage
        public string? NomAdherent { get; set; }

        public string? TitreLivre { get; set; }
    }
}