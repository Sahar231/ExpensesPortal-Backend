namespace FraisMission.Dtos
{
    public class ApprovalHistoryDto
    {
        public DateTime DateAction { get; set; }
        public string Statut { get; set; }
        public string Commentaire { get; set; }
        public string ValideurNom { get; set; }
        public string ValideurPrenom { get; set; }
    }
}
