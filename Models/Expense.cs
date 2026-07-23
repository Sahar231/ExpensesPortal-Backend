using System;
using System.Collections.Generic;

namespace FraisMission.Models;

public partial class Expense
{
    public int Id { get; set; }

    public decimal Montant { get; set; }

    public DateTime Date { get; set; }

    public string Categorie { get; set; } = null!;

    public string Statut { get; set; } = null!;

    public string? Commentaire { get; set; }

    public int EmployeeId { get; set; }

    public int MissionId { get; set; }

    public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public virtual User Employee { get; set; } = null!;

    public virtual Mission Mission { get; set; } = null!;
}
