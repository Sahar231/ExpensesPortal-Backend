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
        private readonly IConfiguration _configuration; // Added for JWT

        // Le constructeur reçoit maintenant DbContext ET Configuration
        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("signup")]
        public IActionResult Register([FromBody] RegisterDto dto)
        {
            // Normaliser en amont
            string cleanEmail = dto.Email.Trim().ToLower();
            if (_context.Users.Any(u => u.Email.Trim().ToLower() == cleanEmail))
                return BadRequest(new { message = "Cet email est déjà utilisé." });

            var user = new User { Nom = dto.Nom, Prenom = dto.Prenom, Email = cleanEmail, MotDePasse = BCrypt.Net.BCrypt.HashPassword(dto.Password), Role = dto.Role };
            user.Email = cleanEmail;

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

        [HttpPost("login")] // URL: https://localhost:7212/api/auth/login
        public IActionResult Login([FromBody] LoginDto dto)
        {
            string cleanEmail = dto.Email.Trim().ToLower();

            // 1. Vérifier si l'utilisateur existe
            var user = _context.Users.FirstOrDefault(u => u.Email.Trim().ToLower() == cleanEmail);
            if (user == null)
            {
                return BadRequest(new { message = "Email inncorrect." });
            }

            // 2. Vérifier le mot de passe haché
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.MotDePasse);
            if (!isPasswordValid)
            {
                return BadRequest(new { message = " mot de passe incorrect." });
            }

            // 3. Génération du JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
{
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Nom),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
}),
                Expires = DateTime.UtcNow.AddHours(2), // Expiration automatique après 2h
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 4. Réponse envoyée à Angular
            return Ok(new
            {
                message = "Connexion réussie !",
                token = tokenString, // Le fameux précieux sésame 🔑
                user = new { user.Nom, user.Prenom, user.Email, user.Role }
            });
        }
    }
}