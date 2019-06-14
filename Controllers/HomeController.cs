using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using betten.Models;
using Newtonsoft.Json;
using System.Net;

namespace betten.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["IsLocal"] = Request.HttpContext.Connection.RemoteIpAddress.Equals(IPAddress.Loopback);
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
