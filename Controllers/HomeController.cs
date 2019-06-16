using Microsoft.AspNetCore.Mvc;
using System.Net;
using betten.Utils;
using betten.Model;
using System.Threading.Tasks;
using System.Linq;

namespace betten.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["IsLocal"] = Request.HttpContext.Connection.RemoteIpAddress.Equals(IPAddress.Loopback);
            return View();
        }

        public async Task<IActionResult> Export(int id)
        {
            var dbContext = new BettenContext();
            ExcelExporter excelExporter = new ExcelExporter(dbContext, id);
            var evt = dbContext.Events.First(e => e.Id == id);
            return File(
                await excelExporter.Export(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                 $"{evt.Title} {evt.Date}.xlsx");
        }
    }
}
