using Microsoft.AspNetCore.Mvc;
using FraisMission.Data;
using FraisMission.Models;
using FraisMission.Dtos;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FraisMission.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("signup")]
        public IActionResult Register([FromBody] RegisterDto dto)
        {
            // Normalisation mta3 l-email
            string cleanEmail = dto.Email.Trim().ToLower();

            // Optimisation SQL: mghir Trim()/ToLower() fi l-Linq query
            if (_context.Users.Any(u => u.Email == cleanEmail))
                return BadRequest(new { message = "Cet email est déjà utilisé." });

            var user = new User
            {
                Nom = dto.Nom,
                Prenom = dto.Prenom,
                Email = cleanEmail,
                MotDePasse = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role
            };

            _context.Users.Add(user);
            try
            {
                _context.SaveChanges();
                return Ok(new { message = "Inscription réussie !" });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                if (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
                    sqlEx.Number == 2627)
                {
                    return BadRequest(new { message = "Cet email est déjà utilisé." });
                }
                throw;
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto dto)
        {
            // 1. Clean el-email f C#
            string cleanEmail = dto.Email?.Trim().ToLower() ?? "";

            // 2. Recherche rapide fi SQL (Utilise l'index de la base de données)
            var user = _context.Users.FirstOrDefault(u => u.Email == cleanEmail);

            // Message unifié si l'email n'existe pas
            if (user == null)
            {
                return BadRequest(new { message = "Email ou mot de passe incorrect." });
            }

            // 3. Vérification du mot de passe
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.MotDePasse);

            // Même message unifié si le mot de passe est faux
            if (!isPasswordValid)
            {
                return BadRequest(new { message = "Email ou mot de passe incorrect." });
            }

            // 4. Génération du JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Nom ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "")
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 5. Réponse envoyée à Angular
            return Ok(new
            {
                message = "Connexion réussie !",
                token = tokenString,
                user = new { user.Nom, user.Prenom, user.Email, user.Role }
            });
        }
    }
}