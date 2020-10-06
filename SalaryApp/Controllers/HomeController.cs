using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SalaryApp.Models;
using System.Data;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PagedList;



namespace SalaryApp.Controllers
{
    public class HomeController : Controller
    {
        private AppDbContext db;
        private UserManager<ApplicationUser> manager;

        public HomeController()
        {
            db = new AppDbContext();
            manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
        }

        public ActionResult roles()
        {
            var users = (from p in db.Users select new URolesItem() { name = p.UserName, Roles = (from rid in p.Roles join r in db.Roles on rid.RoleId equals r.Id select r.Name).ToList() }).ToList();
            return View(users);
        }

        [Authorize]
        public ActionResult Index(PaymentSearchModel model) //http://www.advancesharp.com/blog/1096/mvc-search-page-example-with-code
        {
            const int RecordsPerPage = 10;
            var pageIndex = model.Page ?? 1;
            ApplicationUser currentUser = manager.FindById(User.Identity.GetUserId());

            ViewBag.Projects = getUserProjects(currentUser);
            ViewBag.TotalBalance = currentUser.UserBalances.Sum(x => x.Sum).ToString("N0");
            var results = db.UserPayments.ToList().Where(p => p.User.Id == currentUser.Id).Where(p => p.PaymentGroup.State == PaymentsGroupState.PaidOut);
            ViewBag.PaidOutSum = results.Sum(x => x.Sum).ToString("N0");
            if ( model.Project != null )
            {
                results = results.Where(p => p.Project.ProjectId == model.Project);
                ViewBag.Balance = currentUser.UserBalances.Where(x=>x.Project.ProjectId == model.Project).Sum(x => x.Sum).ToString("N0");
            }
            

            if ( model.DateFrom != null )
            {
                int d = model.DateFrom.Value.Day;
                model.DateFrom = new DateTime(model.DateFrom.Value.Year, model.DateFrom.Value.Month, 1);
                results = results.Where(p => p.PaymentGroup.WhenPaidOut >= model.DateFrom);
                model.DateFrom = new DateTime(model.DateFrom.Value.Year, model.DateFrom.Value.Month, d);

            }
            if (model.DateTo != null)
            {
                int d = model.DateTo.Value.Day;
                model.DateTo = new DateTime(model.DateTo.Value.Year, model.DateTo.Value.Month, 1);
                results = results.Where(p => p.PaymentGroup.WhenPaidOut <= model.DateTo);
                model.DateTo = new DateTime(model.DateTo.Value.Year, model.DateTo.Value.Month, d);
            }


            ViewBag.FilterSum = results.Sum(x => x.Sum).ToString("N0");

            if (!String.IsNullOrEmpty(model.sortby))
            {
                if (model.sortby == "during")
                {
                    model.DuringSortOrder = (model.DuringSortOrder == "asc") ? "desc" : "asc";

                    results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                    results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                }
                else
                {
                    if (model.sortby == "project")
                    {
                        model.ProjectSortOrder = (model.ProjectSortOrder == "asc") ? "desc" : "asc";
                        results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                        results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                    }
                }

            }
            else
            {
                if (!String.IsNullOrEmpty(model.presortby))
                {
                    if( model.presortby == "during" )
                    {
                        results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                        results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                    }
                    else if (model.presortby == "project")
                    {
                        results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                        results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                    }
                    else
                    {
                        results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                        results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                    }

                }
                else
                {
                    results = model.ProjectSortOrder == "asc" ? results.OrderBy(x => x.Project.Name) : results.OrderByDescending(x => x.Project.Name);
                    results = model.DuringSortOrder == "asc" ? results.OrderBy(x => x.PaymentGroup.WhenPaidOut) : results.OrderByDescending(x => x.PaymentGroup.WhenPaidOut);
                }
            }

            model.SearchResults = results.ToPagedList(pageIndex, RecordsPerPage);

            return View(model);
        }
        [Authorize]
        public ActionResult MemberProjects()
        {
            var currentUser = manager.FindById(User.Identity.GetUserId());
            return View(getUserProjects(currentUser));
        }

