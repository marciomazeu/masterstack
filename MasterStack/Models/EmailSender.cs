using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Net;
using System.Net.Mail;

namespace MasterStack.Services // Ajuste para seu namespace
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

//         public async Task SendEmailAsync(string email, string subject, string htmlMessage)
//         {
//             var smtpHost = _config["EmailSettings:Host"];
//             var smtpPort = int.Parse(_config["EmailSettings:Port"] ?? "587");
//             var smtpUser = _config["EmailSettings:apikey"]; // Aqui vai "apikey"
//             var smtpPass = _config["EmailSettings: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"]; // Aqui vai a sua SG.xxx

//             using (var client = new SmtpClient(smtpHost, smtpPort))
// {
//     // ORDEM IMPORTANTE: 
//     client.UseDefaultCredentials = false; // 1. Desliga o padrão do Windows
//     client.Credentials = new NetworkCredential(smtpUser, smtpPass); // 2. Define API Key
//     client.EnableSsl = true; // 3. Ativa a segurança

//     var from = new MailAddress("marciomazeu@hotmail.com", "MasterStack");
//     var to = new MailAddress(email.Trim());

//     using (var mailMessage = new MailMessage(from, to))
//     {
//         mailMessage.Subject = subject;
//         mailMessage.Body = htmlMessage;
//         mailMessage.IsBodyHtml = true;
        
//         // Garante que o envio aguarde a autenticação
//         await client.SendMailAsync(mailMessage);
//     }
// }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtpHost = _config["EmailSettings:Host"];
        var smtpPort = int.Parse(_config["EmailSettings:Port"] ?? "587");
        var smtpUser = _config["EmailSettings:Username"];
        var smtpPass = _config["EmailSettings:Password"];

        var message = new MimeMessage();
        // O e-mail de "From" DEVE ser o e-mail que você validou no SendGrid (Sender Identity)
        message.From.Add(new MailboxAddress("MasterStack", "marciomazeu@hotmail.com"));
        message.To.Add(new MailboxAddress("", email.Trim()));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            // Aceita qualquer certificado (evita erro de SSL em desenvolvimento no Mac)
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Se porta 465 use SslOnConnect, se 587 use StartTls
            var options = smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await client.ConnectAsync(smtpHost, smtpPort, options);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            
            Console.WriteLine(">>> E-mail enviado com sucesso!");
        }
        catch (Exception ex)
        {
            // Se cair aqui, o erro aparecerá no terminal do VS Code / Visual Studio
            Console.WriteLine($"[ERRO SMTP]: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"[DETALHE]: {ex.InnerException.Message}");
            throw; 
        }
    }
    }
}