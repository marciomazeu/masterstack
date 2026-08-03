using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MasterStack.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterStack.Services
{
    public class AffiliateExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AffiliateExpirationService> _logger;

        public AffiliateExpirationService(IServiceProvider serviceProvider, ILogger<AffiliateExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de Verificação de Links de Afiliados Iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var now = DateTime.UtcNow;

                        // Busca produtos ativos cuja data de expiração já passou
                        var expiredProducts = context.AffiliateProducts
                            .Where(p => p.IsActive && p.ExpirationDate.HasValue && p.ExpirationDate.Value <= now)
                            .ToList();

                        if (expiredProducts.Any())
                        {
                            foreach (var product in expiredProducts)
                            {
                                product.IsActive = false; // Desativa automaticamente
                                product.UpdatedAt = now;
                                _logger.LogWarning($"Link de Afiliado Expirado e Desativado: ID {product.Id} - Código: {product.ProductCode}");
                            }

                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar verificação de expiração de afiliados.");
                }

                // Aguarda 24 horas até a próxima verificação
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}