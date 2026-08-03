using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MasterStack.Services
{
    public class GeminiResponseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class GeminiAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiAiService(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["Gemini:ApiKey"] 
                      ?? throw new InvalidOperationException("A configuração Gemini:ApiKey não foi encontrada.");
        }

        public async Task<GeminiResponseDto> GeneratePostSuggestionAsync(string topic, string culture, string length, string? opinion = null)
        {
            if (!string.IsNullOrEmpty(topic))
            {
                topic = topic.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }

            if (!string.IsNullOrEmpty(opinion))
            {
                opinion = opinion.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            string contextoHumano = string.IsNullOrWhiteSpace(opinion) 
                ? "" 
                : $"O autor do blog forneceu a seguinte experiência pessoal/opinião sobre o tema: '{opinion}'. Integre esse relato, ponto de vista ou dados nativamente e de forma fluida no meio do texto para dar autoridade real ao artigo.";

            var prompt = $@"
                Você é um especialista em SEO e redator profissional de tecnologia.
                Gere uma sugestão de artigo sobre o seguinte tema: '{topic}'.
                O idioma de saída deve ser estritamente: '{culture}'.
                A extensão do texto principal (Content) deve ser {length}.
                
                {contextoHumano}
                
                Retorne a resposta EXATAMENTE no formato JSON abaixo:
                {{
                    ""Title"": ""Título chamativo e focado em SEO"",
                    ""Slug"": ""url-amigavel-do-titulo"",
                    ""MetaDescription"": ""Resumo de até 160 caracteres para o Google"",
                    ""Content"": ""<p>Conteúdo completo do artigo formatado em HTML simples limpo.</p>""
                }}";

            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // 1. DECLARAÇÃO ÚNICA: Definimos a variável aqui no topo do bloco
            HttpResponseMessage response = null!;
            int maxRetries = 3;
            int delayMs = 1500;

            for (int i = 0; i < maxRetries; i++)
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(url)) 
                { 
                    Content = content 
                };
                
                // 2. USO CORRETO: Sem a palavra 'var' antes de response, pois ela já foi declarada na linha acima
                response = await _httpClient.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode || response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    break;
                }

                if (i < maxRetries - 1)
                {
                    Console.WriteLine($"[API OSCILANDO]: Gemini instável (503). Tentativa {i + 1} de {maxRetries}...");
                    await Task.Delay(delayMs);
                }
            }
            
            // 3. Validação normal usando a variável declarada lá em cima
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro na API do Gemini: {response.StatusCode}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
                // ... Resto do código de leitura do responseString ...

            using var doc = JsonDocument.Parse(responseString);
            
            var aiResponseText = doc.RootElement.GetProperty("candidates")[0]
                                     .GetProperty("content")
                                     .GetProperty("parts")[0]
                                     .GetProperty("text")
                                     .GetString();

            if (string.IsNullOrWhiteSpace(aiResponseText))
            {
                throw new Exception($"A API do Gemini não retornou um texto válido. Resposta bruta: {responseString}");
            }

            // 🔥 NOVA LIMPEZA BLINDADA: Corta tudo o que estiver fora do primeiro '{' e do último '}'
            var jsonMatch = Regex.Match(aiResponseText, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                aiResponseText = jsonMatch.Value;
            }
            else
            {
                // Fallback clássico caso a Regex falhe por algum motivo estrutural
                aiResponseText = Regex.Replace(aiResponseText, @"^```json\s*", "", RegexOptions.IgnoreCase);
                aiResponseText = Regex.Replace(aiResponseText, @"^```\s*", "", RegexOptions.IgnoreCase);
                aiResponseText = Regex.Replace(aiResponseText, @"\s*```$", "", RegexOptions.IgnoreCase);
            }
            aiResponseText = aiResponseText.Trim();

            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            try
            {
                var postData = JsonSerializer.Deserialize<GeminiResponseDto>(aiResponseText, options);

                if (postData != null)
                {
                    if (!string.IsNullOrEmpty(postData.MetaDescription) && postData.MetaDescription.Length > 160)
                    {
                        postData.MetaDescription = postData.MetaDescription.Substring(0, 157) + "...";
                    }

                    if (!string.IsNullOrEmpty(postData.Content))
                    {
                        postData.Content = postData.Content.Replace("\n", "").Replace("\r", "");
                        postData.Content = postData.Content.Replace("<p></p>", "").Replace("<p> </p>", "");
                    }
                }

                return postData ?? throw new Exception("O resultado da desserialização retornou nulo.");
            }
            catch (JsonException ex)
            {
                throw new Exception($"Erro ao decodificar JSON da IA: {ex.Message}. Conteúdo bruto: {aiResponseText}");
            }
        }

        public async Task<GeminiResponseDto> RefinePostAsync(string currentContent, string opinion, string culture)
        {
            if (!string.IsNullOrEmpty(currentContent))
            {
                currentContent = currentContent
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")  
                    .Replace("\r", "\\r");
            }

            if (!string.IsNullOrEmpty(opinion))
            {
                opinion = opinion.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }

            // 🔥 CORREÇÃO: Removido lixo de link markdown que corrompia a URL
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var prompt = $@"
                Você é um editor de texto profissional. Você recebeu um artigo de blog em HTML e precisa refiná-lo com base na opinião e feedbacks do autor.
                Idioma do texto: '{culture}'.
                
                Texto Atual em HTML:
                ""{currentContent}""
                
                Instruções de Refinamento do Autor (Incorpore isso de forma orgânica e natural no texto acima):
                ""{opinion}""
                
                Melhore a escrita, corrija a fluidez e integre as instruções do autor perfeitamente no meio do conteúdo. Mantenha a estrutura de tags HTML limpas.
                
                Retorne a resposta EXATAMENTE no formato JSON abaixo (mantenha Title, Slug e MetaDescription vazios):
                {{
                    ""Title"": """",
                    ""Slug"": """",
                    ""MetaDescription"": """",
                    ""Content"": ""<p>O texto antigo totalmente reescrito e atualizado com as melhorias inclusas em HTML.</p>""
                }}";

            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(requestMessage);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro na API do Gemini: {response.StatusCode}");
            }

            // 🔥 RECOLOCADO: Lê a string da resposta HTTP enviada pelo Google
            var responseString = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseString);
            string? aiResponseText = null;
            
            // 🔥 RECOLOCADO: Faz o parse seguro para achar a propriedade "text" dentro do nó do Gemini
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var aiContent))
                {
                    if (aiContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        if (parts[0].TryGetProperty("text", out var textProperty))
                        {
                            aiResponseText = textProperty.GetString();
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(aiResponseText))
            {
                throw new Exception($"A API do Gemini não retornou um texto válido. Resposta bruta: {responseString}");
            }

            // 🔥 Aplica a mesma blindagem no refinamento
            var jsonMatch = Regex.Match(aiResponseText, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                aiResponseText = jsonMatch.Value;
            }
            else
            {
                aiResponseText = Regex.Replace(aiResponseText, @"^```json\s*", "", RegexOptions.IgnoreCase);
                aiResponseText = Regex.Replace(aiResponseText, @"^```\s*", "", RegexOptions.IgnoreCase);
                aiResponseText = Regex.Replace(aiResponseText, @"\s*```$", "", RegexOptions.IgnoreCase);
            }
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            try
            {
                // Aqui o .NET vai encontrar a variável 'options' sem problemas
                var resultData = JsonSerializer.Deserialize<GeminiResponseDto>(aiResponseText, options);
                
                if (resultData != null)
                {
                    resultData.Content = resultData.Content ?? "";
                    resultData.Content = resultData.Content.Replace("\n", "").Replace("\r", "");
                    resultData.Title = resultData.Title ?? "";
                    resultData.Slug = resultData.Slug ?? "";
                    resultData.MetaDescription = resultData.MetaDescription ?? "";
                }

                return resultData ?? throw new Exception("A desserialização retornou nulo.");
            }
            catch (JsonException ex)
            {
                throw new Exception($"Falha ao ler o JSON. Erro: {ex.Message}. Conteúdo retornado pela IA: {aiResponseText}");
            }
        }
    }
}