using Microsoft.AspNetCore.Mvc;

namespace MasterStack.Controllers
{
    public class TutorialController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
