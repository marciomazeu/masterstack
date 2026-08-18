using MasterStack.ViewModels;

namespace MasterStack.Services.JobProviders
{
    public interface IJobProvider
    {
        string ProviderName { get; }
        Task<List<JobDto>> SearchJobsAsync(JobSearchFilter filter);
    }
}