        public static UserSalaryTable BuildSalaryTable(List<ApplicationUser> users)
        {
            UserSalaryTable model = new UserSalaryTable();
            foreach (var user in users)
            {
                UserSalaryViewModel UserSalary = new UserSalaryViewModel()
                {
                    ShortName = user.ShortName != null ? user.ShortName : user.UserName,
                    Dept = user.Department == null ? "Ошибка" : user.Department.Name,
                    id = user.Id,
                    ProjectsBalances = user.UserBalances.Select(x => new UserProjectSalaryViewModel()
                    {
                        id = x.Project.ProjectId,
                        Balance = x.Sum,
                        ProjectName = x.Project.Name,
                        PaidsOut = user.UsersPayments.Where(y => y.PaymentGroup.State == PaymentsGroupState.PaidOut && y.Project.ProjectId == x.Project.ProjectId).
                                    GroupBy(y => y.PaymentGroup.WhenPaidOut).ToDictionary(z => z.Key, z => z.Sum(v => v.Sum)),
                        Balanced = user.UsersPayments.Where(y => y.PaymentGroup.State == PaymentsGroupState.InProcess && y.Project.ProjectId == x.Project.ProjectId).
                                    GroupBy(y => y.PaymentGroup.WhenCreated).ToDictionary(z => z.Key, z => z.Sum(v => v.Sum))
                    })

                };
                model.Users.Add(UserSalary);
            }

            foreach (var x in model.Users)
            {
                foreach (var y in x.ProjectsBalances)
                {
                    foreach (var z in y.PaidsOut)
                    {
                        if (!model.PaymentsGroupsColumns.Contains(z.Key))
                            model.PaymentsGroupsColumns.Add(z.Key);
                    }
                    foreach (var z in y.Balanced)
                    {
                        if (!model.PaymentsGroupsColumns.Contains(z.Key))
                            model.PaymentsGroupsColumns.Add(z.Key);
                    }
                }
            }

            model.PaymentsGroupsColumns.Sort(delegate (DateTime x, DateTime y) { return y.CompareTo(x); });


            return model;
        }

        [Authorize(Roles = "Administrators,Directors,Managers")]
        public ActionResult Members()
        {
            ApplicationUser currentUser = manager.FindById(User.Identity.GetUserId());

            List<ApplicationUser> users = new List<ApplicationUser>();
            var results = (from u in db.Users select u);

            if( manager.IsInRole(currentUser.Id, SalaryRoles.Managers) && !manager.IsInRole(currentUser.Id, SalaryRoles.Directors) && !manager.IsInRole(currentUser.Id, SalaryRoles.Administrators) )
            {
                results = results.Where(x => x.Department.Boss.Id == currentUser.Id && x.UserName != "root" && x.HideSalary == false);
            }

            users = results.OrderBy(x => x.Department.Name).ToList();

            UserSalaryTable model = BuildSalaryTable(users);
            return View(model);
        }

        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult Projects()
        {
            ApplicationUser currentUser = manager.FindById(User.Identity.GetUserId());
            List<MemberProjectView> projects = new List<MemberProjectView>();

            if (manager.IsInRole(currentUser.Id, SalaryRoles.Directors) || manager.IsInRole(currentUser.Id, SalaryRoles.Administrators))
            {
                projects = (from p in db.Projects select new MemberProjectView()
                    { ProjectId = p.ProjectId, ProjectName = p.Name, Balance = 0, Debet = 0, TotalSum = 0, ProjectState = p.ProjectState }).ToList();
            }
            else
            {

                if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers))
                {
                    projects = (from p in db.Projects where p.Department.Boss.Id == currentUser.Id select new MemberProjectView()
                        { ProjectId = p.ProjectId, ProjectName = p.Name, Balance = 0, Debet = 0, TotalSum = 0, ProjectState = p.ProjectState }).ToList();
                }
                if (manager.IsInRole(currentUser.Id, SalaryRoles.Assistant))
                {
                    projects.AddRange( (from p in db.Projects where p.Department.Assistant.Id == currentUser.Id select new MemberProjectView()
                        { ProjectId = p.ProjectId, ProjectName = p.Name, Balance = 0, Debet = 0, TotalSum = 0, ProjectState = p.ProjectState }).ToList() );
                }
            }

            var debets = db.UserPayments.Where(x=>x.PaymentGroup.State == PaymentsGroupState.PaidOut).GroupBy(x => x.Project).Select(y => new MemberProjectView() { Debet = y.Sum(z => z.Sum), ProjectId = y.Key.ProjectId }).ToList();
            var balances = db.UserBalances.GroupBy(x => x.Project).Select(y => new MemberProjectView() { Balance = (int) y.Sum(z => z.Sum), ProjectId = y.Key.ProjectId }).ToList();
            for (int i =0; i<projects.Count; i++)
            {
                var b = balances.FirstOrDefault(x => x.ProjectId == projects[i].ProjectId);
                var d = debets.FirstOrDefault(x => x.ProjectId == projects[i].ProjectId);
                projects[i].Balance = (b == null ? 0 : b.Balance);
                projects[i].Debet = (d == null ? 0 : d.Debet);
                projects[i].TotalSum = projects[i].Balance + projects[i].Debet;
            }

            return View(projects.OrderByDescending(x=>x.TotalSum));
        }

        private IEnumerable<MemberProjectView> getUserProjects(ApplicationUser User)
        {
            if (User == null) return new List<MemberProjectView>();
            List<MemberProjectView> projects = User.UserBalances.Select(p => 
                new MemberProjectView() { ProjectName = p.Project.Name, ProjectId = p.Project.ProjectId, Balance = p.Sum }).ToList();

            for(int i=0; i<projects.Count; i++)
            {
                projects[i].Debet = 
                    User.UsersPayments.Where(x => x.Project.ProjectId == projects[i].ProjectId && x.PaymentGroup.State == PaymentsGroupState.PaidOut).Sum(x => x.Sum);
                projects[i].TotalSum = projects[i].Debet + projects[i].Balance;
            }

            return projects;
        }


    }
}