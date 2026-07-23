using FraisMission.Data;
using FraisMission.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FraisMission.Controllers
{
    [Authorize] // Accessible à TOUT utilisateur connecté (Employé ET Manager)
    [ApiController]
    [Route("api/[controller]")]
    public class MissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Missions
        // Permet à tout le monde (Employé pour créer un frais, Manager pour son dashboard) 
        // de lister les missions disponibles en BDD
        [HttpGet]
        public async Task<IActionResult> GetAllMissions()
        {
            var missions = await _context.Missions
                .Select(m => new
                {
                    m.Id,
                    m.Nom,
                    m.Lieu
                })
                .ToListAsync();

            return Ok(missions);
        }

        // GET: api/Missions/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMission(int id)
        {
            var mission = await _context.Missions
                .Select(m => new { m.Id, m.Nom, m.Lieu, m.DateDebut, m.DateFin })
                .FirstOrDefaultAsync(m => m.Id == id);

            if (mission == null)
                return NotFound("Mission introuvable.");

            return Ok(mission);
        }
    }
}