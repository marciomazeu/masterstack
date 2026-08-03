using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MasterStack.Data;
using MasterStack.Models;

namespace MasterStack.Services
{
    public interface IAffiliateRenderService
    {
        Task<string> ParseAffiliateShortcodesAsync(string htmlContent, string culture);
    }

    public class AffiliateRenderService : IAffiliateRenderService
    {
        private readonly ApplicationDbContext _context;

        public AffiliateRenderService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> ParseAffiliateShortcodesAsync(string htmlContent, string culture)
        {
            if (string.IsNullOrEmpty(htmlContent)) return string.Empty;

            // Regex para identificar a tag [[product:codigo-do-produto]]
            var regex = new Regex(@"\[\[product:(.*?)\]\]", RegexOptions.IgnoreCase);
            var matches = regex.Matches(htmlContent);

            if (!matches.Any()) return htmlContent;

            var currentCulture = culture.ToLower();

            foreach (Match match in matches)
            {
                var productCode = match.Groups[1].Value.Trim();
                
                // Busca o produto ativo e não expirado
                var product = await _context.AffiliateProducts
                    .FirstOrDefaultAsync(p => p.ProductCode == productCode && p.IsActive);

                if (product != null)
                {
                    // Lógica para selecionar o idioma correto com base na Culture
                    string title = currentCulture.StartsWith("pt") ? (string.IsNullOrEmpty(product.Title_PT) ? product.Title_EN : product.Title_PT)
                                 : currentCulture.StartsWith("fr") ? (string.IsNullOrEmpty(product.Title_FR) ? product.Title_EN : product.Title_FR)
                                 : product.Title_EN;

                    string description = currentCulture.StartsWith("pt") ? (string.IsNullOrEmpty(product.Description_PT) ? product.Description_EN : product.Description_PT)
                                       : currentCulture.StartsWith("fr") ? (string.IsNullOrEmpty(product.Description_FR) ? product.Description_EN : product.Description_FR)
                                       : product.Description_EN;

                    string targetUrl = currentCulture.StartsWith("pt") ? (string.IsNullOrEmpty(product.TargetUrl_PT) ? product.TargetUrl_EN : product.TargetUrl_PT)
                                     : currentCulture.StartsWith("fr") ? (string.IsNullOrEmpty(product.TargetUrl_FR) ? product.TargetUrl_EN : product.TargetUrl_FR)
                                     : product.TargetUrl_EN;

                    string buttonText = currentCulture.StartsWith("pt") ? "Ver Oferta na " + product.Network
                                      : currentCulture.StartsWith("fr") ? "Voir l'offre sur " + product.Network
                                      : "View Deal on " + product.Network;

                    // Gera o HTML do Card de Afiliado
                    string cardHtml = $@"
                        <div class=""card my-4 border-0 shadow-sm bg-light text-dark rounded-3 overflow-hidden"">
                            <div class=""row g-0 align-items-center"">
                                {(string.IsNullOrEmpty(product.ImageUrl) ? "" : $@"
                                <div class=""col-md-4 text-center p-3 bg-white"">
                                    <img src=""{product.ImageUrl}"" class=""img-fluid rounded"" alt=""{title}"" style=""max-height: 160px; object-fit: contain;"">
                                </div>")}
                                <div class=""col-md-{(string.IsNullOrEmpty(product.ImageUrl) ? "12" : "8")}"">
                                    <div class=""card-body p-3"">
                                        <span class=""badge bg-warning text-dark mb-2""><i class=""fas fa-star me-1""></i> {product.Network}</span>
                                        <h6 class=""card-title fw-bold mb-1"">{title}</h6>
                                        <p class=""card-text small text-muted mb-2"">{description}</p>
                                        <div class=""d-flex align-items-center justify-content-between mt-2"">
                                            {(product.Price.HasValue ? $"<span class=\"fw-bold text-success fs-5\">{product.Currency} ${product.Price:F2}</span>" : "<span></span>")}
                                            <a href=""{targetUrl}"" target=""_blank"" rel=""nofollow sponsored"" class=""btn btn-sm btn-primary fw-bold"">
                                                {buttonText} <i class=""fas fa-external-link-alt ms-1""></i>
                                            </a>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>";

                    htmlContent = htmlContent.Replace(match.Value, cardHtml);
                }
                else
                {
                    // Se o produto expirou ou não existe, remove a tag para não quebrar a leitura do leitor
                    htmlContent = htmlContent.Replace(match.Value, string.Empty);
                }
            }

            return htmlContent;
        }
    }
}