namespace FraisMission.Dtos
{
    public class ExpenseCreateDto
    {
        public int MissionId { get; set; }
        public decimal Montant { get; set; }
        public DateTime Date { get; set; }
        public string Categorie { get; set; } = string.Empty;
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
        public string Status { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
    }
}