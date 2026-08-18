using System.Text;
using System.Text.RegularExpressions;
using MasterStack.ViewModels;
using UglyToad.PdfPig;

namespace MasterStack.Services
{
    public class ResumeParserService
    {
        public ResumeViewModel ParsePdf(IFormFile file)
        {
            string text = ExtractTextFromPdf(file);
            
            var model = new ResumeViewModel();

            // 1. Extrai Habilidades (busca por palavras-chave como "Habilidades" ou "Skills")
            model.SkillsCsv = ExtractSkills(text);

            // 2. Extrai um Resumo básico do topo do documento
            model.Summary = ExtractSummary(text);

            return model;
        }

        private string ExtractTextFromPdf(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var pdf = PdfDocument.Open(stream);
            var sb = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            return sb.ToString();
        }

        private string ExtractSkills(string text)
        {
            var match = Regex.Match(text, @"(?:Habilidades|Skills|Tecnologias)[:\s\n]+([^\n]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private string ExtractSummary(string text)
        {
            // Pega os primeiros 300 caracteres como resumo preliminar
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string cleaned = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return cleaned.Length > 300 ? cleaned.Substring(0, 300) + "..." : cleaned;
        }
    }
}