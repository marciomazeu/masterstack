# MasterStack - ASP.NET Core Multi-Language Web App

O **MasterStack** é uma aplicação web moderna desenvolvida em ASP.NET Core MVC, projetada com uma arquitetura robusta para suporte a múltiplos idiomas (i18n). O projeto foca em boas práticas de localização, performance e interface responsiva.

## 🌐 Funcionalidades de Localização
* **Suporte Trilíngue:** Totalmente traduzido para Português (Brasil), Inglês (EUA) e Francês (Canadá).
* **Deteção de Idioma via URL:** Utiliza rotas dinâmicas (ex: `/pt-BR/Home`) para garantir SEO amigável.
* **Fallback de Recursos:** Sistema de segurança que utiliza recursos neutros caso uma tradução específica não seja encontrada.
* **Interface Adaptável:** Seletor de idiomas com bandeiras dinâmicas e layout que respeita o tamanho das palavras em diferentes línguas.

## 🛠️ Tecnologias Utilizadas
* **Backend:** .NET 8 / ASP.NET Core MVC
* **Frontend:** Bootstrap 5, Razor Pages, CSS3
* **Localização:** Arquivos de Recurso (.resx) e `IHtmlLocalizer`
* **Ícones:** FontAwesome & FlagCDN

## 🚀 Como Executar o Projeto
1. Clone este repositório:
   ```bash
   git clone [https://github.com/SEU_USUARIO/MasterStack.git](https://github.com/SEU_USUARIO/MasterStack.git)
2. Abra a solução no Visual Studio 2022.

3. Certifique-se de que a carga de trabalho "ASP.NET e desenvolvimento web" está instalada.

4. Pressione F5 para rodar o projeto.

📁 Estrutura de Pastas de Localização
* SharedResource.resx: Base principal (Inglês).

* SharedResource.pt-BR.resx: Traduções para Português.

* SharedResource.fr-CA.resx: Traduções para Francês.

Desenvolvido por Marcio Jose Mazeu.
