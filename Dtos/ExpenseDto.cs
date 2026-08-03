using System;
using System.ComponentModel.DataAnnotations;

namespace FraisMission.Dtos
{
    public class ExpenseCreateDto
    {
        [Required(ErrorMessage = "L'ID de la mission est obligatoire.")]
        [Range(1, int.MaxValue, ErrorMessage = "L'ID de la mission doit être valide.")]
        public int MissionId { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Le montant doit être supérieur à 0.")]
        public decimal Montant { get; set; }

        [Required(ErrorMessage = "La date est obligatoire.")]
        [DateInPast(ErrorMessage = "La date ne peut pas être dans le futur.")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "La catégorie est obligatoire.")]
        [RegularExpression("^(Transport|Logement|Repas)$", ErrorMessage = "La catégorie doit être : Transport, Logement ou Repas.")]
        public string Categorie { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Le commentaire ne doit pas dépasser 500 caractères.")]
        public string? Commentaire { get; set; }
    }

    // Custom Validation Attribute pour la Date
    public class DateInPastAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is DateTime date)
            {
                return date.Date <= DateTime.Now.Date;
            }
            return true;
        }
    }

    public class ExpenseResponseDto
    {
        public int Id { get; set; }
        public int MissionId { get; set; }
        public string MissionNom { get; set; } = string.Empty;

        public string MissionLieu { get; set; } = string.Empty;
        public DateTime MissionDate { get; set; }

        public decimal Montant { get; set; }
        public DateTime Date { get; set; }
        public string Categorie { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public string Commentaire { get; set; } = string.Empty;

        public string? EmployeeNom { get; set; }
        public string? EmployeePrenom { get; set; }
        public string? EmployeeEmail { get; set; }
    }

    public class ReviewDto
    {
        [Required(ErrorMessage = "Le statut est obligatoire.")]
        [RegularExpression("^(Approuve|Rejete|EnAttente)$", ErrorMessage = "Le statut doit être: Approuve, Rejete ou EnAttente.")]
        public string Status { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Le commentaire ne doit pas dépasser 500 caractères.")]
        public string Comment { get; set; } = string.Empty;
    }
}