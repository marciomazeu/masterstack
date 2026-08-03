using System.Collections.Generic;
using MasterStack.Models;

namespace MasterStack.ViewModels
{
    public class HomeViewModel
    {
        // Esta lista vai segurar os posts que vamos exibir na Home
        // Se você ainda não tem a classe "Post", pode deixar como "List<string>" ou comentar a linha
        //public List<Article> LatestPosts { get; set; } = new List<Article>();

        // Uma lista forte contendo os posts que vierem do banco
        public List<BlogPost> LatestPosts { get; set; } = new List<BlogPost>();
    }
}