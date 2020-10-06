using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using SalaryApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SalaryApp.Controllers
{
    public class HelpController : Controller
    {
        private Microsoft.AspNet.Identity.UserManager<ApplicationUser> manager;
        private AppDbContext db;
        // GET: Help

        public HelpController()
        {
            db = new AppDbContext();
            manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
        }
        public ActionResult Index()
        {
            return View();
        }
    }
}