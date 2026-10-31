using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// 💡 IMPORTS DOS NAMESPACES DO PROJETO:
using MasterStack.Services;
using MasterStack.Models;
using MasterStack.Models.External;
using MasterStack.ViewModels; // Importa o JobSearchFilter da subpasta External

namespace MasterStack.Services
{
    public class JobSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobSyncBackgroundService> _logger;

        public JobSyncBackgroundService(IServiceProvider serviceProvider, ILogger<JobSyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("JobSyncBackgroundService iniciado com suporte a vagas no Canadá.");

    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var jobAggregator = scope.ServiceProvider.GetService<JobAggregatorService>();
                if (jobAggregator != null)
                {
                    _logger.LogInformation("Iniciando sincronização de vagas no Canadá...");

                    // Busca ampla cobrindo o Canadá inteiro
                    var canadaFilter = new JobSearchFilter 
                    { 
                        Query = "developer", 
                        Location = "Canada", 
                        Page = 1 
                    };

                    await jobAggregator.AggregateJobsAsync(canadaFilter);
                    _logger.LogInformation("Sincronização de vagas do Canadá concluída.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na sincronização de vagas em segundo plano.");
        }

        await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
    }
}
    }
}