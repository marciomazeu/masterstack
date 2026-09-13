using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace MasterStack.Models // Mantido na pasta Models
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration config, ILogger<EmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Tenta ler com a sintaxe Host/Username/Password ou SmtpServer/SmtpUser/SmtpPass
            var smtpHost = _config["EmailSettings:Host"] 
                ?? _config["EmailSettings:SmtpServer"] 
                ?? "email-smtp.us-east-2.amazonaws.com";

            var smtpPortStr = _config["EmailSettings:Port"] ?? _config["EmailSettings:SmtpPort"] ?? "587";
            var smtpPort = int.Parse(smtpPortStr);

            var smtpUser = _config["EmailSettings:Username"] ?? _config["EmailSettings:SmtpUser"];
            var smtpPass = _config["EmailSettings:Password"] ?? _config["EmailSettings:SmtpPass"];

            _logger.LogInformation(">>> SMTP CONNECT: Host={Host}, Port={Port}, UserLength={UserLen}", 
                smtpHost, smtpPort, smtpUser?.Length ?? 0);

          var message = new MimeMessage();

        // Pega o e-mail do remetente da configuração ou usa o verificado na AWS SES como padrão
        var senderEmail = _config["EmailSettings:FromEmail"] 
            ?? _config["EmailSettings:Sender"] 
            ?? "marciomazeu@hotmail.com";

        var senderName = _config["EmailSettings:FromName"] ?? "MasterStack";

        // REMETENTE (Quem envia)
        message.From.Add(new MailboxAddress(senderName, senderEmail));

        // DESTINATÁRIO (Quem recebe - o e-mail digitado no formulário)
        message.To.Add(new MailboxAddress("", email.Trim()));

        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };
            using var client = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                var options = smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

                await client.ConnectAsync(smtpHost, smtpPort, options);
                await client.AuthenticateAsync(smtpUser, smtpPass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(">>> E-MAIL ENVIADO COM SUCESSO PARA: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERRO SMTP]: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("[DETALHE INNER]: {Message}", ex.InnerException.Message);
                }
                throw;
            }
        }
    }
}