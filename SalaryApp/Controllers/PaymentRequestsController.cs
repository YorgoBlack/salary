using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using PagedList;
using SalaryApp.Models;
using OfficeOpenXml;
using MimeKit;
using MailKit.Net.Smtp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace SalaryApp.Controllers
{
    [Authorize]
    public class PaymentRequestsController : Controller
    {
        private AppDbContext db;
        private UserManager<ApplicationUser> manager;

        public PaymentRequestsController()
        {
            db = new AppDbContext();
            manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
        }

        private ApplicationUser GetFirstUserInRole(string roleName)
        {
            var RoleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            var user = RoleManager.FindByName(roleName).Users.FirstOrDefault();
            return user == null ? null : db.Users.Find(user.UserId);
        }


        // GET: PaymentRequests
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult Index(PaymentRequestSearchModel model)
        {
            const int RecordsPerPage = 20;
            var pageIndex = model.Page ?? 1;
            var currentUser = manager.FindById(User.Identity.GetUserId());

            var results = (from x in db.PaymentRequests select x);
            bool isDirOrAdmin = manager.IsInRole(currentUser.Id, SalaryRoles.Directors) || manager.IsInRole(currentUser.Id, SalaryRoles.Administrators);

            if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) && !isDirOrAdmin)
            {
                results = results.Where(x => currentUser.Id == (x.Project.Department.Boss != null ? x.Project.Department.Boss.Id : "nouser")   );
            }
            else  if( manager.IsInRole(currentUser.Id, SalaryRoles.Assistant) && !isDirOrAdmin )
            {
                results = results.Where(x => currentUser.Id == (x.Project.Department.Assistant != null ? x.Project.Department.Assistant.Id : "nouser"));
            }
            else if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) && manager.IsInRole(currentUser.Id, SalaryRoles.Assistant) && !isDirOrAdmin )
            {
                var r1 = results.Where(x => currentUser.Id == (x.Project.Department.Boss != null ? x.Project.Department.Boss.Id : "nouser"));
                var r2 = results.Where(x => currentUser.Id == (x.Project.Department.Assistant != null ? x.Project.Department.Assistant.Id : "nouser"));
                results = r1.Concat(r2);
            }

            if ( model.SelectedStates == null )
            {
                if (Request.Cookies["SelectedStates"] == null)
                {
                    if (manager.IsInRole(currentUser.Id, "Administrators"))
                        model.SelectedStates = new PaymentRequestState[1] { PaymentRequestState.Confirmed };
                    if (manager.IsInRole(currentUser.Id, "Directors"))
                        model.SelectedStates = new PaymentRequestState[1] { PaymentRequestState.WaitConfirm };
                }
                else
                {
                    Object obj = JsonConvert.DeserializeObject(Request.Cookies["SelectedStates"].Value);
                    model.SelectedStates = ((IEnumerable)obj).Cast<object>().Select(x => PaymentRequest.FromString(x.ToString())).ToArray();
                }
            }
            if( model.SelectedStates != null )
            {
                Response.Cookies.Add(new HttpCookie("SelectedStates", JsonConvert.SerializeObject(model.SelectedStates)));
            }

            if( model.SelectedStates != null )
            {
                results = results.Where(x => model.SelectedStates.Contains(x.RequestState));
            }

            if (model.Project != null)
            {
                results = results.Where(x => x.Project.ProjectId == model.Project);
            }

            if (model.DateTo != null)
            {
                results = results.Where(x => x.WhenCreated <= model.DateTo);
            }
            if (model.DateFrom != null)
            {
                results = results.Where(x => x.WhenCreated >= model.DateFrom);
            }

            ViewBag.Projects = results.Select(x => x.Project).Distinct().ToList();
            model.SearchResults = results.ToList().ToPagedList(pageIndex, RecordsPerPage);
            model.Comments.Clear();

            foreach (var it in model.SearchResults)
            {
                int cnt = db.RequestHistories.Where(x => x.Request.PaymentRequestId == it.PaymentRequestId).ToList().Count;
                if (cnt > 0)
                {
                    model.Comments.Add(it.PaymentRequestId, null);
                }
            }
            return View(model);
        }

        private void GenerateProjectsList()
        {
            var currentUser = manager.FindById(User.Identity.GetUserId());

            var results = (from x in db.Projects select x);


            if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) && !manager.IsInRole(currentUser.Id, SalaryRoles.Directors))
            {
                results = results.Where(x => currentUser.Id == (x.Department.Boss != null ? x.Department.Boss.Id : "nouser"));
            }
            else if (manager.IsInRole(currentUser.Id, SalaryRoles.Assistant) && !manager.IsInRole(currentUser.Id, SalaryRoles.Directors) )
            {
                results = results.Where(x => currentUser.Id == (x.Department.Assistant != null ? x.Department.Assistant.Id : "nouser"));
            }
            else if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) && manager.IsInRole(currentUser.Id, SalaryRoles.Assistant) && !manager.IsInRole(currentUser.Id, SalaryRoles.Directors))
            {
                var r1 = results.Where(x => currentUser.Id == (x.Department.Boss != null ? x.Department.Boss.Id : "nouser"));
                var r2 = results.Where(x => currentUser.Id == (x.Department.Assistant != null ? x.Department.Assistant.Id : "nouser"));
                results = r1.Concat(r2);
            }

            var depts = (from d in db.Departments where d.Boss.Id == currentUser.Id || d.Assistant.Id == currentUser.Id select d).ToList();
            if (manager.IsInRole(currentUser.Id, SalaryRoles.Directors))
            {
                depts = (from d in db.Departments select d).ToList();
            }
            ViewBag.Departments = depts;


            ViewBag.Projects = results.ToList();

        }

        // GET: PaymentRequests/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PaymentRequest PaymentRequest = db.PaymentRequests.Find(id);
            if (PaymentRequest == null)
            {
                return HttpNotFound();
            }
            return View(PaymentRequest);
        }

        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        // GET: PaymentRequests/Create
        public ActionResult Create()
        {
            GenerateProjectsList();
            PaymentRequestEditModel model = new PaymentRequestEditModel() { Sum_Add = new double[100], UserName_Add = new string[100], type_Add = new PaymentRequestType[100] };
            model.DepartmentId = 0;
            return View("Create", model);
        }
        public static Project ProjectFind(AppDbContext db, string ProjectCode, string ProjectShortName)
        {
            string ProjectFullName = ProjectCode + "_" + ProjectShortName;
            Project prj = db.Projects.SingleOrDefault(x => x.Name.Trim() == ProjectFullName.Trim());
            if (prj == null)
            {
                ProjectFullName = ProjectCode + "-" + ProjectShortName;
                prj = db.Projects.SingleOrDefault(x => x.Name.Trim() == ProjectFullName.Trim());
                if (prj == null)
                {
                    ProjectFullName = ProjectShortName;
                    prj = db.Projects.SingleOrDefault(x => x.Name.Trim() == ProjectFullName.Trim());
                }
            }
            return prj;
        }

        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PaymentRequestEditModel input)
        {
            if (ModelState.IsValid)
            {
                Project prj = ProjectFind(db, input.ProjectCode, input.ProjectShortName);

                if ( (input.ProjectCode != null && input.ProjectShortName != null) || prj != null )
                {
                    input.ProjectName = input.ProjectCode + "_" + input.ProjectShortName;
                    var currentUser = manager.FindById(User.Identity.GetUserId());
                    ApplicationUser recipient = db.Users.SingleOrDefault(x => x.FullName == input.UserName);

                    bool exists = System.IO.Directory.Exists(Server.MapPath("~/App_Data"));
                    if (!exists)
                    {
                        System.IO.Directory.CreateDirectory(Server.MapPath("~/App_Data"));
                        System.IO.Directory.CreateDirectory(Server.MapPath("~/App_Data/Uploads"));
                    }
                    else
                    {
                        if (!System.IO.Directory.Exists(Server.MapPath("~/App_Data/Uploads")))
                            System.IO.Directory.CreateDirectory(Server.MapPath("~/App_Data/Uploads"));
                    }

                    string fileName = input.File != null ? Environment.TickCount + "_" + System.IO.Path.GetFileName(input.File.FileName) : "";
                    if (fileName != "")
                    {
                        try
                        {
                            var path = System.IO.Path.Combine(Server.MapPath("~/App_Data/Uploads"), fileName);
                            input.File.SaveAs(path);
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError("", "Ошибка загрузки файла " + e.Message);
                            GenerateProjectsList();
                            return View(input);
                        }
                    }


                    if (recipient != null)
                    {
                        if (prj == null)
                        {
                            var res = (from d in db.Departments select d);
                            if (!manager.IsInRole(currentUser.Id, SalaryRoles.Directors))
                            {
                                res = res.Where(d => d.Boss.Id == currentUser.Id || d.Assistant.Id == currentUser.Id);
                            }

                            var depts = res.ToList();
                            Department dept = null;
                            if (depts.Count == 0 && !manager.IsInRole(currentUser.Id, "Directors"))
                            {
                                ModelState.AddModelError("", "Пользователь не имеет прав добавлять проекты ");
                                GenerateProjectsList();
                                return View(input);
                            }
                            else if (depts.Count == 0 && manager.IsInRole(currentUser.Id, "Directors"))
                            {
                                dept = db.Departments.SingleOrDefault(x => x.Name == "_Резерв");
                                if (dept == null)
                                {
                                    dept = db.Departments.Add(new Department { Name = "_Резерв", Boss = currentUser });
                                    db.SaveChanges();
                                    dept = db.Departments.SingleOrDefault(x => x.Name == "_Резерв");
                                }
                            }
                            else if (depts.Count > 0)
                            {
                                dept = depts.FirstOrDefault(x => x.DepartmentId == input.DepartmentId);
                                if (dept == null)
                                {
                                    ModelState.AddModelError("", "Неверно задано направление ");
                                    GenerateProjectsList();
                                    return View(input);
                                }
                            }
                            prj = db.Projects.Add(new Project() { Department = dept, Name = input.ProjectName });
                            db.SaveChanges();
                        }

                        PaymentRequest r = new PaymentRequest()
                        {
                            SumType = input.type,
                            TimesOrSum = input.type == PaymentRequestType.Times ? input.Sum : PaymentRequest.RoundSum(input.Sum),
                            AgreedPM = input.AgreedPM,
                            Project = prj,
                            AttachedFileName = fileName,
                            AppointedUser = currentUser,
                            RecipientUser = recipient,

                        };

                        if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) || manager.IsInRole(currentUser.Id, SalaryRoles.Assistant))
                        {
                            r.RequestState = input.SetStateWaitConfirm ? PaymentRequestState.WaitConfirm : PaymentRequestState.InProcess;
                            // шлем почту если надо
                            if (r.RequestState == PaymentRequestState.WaitConfirm)
                            {
                                SendMailToDirector(r.AppointedUser.ShortName, r.Project.Name);
                            }
                        }
                        else if (manager.IsInRole(currentUser.Id, "Directors"))
                            r.RequestState = input.SetStateCredited ? PaymentRequestState.Confirmed : PaymentRequestState.WaitConfirm;

                        r = db.PaymentRequests.Add(r);

                        if (!String.IsNullOrEmpty(input.Comments))
                        {
                            db.RequestHistories.Add(new RequestHistory() { WhenPosted = DateTime.Now, Comments = input.Comments, Author = currentUser, Request = r });
                        }


                        db.SaveChanges();

                        for(int i=0; i<input.UserName_Add.Length; i++)
                        {
                            if( !string.IsNullOrEmpty(input.UserName_Add[i]) && input.Sum_Add[i] > 0)
                            {
                                string str = input.UserName_Add[i];
                                ApplicationUser recipient1 = db.Users.SingleOrDefault(x => x.FullName == str);
                                if (recipient1 != null)
                                {
                                    PaymentRequest r1 = new PaymentRequest()
                                    {
                                        AgreedPM = input.AgreedPM,
                                        Project = r.Project,
                                        AttachedFileName = r.AttachedFileName,
                                        AppointedUser = r.AppointedUser,
                                        RequestState = r.RequestState,

                                        RecipientUser = recipient1,
                                        SumType = input.type_Add[i],
                                        TimesOrSum = input.type_Add[i] == PaymentRequestType.Times ? input.Sum_Add[i] : PaymentRequest.RoundSum(input.Sum_Add[i])
                                    };
                                    db.PaymentRequests.Add(r1);
                                    db.SaveChanges();
                                }
                            }
                        }

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Сотрудник не найден или не задано поле Кому");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Не задано поле Шифр и/или Название");
                }
            }
            ModelState.AddModelError("", "Ошибка ввода");
            GenerateProjectsList();
            return View(input);
        }

        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult AccrueFileForm()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult AccrueExec(HttpPostedFileBase upload) 
        {
            AccruiesViewModel model = new AccruiesViewModel() {  Accruies = new List<AccrueItem>() };
            Dictionary<string, double> stavki = new Dictionary<string, double>();
            IEnumerable<UsersExchangeData> users_all = DepartmentsController.getDomainUsers();


            if ( upload != null )
            {
                using (var package = new ExcelPackage(upload.InputStream))
                {
                    var Sheet = package.Workbook.Worksheets[1];
                    int lastRow = Sheet.Dimension.End.Row;
                    int lastCol = Sheet.Dimension.End.Column;
                    int i = 5;
                    UsersExchangeData userdata;
                    do
                    {
                        // обновляем данные
                        if (Sheet.Cells[i, 1].Value != null)
                        {
                            ApplicationUser user = null;
                            string username = Sheet.Cells[i, 1].Value.ToString();
                            userdata = DepartmentsController.CheckDomainUserExists(username, users_all);
                            if (userdata != null)
                            {
                                user = manager.FindByName(userdata.UserLogin);
                                if (user == null)
                                {
                                    user = new ApplicationUser();
                                    user.UserName = userdata.UserLogin;
                                    user.FullName = userdata.FullName;
                                    user.Email = "";
                                    user.ShortName = username;
                                    var res = manager.Create(user);
                                }
                                else
                                {
                                    user.ShortName = username;
                                }
                                db.SaveChanges();
                            }
                        }
                        else
                        {
                            userdata = null;
                        }

                        if (userdata != null && Sheet.Cells[i, 3].Value != null)
                        {
                            if (!String.IsNullOrEmpty(Sheet.Cells[i, 1].Value.ToString()))
                            {
                                if (!String.IsNullOrEmpty(Sheet.Cells[i, 3].Value.ToString()))
                                {
                                    double v;
                                    if (double.TryParse(Sheet.Cells[i, 3].Value.ToString(), out v))
                                    {
                                        stavki.Add(Sheet.Cells[i, 1].Value.ToString().Trim().ToUpper(),v);
                                        db.updShowSalary( manager.FindByName(userdata.UserLogin), v);
                                    }
                                }
                            }
                        }
                        i++;
                    } while (i <= lastRow);

                }
                model.RequestsIds = new List<int>();
                foreach ( KeyValuePair<string,double> stavka in stavki)
                {
                    var request = db.PaymentRequests.Where(p => p.RecipientUser.ShortName.Trim().ToUpper() == stavka.Key && p.RequestState == PaymentRequestState.Confirmed).ToList();
                    //var request = (from p in db.PaymentRequests where p.RecipientUser.ShortName.Trim().ToUpper() == stavka.Key && p.RequestState == PaymentRequestState.Confirmed select p).ToList();
                    foreach(var p in request)
                    {
                        AccrueItem m = new AccrueItem() {
                            RequestId = p.PaymentRequestId,
                            UserShortName = stavka.Key,
                            ProjectName = p.Project.Name,
                            UserId = p.RecipientUser.Id,
                            ProjectId = p.Project.ProjectId,
                            Sum = p.SumType == PaymentRequestType.Times ? PaymentRequest.RoundSum(p.TimesOrSum * stavka.Value) : p.TimesOrSum,
                            Comments = p.SumType == PaymentRequestType.Times ? "=" + p.TimesOrSum + " часов" : ""
                        };

                        bool find = false;
                        for(int l = 0; l < model.Accruies.Count; l++ )
                        {
                            if(model.Accruies[l].ProjectId == m.ProjectId && model.Accruies[l].UserId == m.UserId )
                            {
                                find = true;
                                model.Accruies[l].Sum += m.Sum;
                            }
                        }

                        if (!find)
                        {
                            model.Accruies.Add(m);
                        }

                        model.RequestsIds.Add(p.PaymentRequestId);
                    }
                }
            }
            
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult AccrueConfirm(AccruiesViewModel model)
        {
            if (model.Accruies != null)
            {
                foreach (var item in model.Accruies)
                {
                    PaymentRequest r = db.PaymentRequests.Find(item.RequestId);
                    if (r != null)
                    {
                        UserBalance b = db.UserBalances.FirstOrDefault(x => x.User.Id == item.UserId && x.Project.ProjectId == item.ProjectId);
                        if( b == null )
                        {
                            b = db.UserBalances.Add(new UserBalance() {User = db.Users.Find(item.UserId), Project = db.Projects.Find(item.ProjectId), Sum = 0 } );
                            db.UserBalances.Add(b);
                        }
                        b.Sum += item.Sum;
                    }
                }

                db.SaveChanges();

                if (model.RequestsIds != null)
                {
                    foreach (var rID in model.RequestsIds)
                    {
                        PaymentRequest r = db.PaymentRequests.Find(rID);
                        if (r != null)
                        {
                            r.RequestState = PaymentRequestState.Credited;
                            r.WhenStateChanged = DateTime.Now;
                            db.SaveChanges();
                        }
                    }

                }
            }

            return RedirectToAction("Index");
        }

        public ActionResult FindProjectByDept(string query)
        {
            var currentUser = manager.FindById(User.Identity.GetUserId());
            List<UsersListItemModel> l = new List<UsersListItemModel>();

            if (manager.IsInRole(currentUser.Id, SalaryRoles.Directors) || manager.IsInRole(currentUser.Id, SalaryRoles.Administrators) )
            {
                l = (from p in db.Projects
                     where p.Name.ToUpper().Contains(query.ToUpper())
                     select new UsersListItemModel() { Name = p.Name, ProjectCode ="Code", ProjectShortName = "ShortName", id = p.ProjectId }).ToList();
            }
            else if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers) || manager.IsInRole(currentUser.Id, SalaryRoles.Assistant))
            {
                l = (from p in db.Projects
                     where (p.Department.Boss.Id == currentUser.Id || p.Department.Assistant.Id == currentUser.Id) && p.Name.ToUpper().Contains(query.ToUpper())
                     select new UsersListItemModel() { Name = p.Name, ProjectCode = "Code", ProjectShortName = "ShortName", id = p.ProjectId }).ToList();
            }

            return Json(l, JsonRequestBehavior.AllowGet);
        }

        public ActionResult FindUserByName(string query)
        {
            var users_all = db.Users.ToList();
            List<UsersListItemModel> list = new List<UsersListItemModel>();
            foreach(var item in users_all)
            {
                if( item.FullName.ToUpper().Contains(query.ToUpper()) && item.UserName != "root")
                    list.Add(new UsersListItemModel() { Name = item.FullName });
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        public ActionResult HistoryComments(int? id)
        {
            PaymentRequestEditModel edit = new PaymentRequestEditModel();
            if (id == null )
            {
                edit.HistoryComments = "Нет комментариев";
            }
            else
            {
                var q1 = db.RequestHistories.Where(x => x.Request.PaymentRequestId == id).ToList();
                string text = "";
                foreach (var item in q1)
                {
                    text += item.WhenPosted.ToString("dd.MM.yyyy HH:mm") + ", " + item.Author.ShortName + ": <br/>";
                    text += item.Comments;
                    text += "<hr/>";
                }
                edit.HistoryComments = text == "" ? "Нет комментариев" : text;
            }
            return PartialView("HistoryComments", edit);
            //return View(edit);
            //return Json(edit, JsonRequestBehavior.AllowGet);
        }

        // GET: PaymentRequests/Edit/5
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PaymentRequest r = db.PaymentRequests.Find(id);
            if (r == null)
            {
                return HttpNotFound();
            }
            int p = r.AttachedFileName.IndexOf('_');
            string fn = p > 0 ? r.AttachedFileName.Substring(p+1, r.AttachedFileName.Length - p-1) : r.AttachedFileName;

            var q1 = db.RequestHistories.Where(x => x.Request.PaymentRequestId == r.PaymentRequestId).ToList();
            string text = "";
            foreach(var item in q1)
            {
                text += item.WhenPosted.ToString("dd.MM.yyyy HH:mm") + ", " + item.Author.ShortName + ": <br/>";
                text += item.Comments;
                text += "<hr/>";
            }

            PaymentRequestEditModel edit = new PaymentRequestEditModel() {
                AttachFileName = fn, HistoryComments = text,
                type = r.SumType, PaymentRequestId = r.PaymentRequestId, AgreedPM = r.AgreedPM,
                ProjectName = r.Project.Name, Sum = r.TimesOrSum, UserName = r.RecipientUser.FullName,
                ProjectCode = PaymentRequestEditModel.getProjectCode(r.Project.Name),
                ProjectShortName = PaymentRequestEditModel.getProjectShortName(r.Project.Name)
            };

            GenerateProjectsList();

            return View(edit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult Edit(PaymentRequestEditModel input)
        {
            if (ModelState.IsValid)
            {
                var currentUser = manager.FindById(User.Identity.GetUserId());
                Project prj = db.Projects.SingleOrDefault(x => x.Name.Trim() == input.ProjectName.Trim());
                ApplicationUser recipient = db.Users.SingleOrDefault(x => x.FullName == input.UserName);
                PaymentRequest request = db.PaymentRequests.Find(input.PaymentRequestId);

                if (request != null)
                {
                    string pre_fileName = !String.IsNullOrEmpty(request.AttachedFileName) ? System.IO.Path.Combine(Server.MapPath("~/App_Data/Uploads"), request.AttachedFileName) : "";

                    if ( (pre_fileName != "") && input.DeleteAttachedFile )
                    {
                        System.IO.File.Delete(pre_fileName);
                    }

                    string fileName = input.File != null ? Environment.TickCount + "_" + System.IO.Path.GetFileName(input.File.FileName) : "";
                    if (fileName != "")
                    {
                        try
                        {
                            if( pre_fileName != "" )
                            {
                                System.IO.File.Delete(pre_fileName);
                            }

                            var path = System.IO.Path.Combine(Server.MapPath("~/App_Data/Uploads"), fileName);
                            input.File.SaveAs(path);
                        }
                        catch (Exception e)
                        {
                            ModelState.AddModelError("", "Ошибка загрузки файла " + e.Message);
                            GenerateProjectsList();
                            return View(input);
                        }
                    }

                    if (recipient != null)
                    {
                        if (prj == null)
                        {
                            Department dept = db.Departments.SingleOrDefault(x => x.Boss.Id == currentUser.Id);
                            if( dept == null )
                            {
                                ModelState.AddModelError("", "Пользователь не РП ");
                                GenerateProjectsList();
                                return View(input);
                            }
                            prj = db.Projects.Add(new Project() { Department = dept, Name = input.ProjectName });
                        }

                        request.AttachedFileName = fileName;
                        request.SumType = input.type;
                        request.RecipientUser = recipient;
                        request.AgreedPM = input.AgreedPM;
                        request.TimesOrSum = request.SumType == PaymentRequestType.Times ? input.Sum : PaymentRequest.RoundSum(input.Sum);
                        request.Project = prj;
                        if (!manager.IsInRole(currentUser.Id, SalaryRoles.Directors))
                        {
                            request.RequestState = input.SetStateWaitConfirm ? PaymentRequestState.WaitConfirm : PaymentRequestState.InProcess;
                        }
                        else
                        {
                            request.RequestState = input.SetStateCredited ? PaymentRequestState.Confirmed : PaymentRequestState.WaitConfirm;
                        }

                        if (request.RequestState == PaymentRequestState.WaitConfirm)
                        {
                            SendMailToDirector(request.AppointedUser.ShortName, request.Project.Name);
                        }


                        if ( !String.IsNullOrEmpty(input.Comments) )
                        {
                            db.RequestHistories.Add(new RequestHistory() { WhenPosted = DateTime.Now, Comments = input.Comments, Author = currentUser, Request = request });
                        }

                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                }
            }
            GenerateProjectsList();
            ModelState.AddModelError("", "Ошибка ввода");
            return View(input);
        }

        public FileResult Download(string file)
        {
            int p = file.IndexOf('_');
            string fn = p > 0 ? file.Substring(p + 1, file.Length - p - 1) : file;
            return File(System.IO.Path.Combine(Server.MapPath("~/App_Data/Uploads"), file), System.Net.Mime.MediaTypeNames.Application.Octet, fn );
        }

        void SendMailToProjectManager(string EmailTo, string ProjectName)
        {
            if( EmailTo != null )
            {
                string Body = "Здравствуйте." + Environment.NewLine + "На портале премий была рассмотрена Ваша заявка.";
                Body += Environment.NewLine + "Чтобы узнать результат, перейдите по ссылке http://" + Request.Url.Authority + "/PaymentRequests";

                SendMail(EmailTo, "Портал премий. Заявка по проекту " + ProjectName + ". Изменение состояния", Body);
            }
        }

        void SendMailToDirector(string AppointedUser)
        {
            ApplicationUser boss = GetFirstUserInRole("Directors");
            if (boss != null)
            {
                string Body = "Здравствуйте." + Environment.NewLine + "На портале премий требуется обработать поступившие заявки на начисление премии.";
                Body += Environment.NewLine + "Для просмотра заявок перейдите по ссылке http://" + Request.Url.Authority + "/PaymentRequests";

                if (boss.Email != null)
                {
                    SendMail(boss.Email, "Портал премий. Премия от " + AppointedUser + " по нескольким проектам ", Body);
                }
            }
        }
        void SendMailToDirector(string AppointedUser, string ProjectName)
        {
            ApplicationUser boss = GetFirstUserInRole("Directors");
            if (boss != null)
            {
                string Body = "Здравствуйте." + Environment.NewLine + "На портале премий требуется обработать поступившую заявку на начисление премии.";
                Body += Environment.NewLine + "Для просмотра заявок перейдите по ссылке http://" + Request.Url.Authority + "/PaymentRequests";

                if (boss.Email != null)
                {
                    SendMail(boss.Email, "Портал премий. Премия от " + AppointedUser + " по проекту " + ProjectName, Body);
                }
            }
        }

        static SmtpClient SmtpClient = null;
        
        void SendMail(string EmailTo, string Subject, string TextMessage)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(WebConfig.SmtpUserName));
                message.To.Add(new MailboxAddress(EmailTo));
                message.Subject = Subject;
                message.Body = new TextPart("plain")
                {
                    Text = TextMessage
                };
                
                if (SmtpClient == null)
                {
                    SmtpClient = new SmtpClient();
                    SmtpClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; };
                    SmtpClient.Connect(WebConfig.SmtpHost, WebConfig.SmtpPort);
                    SmtpClient.AuthenticationMechanisms.Remove("XOAUTH2");
                    SmtpClient.Authenticate(WebConfig.SmtpUserName, WebConfig.SmtpUserPass);
                }

                SmtpClient.Send(message);
                //SmtpClient.Disconnect(true);
            }
            catch (Exception e)
            {

            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult ChangeStateAll()
        {
            if (Request["PaymentRequestState_Modal"] == null)
            {
                ModelState.AddModelError("", "Ошибочный запрос");
                return RedirectToAction("Index");
            }
            try
            {
                PaymentRequestState old_state;
                PaymentRequestState new_state;

                switch (Request["PaymentRequestState_Modal"])
                {
                    case "WaitConfirm":
                        old_state = PaymentRequestState.InProcess;
                        new_state = PaymentRequestState.WaitConfirm;
                        break;
                    case "Confirmed":
                        old_state = PaymentRequestState.WaitConfirm;
                        new_state = PaymentRequestState.Confirmed;
                        break;
                    case "Rejected":
                        old_state = PaymentRequestState.WaitConfirm;
                        new_state = PaymentRequestState.Rejected;
                        break;
                    case "ReWorked":
                        old_state = PaymentRequestState.WaitConfirm;
                        new_state = PaymentRequestState.ReWorked;
                        break;
                    default:
                        return RedirectToAction("Index");
                }


                var currentUser = manager.FindById(User.Identity.GetUserId());

                var results = manager.IsInRole(currentUser.Id, "Directors") ? 
                    db.PaymentRequests.Where(x => x.AppointedUser.Id != currentUser.Id && x.RequestState == old_state).ToList() : 
                    db.PaymentRequests.Where(x => x.AppointedUser.Id == currentUser.Id && x.RequestState == old_state).ToList();

                string Comments = Request["Comments"] != null ? Request["Comments"].ToString().Trim() : "";

                results.ForEach(x => { x.RequestState = new_state; x.WhenStateChanged = DateTime.Now; });

                if (manager.IsInRole(currentUser.Id, "Directors") && Request["PaymentRequestState_Modal"] == "Confirmed")
                {
                    var res2 = db.PaymentRequests.Where(x => x.AppointedUser.Id == currentUser.Id && x.RequestState == old_state).ToList();
                    res2.ForEach(x => { x.RequestState = new_state; x.WhenStateChanged = DateTime.Now; });
                }


                if (!String.IsNullOrEmpty(Comments))
                {
                    foreach(PaymentRequest r in results)
                    {
                        db.RequestHistories.Add(new RequestHistory() { WhenPosted = DateTime.Now, Comments = Comments, Author = currentUser, Request = r });
                    }
                }
                
                db.SaveChanges();

                if (new_state == PaymentRequestState.WaitConfirm)
                {
                    SendMailToDirector(currentUser.ShortName);
                }
                else
                {
                    foreach (var item in results)
                    {
                        SendMailToProjectManager(item.AppointedUser.Email, item.Project.Name);
                    }
                }

                return RedirectToAction("Index");
            }
            catch (Exception e)
            {
                ModelState.AddModelError("", "Ошибка ввода данных " + e.Message);
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult ChangeState()
        {
            if ((Request["PaymentRequestId_Modal"] == null) || (Request["PaymentRequestState_Modal"] == null))
            {
                ModelState.AddModelError("", "Ошибочный запрос");
                return RedirectToAction("Index");
            }

            try
            {
                int id = int.Parse(Request["PaymentRequestId_Modal"]);
                PaymentRequestState state = (PaymentRequestState)byte.Parse(Request["PaymentRequestState_Modal"]);
                PaymentRequestState prev_state;

                PaymentRequest r = db.PaymentRequests.Find(id);
                if (r == null)
                {
                    ModelState.AddModelError("", "Заявка не найдена");
                    return RedirectToAction("Index");
                }

                prev_state = r.RequestState;
                r.RequestState = state;
                r.WhenStateChanged = DateTime.Now;
                

                if(Request["Comments"] != null)
                {
                    string Comments = Request["Comments"].ToString();
                    if (!String.IsNullOrEmpty(Comments))
                    {
                        var currentUser = manager.FindById(User.Identity.GetUserId());
                        db.RequestHistories.Add(new RequestHistory() { WhenPosted = DateTime.Now, Comments = Comments, Author = currentUser, Request = r });
                    }
                }

                db.SaveChanges();

                if (prev_state == PaymentRequestState.InProcess && state == PaymentRequestState.WaitConfirm)
                {
                    SendMailToDirector(r.AppointedUser.ShortName, r.Project.Name);
                }
                else if (prev_state == PaymentRequestState.WaitConfirm && state == PaymentRequestState.ReWorked)
                {
                    SendMailToProjectManager(r.AppointedUser.Email, r.Project.Name);
                }

                return RedirectToAction("Index");
            }
            catch(Exception e)
            {
                ModelState.AddModelError("", "Ошибка ввода данных " + e.Message);
                return RedirectToAction("Index");
            }

        }

        // GET: PaymentRequests/Delete/5
        [Authorize(Roles = "Administrators,Directors,Managers,Assistant")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PaymentRequest PaymentRequest = db.PaymentRequests.Find(id);
            if (PaymentRequest == null)
            {
                return HttpNotFound();
            }
            return View(PaymentRequest);
        }

        // POST: PaymentRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            PaymentRequest PaymentRequest = db.PaymentRequests.Find(id);

            db.RequestHistories.RemoveRange(db.RequestHistories.Where(x => x.Request.PaymentRequestId == id).ToList());
            db.SaveChanges();
            db.PaymentRequests.Remove(PaymentRequest);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
