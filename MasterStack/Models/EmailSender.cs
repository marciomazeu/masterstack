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