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
using FraisMission.Services;

namespace FraisMission.Controllers

{
    

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExpensesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public ExpensesController(
            ApplicationDbContext context,
            EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            if (expense.Statut != "Brouillon" && expense.Statut != "Rejected")
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

        [HttpPost("{id}/soumettre")]
        public async Task<IActionResult> SoumettreExpense(int id)
        {
            var expense = _context.Expenses
                .Include(e => e.Mission)
                .Include(e => e.Employee)
                .FirstOrDefault(e => e.Id == id);

            if (expense == null)
                return NotFound();

            if (expense.Statut != "Brouillon" && expense.Statut != "Rejected")
                return BadRequest("Action impossible.");


            bool estResoumise = expense.Statut == "Rejected";


            expense.Statut = "Soumis";

            _context.SaveChanges();


            // Récupérer le manager
            var manager = _context.Users
                .FirstOrDefault(u => u.Id == expense.Mission.ManagerId);


            if (manager != null)
            {
                string sujet;
                string message;


                if (estResoumise)
                {
                    sujet = "Une note rejetée a été corrigée et soumise de nouveau";

                    message = $@"
            <h3>Bonjour {manager.Nom},</h3>

            <p>
            L'employé <b>{expense.Employee.Nom} {expense.Employee.Prenom}</b>
            a corrigé une note rejetée et l'a soumise de nouveau.
            </p>

            <p>
            <b>Mission :</b> {expense.Mission.Nom}
            </p>

            <p>
            <b>Montant :</b> {expense.Montant} DT
            </p>

            <p>
            Veuillez la vérifier à nouveau.
            </p>";
                }
                else
                {
                    sujet = "Nouvelle note de frais à valider";

                    message = $@"
            <h3>Bonjour {manager.Nom},</h3>

            <p>
            Une nouvelle note de frais a été soumise par 
            <b>{expense.Employee.Nom} {expense.Employee.Prenom}</b>.
            </p>

            <p>
            <b>Mission :</b> {expense.Mission.Nom}
            </p>

            <p>
            <b>Montant :</b> {expense.Montant} DT
            </p>

            <p>
            Veuillez vous connecter pour la valider.
            </p>";
                }


                await _emailService.SendEmailAsync(
                    manager.Email,
                    sujet,
                    message
                );
            }


            return Ok(new
            {
                message = "Note soumise avec succès"
            });
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
                      commentaire = e.Commentaire,
                      estResoumise = e.Statut == "Soumis"
        && e.Approvals.Any(a => a.Status == "Rejected"),

                      dernierCommentaire = e.Approvals
        .OrderByDescending(a => a.ReviewedAt)
        .Select(a => a.Comment)
        .FirstOrDefault(),

                      dateAction = e.Approvals
        .OrderByDescending(a => a.ReviewedAt)
        .Select(a => (DateTime?)a.ReviewedAt)
        .FirstOrDefault()
                  })
                .ToList();

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



        [HttpGet("Statistiques")]
        public IActionResult GetStatistiques()
        {
            int userId = GetCurrentUserId();


            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!roles.Any())
            {
                roles = User.FindAll("role").Select(c => c.Value).ToList();
            }

            bool isManager = roles.Contains("Manager");

            IQueryable<Expense> query = _context.Expenses;

            if (isManager)
            {

                query = query.Where(e => e.Mission != null && e.Mission.ManagerId == userId);
            }
            else
            {

                query = query.Where(e => e.EmployeeId == userId);
            }

            var stats = new
            {
                isManager = isManager,
                totalFrais = query.Count(),
                enAttente = query.Count(e => e.Statut == "Soumis" || e.Statut == "En attente"),
                montantTotalApprouve = query.Where(e => e.Statut == "Approved" || e.Statut == "Approuvé").Sum(e => (decimal?)e.Montant) ?? 0,

                repartitionStatuts = query.GroupBy(e => e.Statut)
                                          .Select(g => new { label = g.Key, nombre = g.Count() })
                                          .ToList(),


                repartitionMissions = query.Where(e => e.Mission != null)
                                           .GroupBy(e => e.Mission.Nom)
                                           .Select(g => new { label = "Mission: " + g.Key, nombre = g.Count() })
                                           .ToList(),

                repartitionEmployes = query.Where(e => e.Employee != null)
                                           .GroupBy(e => e.Employee.Nom + " " + e.Employee.Prenom)
                                           .Select(g => new { label = g.Key, nombre = g.Count() })
                                           .ToList()
            };

            return Ok(stats);
        }

        
    }
}
