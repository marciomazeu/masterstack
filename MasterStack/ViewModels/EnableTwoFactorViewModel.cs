using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class EnableTwoFactorViewModel
    {
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;

        [Required(ErrorMessage = "O código de verificação é obrigatório.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "O código deve ter exatamente 6 dígitos.")]
        public string VerificationCode { get; set; } = string.Empty;
    }
}