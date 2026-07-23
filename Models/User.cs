using System;
using System.Collections.Generic;

namespace FraisMission.Models;

public partial class User
{
    public int Id { get; set; }

    public string Nom { get; set; } = null!;

    public string Prenom { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string MotDePasse { get; set; } = null!;

    public string Role { get; set; } = null!;

    public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
}
