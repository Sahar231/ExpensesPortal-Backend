using FraisMission.Data;
using FraisMission.Dtos;
using FraisMission.Models;
using FraisMission.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FraisMission.Controllers

{


    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExpensesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public ExpensesController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // 1. OBTENIR : Mes frais
        [HttpGet]
        public IActionResult GetMesFrais()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var frais = _context.Expenses
                .Where(e => e.EmployeeId == userId)
                .Include(e => e.Mission)
                .Include(e => e.Employee)
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
                    EmployeeNom = e.Employee != null ? e.Employee.Nom : string.Empty,
                    EmployeePrenom = e.Employee != null ? e.Employee.Prenom : string.Empty,
                    EmployeeEmail = e.Employee != null ? e.Employee.Email : string.Empty
                })
                .ToList();

            return Ok(frais);
        }

        // 2. CRÉATION
        [HttpPost]
        public IActionResult CreateExpense([FromBody] ExpenseCreateDto dto)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var missionExists = _context.Missions.Any(m => m.Id == dto.MissionId);
            if (!missionExists)
            {
                return BadRequest(new { message = "La mission spécifiée n'existe pas." });
            }

            try
            {
                var expense = new Expense
                {
                    EmployeeId = userId,
                    Statut = "Brouillon",
                    Montant = dto.Montant,
                    Date = dto.Date,
                    Categorie = dto.Categorie,
                    MissionId = dto.MissionId
                };

                _context.Expenses.Add(expense);
                _context.SaveChanges();
                return Ok(new { message = "Frais enregistré avec succès !" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Erreur lors de l'enregistrement.", details = ex.Message });
            }
        }

        
        [HttpPut("{id}")]
        public IActionResult Modifier(int id, [FromBody] ExpenseCreateDto dto)
        {
            int userId = GetCurrentUserId();

            // 1. Vérification de l'existence de la note
            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);
            if (expense == null)
                return NotFound(new { message = "Note de frais introuvable." });

            // 2. Vérification du propriétaire
            if (expense.EmployeeId != userId)
                return Forbid();

            // 3. Vérification de l'existence de la Mission en BDD
            var missionExists = _context.Missions.Any(m => m.Id == dto.MissionId);
            if (!missionExists)
            {
                return BadRequest(new { message = "La mission spécifiée n'existe pas." });
            }

            try
            {
                // Mise à jour des valeurs
                expense.MissionId = dto.MissionId;
                expense.Montant = dto.Montant;
                expense.Categorie = dto.Categorie;
                expense.Date = dto.Date;
                expense.Commentaire = dto.Commentaire;

                if (expense.Statut == "Rejected")
                {
                    expense.Statut = "Soumis";
                }
               

                _context.SaveChanges();

                return Ok(new { message = "Note de frais modifiée  avec succès !" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Erreur de mise à jour", details = ex.Message });
            }
        }

        [HttpPost("{id}/soumettre")]
        public async Task<IActionResult> SoumettreExpense(int id)
        {
            int userId = GetCurrentUserId();

            var expense = _context.Expenses
                .Include(e => e.Mission)
                .Include(e => e.Employee)
                .FirstOrDefault(e => e.Id == id);

            if (expense == null)
                return NotFound(new { message = "Note de frais introuvable." });

            if (expense.EmployeeId != userId)
                return Forbid();

            if (expense.Statut != "Brouillon" && expense.Statut != "Rejected")
                return BadRequest(new { message = "Action impossible : la note ne peut plus être soumise." });

            bool estResoumise = expense.Statut == "Rejected";
            expense.Statut = "Soumis";

            _context.SaveChanges();

            if (expense.Mission == null)
            {
                return Ok(new
                {
                    status = "WARN",
                    message = "Note soumise, mais PAS DE MAIL : La note n'a pas de Mission liée !"
                });
            }

            var manager = _context.Users.FirstOrDefault(u => u.Id == expense.Mission.ManagerId);

            if (manager == null || string.IsNullOrEmpty(manager.Email))
            {
                return Ok(new
                {
                    status = "WARN",
                    message = $"Note soumise, mais PAS DE MAIL : Aucun manager trouvé avec ManagerId = {expense.Mission.ManagerId}"
                });
            }

            string sujet = estResoumise
                ? "Une note rejetée a été corrigée et resoumise"
                : "Nouvelle note de frais à valider";

            string empNom = expense.Employee != null ? $"{expense.Employee.Nom} {expense.Employee.Prenom}" : "Un employé";

            string message = $@"
        <h3>Bonjour {manager.Nom},</h3>
        <p>L'employé <b>{empNom}</b> a {(estResoumise ? "corrigé et resoumis" : "soumis")} une note de frais.</p>
        <p><b>Mission :</b> {expense.Mission.Nom}</p>
        <p><b>Montant :</b> {expense.Montant} DT</p>
        <p>Veuillez vous connecter pour la valider.</p>";

            try
            {
                await _emailService.SendEmailAsync(manager.Email, sujet, message);

                return Ok(new
                {
                    status = "SUCCESS",
                    message = $"Note soumise et email envoyé avec SUCCÈS à {manager.Email} !"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    status = "ERROR_MAIL",
                    message = "Note soumise en BDD, mais l'envoi du mail a échoué.",
                    erreurMailDetail = ex.Message
                });
            }
        }

        // 5. SUPPRESSION
        [HttpDelete("{id}")]
        public IActionResult Supprimer(int id)
        {
            int userId = GetCurrentUserId();

            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);
            if (expense == null)
                return NotFound(new { message = "Note de frais introuvable." });

            if (expense.EmployeeId != userId)
                return Forbid();

            if (expense.Statut != "Brouillon")
                return BadRequest(new { message = "Suppression impossible." });

            _context.Expenses.Remove(expense);
            _context.SaveChanges();
            return Ok();
        }

        // 6. DÉTAILS EMPLOYÉ
        [HttpGet("{id}/details-employe")]
        public IActionResult GetDetailsForEmployee(int id)
        {
            int userId = GetCurrentUserId();

            var expense = _context.Expenses
                .Include(e => e.Mission)
                    .ThenInclude(m => m.Manager)
                .FirstOrDefault(e => e.Id == id);

            if (expense == null)
                return NotFound(new { message = "Note introuvable." });

            if (expense.EmployeeId != userId)
                return Forbid();

            var details = new ExpenseDetailsForEmployeeDto
            {
                Id = expense.Id,
                MissionNom = expense.Mission?.Nom ?? string.Empty,
                Montant = expense.Montant,
                Commentaire = expense.Commentaire,
                Statut = expense.Statut,
                ManagerNom = expense.Mission?.Manager?.Nom ?? string.Empty,
                ManagerPrenom = expense.Mission?.Manager?.Prenom ?? string.Empty,
                ManagerEmail = expense.Mission?.Manager?.Email ?? string.Empty,
                MotifRejet = expense.Statut == "Rejected"
                    ? _context.Approvals.FirstOrDefault(a => a.ExpenseId == expense.Id && a.Status == "Rejected")?.Comment
                    : null
            };

            return Ok(details);
        }

        // =====================================================
        // SECTION MANAGER
        // =====================================================

        [Authorize(Roles = "Manager")]
        [HttpGet("validation")]
        public IActionResult GetFraisValidation()
        {
            int managerId = GetCurrentUserId();

            var frais = _context.Expenses
                .Include(e => e.Employee)
                .Include(e => e.Mission)
                .Include(e => e.Approvals)
                .Where(e =>
                    (e.Statut == "Soumis" || e.Statut == "Approved" || e.Statut == "Rejected") &&
                    e.Mission != null &&
                    e.Mission.ManagerId == managerId
                )
                .AsEnumerable()
                .Select(e => new
                {
                    id = e.Id,
                    employeeNom = e.Employee?.Nom,
                    employeePrenom = e.Employee?.Prenom,
                    missionNom = e.Mission?.Nom,
                    categorie = e.Categorie,
                    montant = e.Montant,
                    date = e.Date,
                    statut = e.Statut,
                    commentaire = e.Commentaire,
                    estResoumise = e.Statut == "Soumis" && e.Approvals.Any(a => a.Status == "Rejected"),
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

        [Authorize(Roles = "Manager")]
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveExpense(int id)
        {
            int managerId = GetCurrentUserId();

            var expense = _context.Expenses
                .Include(e => e.Mission)
                .Include(e => e.Employee)
                .FirstOrDefault(e => e.Id == id);

            if (expense == null)
                return NotFound(new { message = "Note introuvable." });

            if (expense.Mission == null || expense.Mission.ManagerId != managerId)
                return Forbid();

            if (expense.Statut != "Soumis")
                return BadRequest(new { message = "Cette note ne peut pas être validée." });

            var approval = new Approval
            {
                ExpenseId = expense.Id,
                Status = "Approved",
                ReviewedBy = _context.Users.Find(managerId),
                ReviewedAt = DateTime.UtcNow
            };

            expense.Statut = "Approved";
            _context.Approvals.Add(approval);
            _context.SaveChanges();

            if (expense.Employee != null && !string.IsNullOrEmpty(expense.Employee.Email))
            {
                string sujet = "Votre note de frais a été approuvée !";
                string message = $@"
            <h3>Bonjour {expense.Employee.Nom} {expense.Employee.Prenom},</h3>
            <p>Bonne nouvelle ! Votre note de frais pour la mission <b>{expense.Mission.Nom}</b> a été <b>approuvée</b>.</p>
            <ul>
                <li><b>Montant :</b> {expense.Montant} DT</li>
                <li><b>Catégorie :</b> {expense.Categorie}</li>
                <li><b>Date de frais :</b> {expense.Date:dd/MM/yyyy}</li>
            </ul>
            <p>Le paiement/remboursement sera traité prochainement.</p>";

                try
                {
                    await _emailService.SendEmailAsync(expense.Employee.Email, sujet, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL API ERROR] : {ex.Message}");
                }
            }

            return Ok(new { message = "Note approuvée avec succès et employé notifié." });
        }

        [Authorize(Roles = "Manager")]
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectExpense(int id, [FromBody] ReviewDto dto)
        {
            int managerId = GetCurrentUserId();

            var expense = _context.Expenses
                .Include(e => e.Mission)
                .Include(e => e.Employee)
                .FirstOrDefault(e => e.Id == id);

            if (expense == null)
                return NotFound(new { message = "Note introuvable." });

            if (expense.Mission == null || expense.Mission.ManagerId != managerId)
                return Forbid();

            if (expense.Statut != "Soumis")
                return BadRequest(new { message = "Cette note ne peut pas être rejetée." });

            if (string.IsNullOrWhiteSpace(dto?.Comment))
                return BadRequest(new { message = "Le motif du rejet est obligatoire." });

            var approval = new Approval
            {
                ExpenseId = expense.Id,
                Status = "Rejected",
                Comment = dto.Comment,
                ReviewedBy = _context.Users.Find(managerId),
                ReviewedAt = DateTime.UtcNow
            };

            expense.Statut = "Rejected";
            _context.Approvals.Add(approval);
            _context.SaveChanges();

            if (expense.Employee != null && !string.IsNullOrEmpty(expense.Employee.Email))
            {
                string sujet = "Votre note de frais a été rejetée";
                string message = $@"
            <h3>Bonjour {expense.Employee.Nom} {expense.Employee.Prenom},</h3>
            <p>Votre note de frais pour la mission <b>{expense.Mission.Nom}</b> a été <b>rejetée</b> par votre manager.</p>
            <p><b>Motif du rejet :</b></p>
            <blockquote style='background-color: #f8d7da; color: #721c24; padding: 10px; border-left: 5px solid #f5c6cb;'>
                {dto.Comment}
            </blockquote>
            <p>Vous pouvez modifier votre note sur l'application et la soumettre à nouveau.</p>";

                try
                {
                    await _emailService.SendEmailAsync(expense.Employee.Email, sujet, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL API ERROR] : {ex.Message}");
                }
            }

            return Ok(new { message = "Note rejetée avec succès et employé notifié." });
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

            var now = DateTime.Now;
            var debutDuMois = new DateTime(now.Year, now.Month, 1);
            var finDuMois = debutDuMois.AddMonths(1).AddDays(-1);

            var stats = new
            {
                isManager = isManager,
                totalFrais = query.Count(),
                enAttente = query.Count(e => e.Statut == "Soumis" || e.Statut == "En attente"),
                montantTotalApprouve = query.Where(e => e.Statut == "Approved" || e.Statut == "Approuvé").Sum(e => (decimal?)e.Montant) ?? 0,
                totalRejete = query.Count(e => e.Statut == "Rejected" || e.Statut == "Rejeté"),
                montantCeMois = query.Where(e => e.Date >= debutDuMois && e.Date <= finDuMois && (e.Statut == "Approved" || e.Statut == "Approuvé"))
                        .Sum(e => (decimal?)e.Montant) ?? 0,

                repartitionStatuts = query.GroupBy(e => e.Statut)
                           .Select(g => new { label = g.Key, nombre = g.Count() })
                           .ToList(),

                repartitionMissions = query.Where(e => e.Mission != null)
                           .GroupBy(e => new { e.MissionId, e.Mission.Nom })
                           .Select(g => new
                           {
                               missionId = g.Key.MissionId,
                               label = g.Key.Nom,
                               nombreFrais = g.Count(),
                               montantTotal = g.Sum(e => e.Montant),
                               montantApprouve = g.Where(e => e.Statut == "Approved" || e.Statut == "Approuvé").Sum(e => (decimal?)e.Montant) ?? 0,
                               montantEnAttente = g.Where(e => e.Statut == "Soumis" || e.Statut == "En attente").Sum(e => (decimal?)e.Montant) ?? 0,
                               montantRejete = g.Where(e => e.Statut == "Rejected" || e.Statut == "Rejeté").Sum(e => (decimal?)e.Montant) ?? 0
                           })
                           .ToList(),

                repartitionEmployes = query.Where(e => e.Employee != null)
                           .GroupBy(e => new { e.EmployeeId, e.Employee.Nom, e.Employee.Prenom })
                           .Select(g => new
                           {
                               label = g.Key.Nom + " " + g.Key.Prenom,
                               nombre = g.Count(),
                               montantTotal = g.Sum(e => e.Montant)
                           })
                           .ToList()
            };

            return Ok(stats);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }
    }
}
