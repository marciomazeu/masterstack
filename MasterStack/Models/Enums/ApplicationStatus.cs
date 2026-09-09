namespace MasterStack.Models.Enums
{
    public enum ApplicationStatus
    {
        Submitted = 0,    // Candidatura enviada
        InReview = 1,     // Em análise pelo recrutador
        Interview = 2,    // Agendado para entrevista
        Approved = 3,     // Aprovado
        Rejected = 4      // Rejeitado / Banco de Talentos
    }
}