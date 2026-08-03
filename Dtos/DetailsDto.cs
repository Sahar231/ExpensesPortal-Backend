namespace FraisMission.Dtos
{
    // DTO pour l'Employé (Vue détaillée d'une note de frais)
    public class ExpenseDetailsForEmployeeDto
    {
        public int Id { get; set; }
        public string MissionNom { get; set; }
        public decimal Montant { get; set; }
        public string Commentaire { get; set; }
        public string Statut { get; set; }

        // Info Responsable (Manager)
        public string ManagerNom { get; set; }
        public string ManagerPrenom { get; set; } = string.Empty;
        public string ManagerEmail { get; set; }

        // Commentaire de rejet (si existant)
        public string MotifRejet { get; set; }
    }

}