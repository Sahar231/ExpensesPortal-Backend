using System;
using System.Collections.Generic;

namespace FraisMission.Models;

public partial class Approval
{
    public int Id { get; set; }

    public string Status { get; set; } = null!;

    public string Comment { get; set; } = null!;

    public DateTime ReviewedAt { get; set; }

    public int ExpenseId { get; set; }

    public int ReviewedById { get; set; }

    public virtual Expense Expense { get; set; } = null!;

    public virtual User ReviewedBy { get; set; } = null!;
}
