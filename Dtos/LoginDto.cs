namespace FraisMission.Dtos
{
    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // 1. Le constructeur vide (OBLIGATOIRE pour .NET / System.Text.Json)
        public LoginDto()
        {
        }

        // 2. Ton constructeur personnalisé
        public LoginDto(string email, string password)
        {
            Email = email;
            Password = password;
        }
    }
}