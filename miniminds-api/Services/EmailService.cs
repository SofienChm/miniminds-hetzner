using System.Net;
using System.Net.Mail;

namespace DaycareAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendWelcomeEmailAsync(string toEmail, string parentName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                Console.WriteLine($"Starting email send to: {toEmail}");
                
                var smtpHost = _configuration["Email:Host"];
                var smtpPort = int.Parse(_configuration["Email:Port"] ?? "587");
                var fromEmail = _configuration["Email:From"];
                var fromPassword = _configuration["Email:Password"];

                Console.WriteLine($"SMTP Config - Host: {smtpHost}, Port: {smtpPort}, From: {fromEmail}");
                
                if (string.IsNullOrEmpty(fromEmail))
                {
                    throw new ArgumentException("FromEmail configuration is missing or empty");
                }
                if (string.IsNullOrEmpty(toEmail))
                {
                    throw new ArgumentException("Destination email is missing or empty");
                }

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(fromEmail, fromPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, "MiniMinds Daycare"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(new MailAddress(toEmail));

                Console.WriteLine($"Sending email with subject: {subject}");
                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR sending email to {toEmail}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string parentName)
        {
            var frontendUrl = _configuration["Frontend:BaseUrl"];
            var resetLink = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(toEmail)}";
            
            var subject = "Bienvenue chez MiniMinds - Initialisez votre mot de passe";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2c3e50;'>Bienvenue chez MiniMinds Daycare !</h2>
                    <p>Bonjour <strong>{parentName}</strong>,</p>
                    <p>Votre compte a été créé avec succès avec l'adresse email : <strong>{toEmail}</strong></p>
                    
                    <div style='background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <h3 style='color: #e74c3c; margin-top: 0;'>⚠️ Action requise</h3>
                        <p>Pour accéder à votre compte, vous devez d'abord initialiser votre mot de passe :</p>
                        <div style='text-align: center; margin: 20px 0;'>
                            <a href='{resetLink}' style='background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; font-weight: bold;'>Initialiser mon mot de passe</a>
                        </div>
                    </div>
                    
                    <p>Une fois votre mot de passe configuré, vous pourrez vous connecter pour :</p>
                    <ul>
                        <li>Suivre les activités de votre enfant</li>
                        <li>Consulter les messages des éducateurs</li>
                        <li>Gérer vos informations personnelles</li>
                    </ul>
                    
                    <p>Cordialement,<br><strong>L'équipe MiniMinds Daycare</strong></p>
                </div>
            ";
            
            await SendEmailAsync(toEmail, subject, body);
        }
    }
}
