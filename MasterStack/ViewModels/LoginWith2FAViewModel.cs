using System.ComponentModel.DataAnnotations;

namespace MasterStack.ViewModels
{
    public class LoginWith2FAViewModel
    {
        [Required(ErrorMessage = "O código é obrigatório.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "O código deve ter exatamente 6 dígitos.")]
        [Display(Name = "Código de Verificação")]
        public string TwoFactorCode { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}