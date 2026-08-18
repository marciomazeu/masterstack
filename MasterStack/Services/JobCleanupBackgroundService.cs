using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MasterStack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MasterStack.Services
{
    public class JobCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobCleanupBackgroundService> _logger;

        // Intervalo entre as verificações (exemplo: a cada 24 horas)
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        // Tempo limite de retenção dos dados no banco
        private const int RetentionDays = 2;

        public JobCleanupBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<JobCleanupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[JobCleanup] Serviço de limpeza em segundo plano iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[JobCleanup] Erro durante a execução da limpeza de vagas.");
                }

                // Aguarda 24 horas antes de rodar novamente (ou até o app ser encerrado)
                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("[JobCleanup] Serviço de limpeza em segundo plano finalizado.");
        }

        private async Task PerformCleanupAsync(CancellationToken cancellationToken)
        {
            // Criando um escopo para obter o DbContext
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            DateTime expirationThreshold = DateTime.UtcNow.AddDays(-RetentionDays);

            // Deleção via ExecuteDeleteAsync (EF Core 7+) para alta performance
            int deletedRows = await dbContext.JobPostings
                .Where(j => j.FetchedAt < expirationThreshold)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedRows > 0)
            {
                _logger.LogInformation(
                    "[JobCleanup] Limpeza concluída com sucesso. {Count} vagas antigas foram removidas (Anteriores a {Threshold}).",
                    deletedRows, expirationThreshold);
            }
            else
            {
                _logger.LogInformation("[JobCleanup] Nenhuma vaga antiga encontrada para remoção.");
            }
        }
    }
}