using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FraisMission.Data;
using FraisMission.Models;
using FraisMission.Dtos;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;

namespace FraisMission.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExpensesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ExpensesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. OBTENIR : Liste des frais avec détails complets (pour le "Voir")
        [HttpGet]
        public IActionResult GetMesFrais()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim.Value);

            var frais = _context.Expenses
                .Where(e => e.EmployeeId == userId)
                .Include(e => e.Mission)
                .Include(e => e.Employee) // Important : charger l'utilisateur
                .Select(e => new ExpenseResponseDto
                {
                    Id = e.Id,
                    MissionId = e.MissionId,
                    MissionNom = e.Mission != null ? e.Mission.Nom : string.Empty,
                    MissionLieu = e.Mission != null ? e.Mission.Lieu : string.Empty,
                    MissionDate = e.Mission != null ? e.Mission.DateDebut : DateTime.MinValue,
                    Montant = e.Montant,
                    Date = e.Date,
                    Categorie = e.Categorie,
                    Statut = e.Statut,
                    Commentaire = e.Commentaire ?? string.Empty,
                    // Informations collaborateur pour le modal Voir
                    EmployeeNom = e.Employee != null ? e.Employee.Nom : string.Empty,
                    EmployeePrenom = e.Employee != null ? e.Employee.Prenom : string.Empty,
                    EmployeeEmail = e.Employee != null ? e.Employee.Email : string.Empty
                })
                .ToList();

            return Ok(frais);
        }

        // 2. CRÉATION
        [HttpPost]
        public IActionResult CreateExpense([FromBody] JsonElement data)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            try
            {
                var expense = new Expense
                {
                    EmployeeId = int.Parse(userIdClaim.Value),
                    Statut = "Brouillon",
                    Montant = data.GetProperty("montant").GetDecimal(),
                    Date = data.GetProperty("date").GetDateTime(),
                    Categorie = data.GetProperty("categorie").GetString()!,
                    MissionId = data.GetProperty("missionId").GetInt32(),
                    Commentaire = data.TryGetProperty("commentaire", out var comm) ? comm.GetString() : null
                };

                _context.Expenses.Add(expense);
                _context.SaveChanges();
                return Ok(new { message = "Frais enregistré avec succès !" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Données invalides.", details = ex.Message });
            }
        }

        // 3. MODIFICATION (Version sécurisée avec JsonElement)
        [HttpPut("{id}")]
        public IActionResult Modifier(int id, [FromBody] JsonElement model)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id && e.EmployeeId == GetCurrentUserId());
            if (expense == null) return NotFound();

            if (expense.Statut != "Brouillon" && expense.Statut != "Rejeté")
                return BadRequest("Modification non autorisée : note figée.");

            try
            {
                expense.MissionId = model.GetProperty("missionId").GetInt32();
                expense.Montant = model.GetProperty("montant").GetDecimal();
                expense.Categorie = model.GetProperty("categorie").GetString()!;
                expense.Date = model.GetProperty("date").GetDateTime();
                expense.Commentaire = model.TryGetProperty("commentaire", out var c) ? c.GetString() : null;
                expense.Statut = "Brouillon";

                _context.SaveChanges();
                return Ok(expense);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Erreur de mise à jour", details = ex.Message });
            }
        }

        // 4. SOUMISSION
        [HttpPost("{id}/soumettre")]
        public IActionResult SoumettreExpense(int id)
        {
            var expense = _context.Expenses.Find(id);
            if (expense == null) return NotFound();
            if (expense.Statut != "Brouillon" && expense.Statut != "Rejeté") return BadRequest("Action impossible.");

            expense.Statut = "Soumis";
            _context.SaveChanges();
            return Ok();
        }

        // 5. SUPPRESSION
        [HttpDelete("{id}")]
        public IActionResult Supprimer(int id)
        {
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id && e.EmployeeId == GetCurrentUserId());
            if (expense == null) return NotFound();
            if (expense.Statut != "Brouillon") return BadRequest("Suppression impossible.");

            _context.Expenses.Remove(expense);
            _context.SaveChanges();
            return Ok();
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
        // =====================================================
        // MANAGER : Liste des frais à valider
        // =====================================================


        [Authorize(Roles = "Manager")]
        [HttpGet("validation")]
        public IActionResult GetFraisValidation()
        {
            int managerId = GetCurrentUserId();

            // 1. Récupérer d'abord les données de base depuis la DB
            var frais = _context.Expenses
                .Include(e => e.Employee)
                .Include(e => e.Mission)
                .Include(e => e.Approvals)
                .Where(e =>
                    (e.Statut == "Soumis" || e.Statut == "Approved" || e.Statut == "Rejected") &&
                    e.Mission != null &&
                    e.Mission.ManagerId == managerId
                )
                .AsEnumerable() // <--- CRUCIAL : On passe en mémoire ici
                .Select(e => new
                {
                    id = e.Id,
                    employeeNom = e.Employee.Nom,
                    employeePrenom = e.Employee.Prenom,
                    missionNom = e.Mission.Nom,
                    categorie = e.Categorie,
                    montant = e.Montant,
                    date = e.Date,
                    statut = e.Statut,

                    // Maintenant que nous sommes en mémoire (C#), 
                    // LINQ to Objects gère mieux les valeurs nulles ou limites
                    dernierCommentaire = e.Approvals
                        .OrderByDescending(a => a.ReviewedAt)
                        .Select(a => a.Comment)
                        .FirstOrDefault(),

                    dateAction = e.Approvals
                        .OrderByDescending(a => a.ReviewedAt)
                        .Select(a => (DateTime?)a.ReviewedAt) // Cast pour gérer les nulls
                        .FirstOrDefault()
                })
                .ToList();

            return Ok(frais);
        }


        // =====================================================
        // MANAGER : Voir détail d'une note
        // =====================================================


        [HttpGet("{id}/details")]
        public IActionResult GetDetailsValidation(int id)
        {
            int managerId = GetCurrentUserId();


            var frais = _context.Expenses

                .Include(e => e.Employee)

                .Include(e => e.Mission)
                    .ThenInclude(m => m.Manager)

               .Include(e => e.Approvals)
    .ThenInclude(a => a.ReviewedBy)

                .FirstOrDefault(e =>
                    e.Id == id
                    &&
                    e.Mission.ManagerId == managerId
                );


            if (frais == null)
                return NotFound();



            return Ok(frais);
        }



        // =====================================================
        // APPROUVER UNE NOTE
        // =====================================================

        [Authorize(Roles = "Manager")]
        [HttpPost("{id}/approve")]
        public IActionResult ApproveExpense(int id)
        {

            int managerId = GetCurrentUserId();


            var expense = _context.Expenses

                .Include(e => e.Mission)

                .FirstOrDefault(e => e.Id == id);



            if (expense == null)
                return NotFound();



            if (expense.Mission.ManagerId != managerId)
                return Forbid();



            if (expense.Statut != "Soumis")
                return BadRequest(
                    "Cette note ne peut pas être validée."
                );



            Approval approval = new Approval
            {
                ExpenseId = expense.Id,
                Status = "Approved",
                Comment = string.Empty, // Correction ici : fournir une chaîne vide pour respecter le type non-nullable
                ReviewedBy = _context.Users.Find(managerId), // Correction ici : passer l'objet User, pas l'id
                ReviewedAt = DateTime.UtcNow
            };


            expense.Statut = "Approved";


            _context.Approvals.Add(approval);


            _context.SaveChanges();



            return Ok(new
            {
                message = "Note approuvée avec succès"
            });
        }



        // =====================================================
        // REJETER UNE NOTE
        // =====================================================

        [Authorize(Roles = "Manager")]
        [HttpPost("{id}/reject")]
        public IActionResult RejectExpense(
            int id,
            [FromBody] RejectDto dto)
        {

            int managerId = GetCurrentUserId();



            var expense = _context.Expenses

                .Include(e => e.Mission)

                .FirstOrDefault(e => e.Id == id);



            if (expense == null)
                return NotFound();



            if (expense.Mission.ManagerId != managerId)
                return Forbid();



            if (expense.Statut != "Soumis")
                return BadRequest(
                    "Cette note ne peut pas être rejetée."
                );



            if (string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest(
                    "Le commentaire est obligatoire."
                );



            Approval approval = new Approval
            {
                ExpenseId = expense.Id,

                Status = "Rejected",

                Comment = dto.Comment,

                ReviewedBy = _context.Users.Find(managerId), // Correction ici : on passe l'objet User, pas l'id

                ReviewedAt = DateTime.UtcNow
            };



            expense.Statut = "Rejected";


            _context.Approvals.Add(approval);


            _context.SaveChanges();



            return Ok(new
            {
                message = "Note rejetée avec succès"
            });
        }
        
        [HttpGet("{id}/details-employe")]
        public IActionResult GetDetailsForEmployee(int id)
        {
            var details = _context.Expenses
                .Where(e => e.Id == id)
                .Select(e => new ExpenseDetailsForEmployeeDto
                {
                    Id = e.Id,
                    MissionNom = e.Mission.Nom,
                    Montant = e.Montant,
                    Commentaire = e.Commentaire,
                    Statut = e.Statut,
                    ManagerNom = e.Mission.Manager.Nom,
                    ManagerPrenom = e.Mission.Manager.Prenom,
                    ManagerEmail = e.Mission.Manager.Email,
                    // Récupère le commentaire depuis la table Approvals si le statut est Rejected
                    MotifRejet = e.Statut == "Rejected" ? _context.Approvals.FirstOrDefault(a => a.ExpenseId == e.Id).Comment : null
                }).FirstOrDefault();

            return Ok(details);
        }

        [HttpGet("{id}/details-manager")]
        public IActionResult GetDetailsForManager(int id)
        {
            var details = _context.Expenses
                .Where(e => e.Id == id)
                .Select(e => new ExpenseDetailsForManagerDto
                {
                    Id = e.Id,
                    EmployeNom = e.Employee.Nom,
                    EmployePrenom = e.Employee.Prenom,
                    EmployeEmail = e.Employee.Email,
                    Montant = e.Montant,
                    Commentaire = e.Commentaire
                }).FirstOrDefault();

            return Ok(details);
        }
        [Authorize(Roles = "Manager")]
        [HttpGet("historique")]
        public IActionResult GetHistoriqueApprovals()
        {
            var historique = _context.Approvals
                .OrderByDescending(a => a.ReviewedAt)
                .Select(a => new
                {
                    // Données de la table Approvals
                    dateAction = a.ReviewedAt,
                    statut = a.Status,
                    commentaire = a.Comment,

                    // Données pour identifier le frais concerné
                    missionNom = a.Expense.Mission.Nom,

                    // Données de l'employé concerné
                    employeeNom = a.Expense.Employee.Nom,
                    employeePrenom = a.Expense.Employee.Prenom
                })
                .ToList();

            return Ok(historique);
        }
    }

}