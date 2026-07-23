using System;
using System.Collections.Generic;

namespace FraisMission.Models;

public partial class Mission
{
    public int Id { get; set; }

    public string Nom { get; set; } = null!;

    public string Lieu { get; set; } = null!;

    public DateTime DateDebut { get; set; }

    public DateTime DateFin { get; set; }

    public int ManagerId { get; set; }

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual User Manager { get; set; } = null!;
}
