using System;
using System.Threading;
using System.Threading.Tasks;
using MasterStack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MasterStack.Services
{
    public class JobCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobCleanupService> _logger;

        public JobCleanupService(IServiceProvider serviceProvider, ILogger<JobCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[JobCleanup] Serviço de limpeza em segundo plano iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        dbContext.Database.SetCommandTimeout(120);

                        int deletedCount = await dbContext.Database.ExecuteSqlRawAsync(
                            "DELETE FROM \"JobPostings\" WHERE \"IsClosed\" = true AND \"ClosedAt\" < NOW() - INTERVAL '30 days'",
                            stoppingToken
                        );

                        if (deletedCount > 0)
                        {
                            _logger.LogInformation($"[JobCleanup] Sucesso: {deletedCount} vaga(s) removida(s).");
                        }
                    }

                    // O Task.Delay DEVE ficar dentro do try/catch para capturar o cancelamento do stoppingToken
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Captura o cancelamento vindo do Task.Delay ou das operações com o CancellationToken sem quebrar a app
                    _logger.LogInformation("[JobCleanup] Execução finalizada devido ao encerramento da aplicação.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[JobCleanup] Erro inesperado durante a execução da limpeza de vagas.");
                    
                    // Aguarda um tempo menor antes de tentar novamente caso tenha ocorrido um erro no banco (ex: 30 minutos)
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}