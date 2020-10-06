using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SalaryApp.Models;
using OfficeOpenXml;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.IO;
using System.Web.Hosting;

namespace SalaryApp.Controllers
{

[Authorize(Roles = "Administrators,Directors")]
    public class PaymentsGroupsController : Controller
    {
        private AppDbContext db = new AppDbContext();
        private bool IsAddPaymentLine = false;

        // GET: PaymentsGroups
        public ActionResult Index()
        {
            Dictionary<int, double> TotalSum = new Dictionary<int, double>();
            var model = db.PaymentsGroups.OrderByDescending(x => x.State).OrderByDescending(x => x.WhenCreated).ToList();
            foreach (var item in model)
            {
                var l = db.UserPayments.Where(x => x.PaymentGroup.PaymentsGroupId == item.PaymentsGroupId).ToList();
                double s = l.Count != 0 ? l.Sum(x => x.Sum) : 0;
                TotalSum.Add(item.PaymentsGroupId, s);
            }
            ViewBag.TotalSum = TotalSum;
            return View(model);
        }

        public JsonResult UserBalances(string FullName, int PaymentsGroupId)
        {
            PaymentsGroup g = db.PaymentsGroups.Find(PaymentsGroupId);
            List<UserProjectBalance> list = new List<UserProjectBalance>();
            if (g != null)
            {
                var projects = (from p in g.UsersPayments select p.Project.ProjectId).ToList();
                var res = (from b in db.UserBalances where b.User.FullName == FullName && b.Sum > 0 && !projects.Contains(b.Project.ProjectId) select 
                       new UserProjectBalance { Name = b.Project.Name, ProjectId = b.Project.ProjectId, Sum = b.Sum });
                list = res.ToList();
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        PG_HeaderViewModel getPG_HeaderViewModel(PaymentsGroup paymentsGroup)
        {
            PG_HeaderViewModel Header = new PG_HeaderViewModel();
            Header.State = paymentsGroup.State;
            Header.WhenCreated = paymentsGroup.WhenCreated;
            Header.WhenPaidOut = paymentsGroup.WhenPaidOut;
            Header.UserClosed = paymentsGroup.UserClosed;
            Header.PaymentsGroupId = paymentsGroup.PaymentsGroupId;
            
            List<ApplicationUser> users = paymentsGroup.UsersPayments != null ? paymentsGroup.UsersPayments.Select(p => p.User).Distinct().ToList() : new List<ApplicationUser>();

            Header.UserNames =  users.Select(x=>x.ShortName == null ? x.UserName : x.ShortName).ToArray();
            Header.UserIds = users.Select(x => x.Id).ToArray();

            Header.Payments = new double[Header.UserIds.Length][];

            return Header;
        }
        PG_ListViewModel getPG_ListsViewModel(int? id)
        {
            PaymentsGroup paymentsGroup = db.PaymentsGroups.Find(id);
            return paymentsGroup == null ? null : getPG_ListsViewModel(paymentsGroup);
        }
        PG_ListViewModel getPG_ListsViewModel(PaymentsGroup paymentsGroup)
        {
            PG_ListViewModel model = new PG_ListViewModel();

            model.Header = getPG_HeaderViewModel(paymentsGroup);
            model.ProjectNames = new string[model.Header.UserIds.Length][];
            model.ProjectIds = new int[model.Header.UserIds.Length][];
            model.Header.Payments = new double[model.Header.UserIds.Length][];
            model.Header.RegularPayments = new double[model.Header.UserIds.Length][];
            model.Balances = new double[model.Header.UserIds.Length][];

            for (int i = 0; i < model.Header.UserNames.Length; i++)
            {
                var list = paymentsGroup.UsersPayments.Where(x => x.User.Id == model.Header.UserIds[i]).ToList();
                model.ProjectNames[i] = new string[list.Count];
                model.ProjectIds[i] = new int[list.Count];
                model.Header.Payments[i] = new double[list.Count];
                model.Header.RegularPayments[i] = new double[list.Count];
                model.Balances[i] = new double[list.Count];
                for (int j=0; j<list.Count;j++)
                {
                    model.ProjectNames[i][j] = list[j].Project.Name;
                    model.ProjectIds[i][j] = list[j].Project.ProjectId;
                    model.Header.Payments[i][j] = list[j].Sum - list[j].RegularPaymentSum; 
                    model.Header.RegularPayments[i][j] = list[j].RegularPaymentSum;
                }
                for(int j=0; j<model.ProjectIds[i].Length; j++)
                {
                    string uid = model.Header.UserIds[i];
                    int pid = model.ProjectIds[i][j];
                    var b = db.UserBalances.FirstOrDefault(x => x.User.Id == uid && x.Project.ProjectId == pid);
                    model.Balances[i][j] = b == null ? 0 : b.Sum;
                }
            }

            model.NewUserPaymentName = "";
            model.NewUserPaymenSum = null;
            return model;
        }


        PaymentsGroupViewModel getPaymentsGroupViewModel(int? id)
        {
            PaymentsGroup paymentsGroup = db.PaymentsGroups.Find(id);
            return paymentsGroup == null ? null : getPaymentsGroupViewModel(paymentsGroup);
        }
        PaymentsGroupViewModel getPaymentsGroupViewModel(PaymentsGroup paymentsGroup)
        {
            PaymentsGroupViewModel model = new PaymentsGroupViewModel();
            model.Header = getPG_HeaderViewModel(paymentsGroup);

            List<Project> Projects = paymentsGroup.UsersPayments.Select(n => n.Project).Distinct().ToList();
            model.ProjectsNames = Projects.Select(n => n.Name).ToArray();
            model.ProjectsIds = Projects.Select(n => n.ProjectId).ToArray();

            for (int i = 0; i < model.Header.UserIds.Length; i++)
            {
                model.Header.Payments[i] = new double[model.ProjectsIds.Length];
            }

            for (int i = 0; i < model.Header.UserNames.Length; i++)
            {
                Dictionary<int, double> up = paymentsGroup.UsersPayments.Where(x => x.User.Id == model.Header.UserIds[i]).ToDictionary(z => z.Project.ProjectId, z => z.Sum);
                for (int j = 0; j < model.ProjectsIds.Length; j++)
                {
                    model.Header.Payments[i][j] = up.ContainsKey(model.ProjectsIds[j]) ? up[model.ProjectsIds[j]] : -1;
                }
            }
            return model;
        }

        // GET: PaymentsGroups/Details/5
        public ActionResult CreateConfirm(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PG_ListViewModel model = getPG_ListsViewModel(id);
            if (model == null)
            {
                return HttpNotFound();
            }
            return View(model);
        }
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            db.UserPayments.RemoveRange(db.UserPayments.Where(x => x.PaymentGroup.PaymentsGroupId == id && x.Sum == 0));
            PG_ListViewModel model = getPG_ListsViewModel(id);
            if( model == null )
            {
                return HttpNotFound();
            }
            return View(model);
        }

        // GET: PaymentsGroups/Create
        public ActionResult Create()
        {
            return View();
        }


        public ActionResult ExportForm(int? id)
        {
            if (id != null)
            {
                RateUploadModel model = new RateUploadModel() { PaymentsGroupId = id };
                return View(model);
            }
            else
            {
                ModelState.AddModelError("", "Ошибка, не задан счет ");
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExportForm( RateUploadModel input)
        {
            if (input.PaymentsGroupId != null)
            {
                if (input.UploadFile != null)
                {
                    PaymentsGroupViewModel payments = getPaymentsGroupViewModel(input.PaymentsGroupId);
                    if (payments != null)
                    {
                        MemoryStream ms = new MemoryStream();
                        using (var package = new ExcelPackage(input.UploadFile.InputStream))
                        {
                            var Sheet = package.Workbook.Worksheets[1];
                            int lastRow = Sheet.Dimension.End.Row;
                            int lastCol = Sheet.Dimension.End.Column;

                            if (Sheet.AutoFilterAddress != null)
                            {
                                Sheet.Cells[Sheet.AutoFilterAddress.ToString()].AutoFilter = false;
                            }
                            try
                            {
                                Sheet.Cells["F:" + Sheet.Dimension.End.Address].Clear();
                            }
                            catch (Exception) { }

                            for(int j=0; j< payments.ProjectsNames.Length;j++)
                            {
                                Sheet.Cells[4, 6 + j].Value = payments.ProjectsNames[j];
                                Sheet.Cells[4, 6 + j].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                            }
                            Sheet.Cells[4, 6 + payments.ProjectsNames.Length].Value = "ВСЕГО";
                            var f = Sheet.Cells[4, 6 + payments.ProjectsNames.Length].Style.Font;
                            f.Bold = true;
                            Sheet.Cells[4, 6 + payments.ProjectsNames.Length].Style.Font = f;
                            Sheet.Cells[4, 6 + payments.ProjectsNames.Length].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);



                            int i = 5;
                            int count_sum_lines = 0;
                            do
                            {
                                if (Sheet.Cells[i, 1].Value != null)
                                {
                                    if (Sheet.Cells[i, 1].Value.ToString().Trim() != "ИТОГО")
                                    {
                                        int index = payments.PaymentsIndexByShortName(Sheet.Cells[i, 1].Value.ToString());
                                        if (index != -1)
                                        {
                                            for (int j = 0; j < payments.Header.Payments[index].Length; j++)
                                            {
                                                if (payments.Header.Payments[index][j] != -1)
                                                {
                                                    Sheet.Cells[i, 6 + j].Value = payments.Header.Payments[index][j];
                                                }
                                            }
                                        }
                                        Sheet.Cells[i, 6 + payments.ProjectsNames.Length].FormulaR1C1 = "SUM(RC[-"+ (payments.ProjectsNames.Length+1) + "]:RC[-1])";
                                        count_sum_lines++;

                                    }
                                    for(int j=0;j <= payments.ProjectsNames.Length;j++)
                                    {
                                        Sheet.Cells[i, 6 + j].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                                    }
                                }
                                i++;
                            } while (i <= lastRow);

                            Sheet.Cells[i - 1, 6 + payments.ProjectsNames.Length].FormulaR1C1 = "SUM(R[-1]C[0]:R[-"+ count_sum_lines + "]C[0])";

                            var range = Sheet.Cells["A4:" + Sheet.Cells[i, 6 + payments.ProjectsNames.Length].Address ];
                            range.AutoFilter = true;

                            package.SaveAs(ms);
                        }
                        ms.Position = 0;
                        return new FileStreamResult(ms, "application/xlsx")
                        {
                            FileDownloadName = "Виртуальный счет КБ Харьков от " + payments.Header.WhenCreated.ToString("dd.MM.yyyy") + ".xlsx"
                        };

                    }
                    else
                        ModelState.AddModelError("", "Ошибка, не найден счет ");
                }
                else
                    ModelState.AddModelError("", "Ошибка загрузки файла ");
            }
            else
                ModelState.AddModelError("", "Ошибка, не задан счет ");

            return View();
        }

        static List<string> UploadFormErros = new List<string>();
        public ActionResult UploadForm()
        {
            RateUploadModel model = new RateUploadModel() { SalaryRate = 30 };
            DateTime d_now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            List<RegularPayment> list = new List<RegularPayment>();
            foreach (var rp in db.RegularPayments)
            {
                DateTime d_from = rp.PayoutFrom == null ? new DateTime(DateTime.Now.Year - 10, DateTime.Now.Month, 1) :
                    new DateTime(rp.PayoutFrom.Value.Year, rp.PayoutFrom.Value.Month, 1);
                DateTime d_to = rp.PayoutTo == null ? new DateTime(DateTime.Now.Year + 10, DateTime.Now.Month, 1) :
                    new DateTime(rp.PayoutTo.Value.Year, rp.PayoutTo.Value.Month, 1);
                
                if (d_now >= d_from && d_now <= d_to)
                {
                    list.Add(rp);
                }
            }


            model.regularPayments = list.ToArray(); 
            if( model.regularPayments != null )
            {
                model.regularPaymentIds = (from p in model.regularPayments select p.RegularPaymentId).ToArray();
                model.accrueRegularPayments = Enumerable.Repeat<bool>(true, model.regularPayments.Length).ToArray();
            }
            foreach(string s in UploadFormErros)
            {
                ModelState.AddModelError("", s);
            }
            UploadFormErros.Clear();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadForm(RateUploadModel input)
        {
            Dictionary<string, double> stavki = new Dictionary<string, double>();

            if (ModelState.IsValid)
            {

                if (input.UploadFile != null)
                {
                    using (var package = new ExcelPackage(input.UploadFile.InputStream))
                    {
                        var Sheet = package.Workbook.Worksheets[1];
                        int lastRow = Sheet.Dimension.End.Row;
                        int lastCol = Sheet.Dimension.End.Column;
                        int i = 5;
                        do
                        {
                            if (Sheet.Cells[i, 1].Value != null && Sheet.Cells[i, 5].Value != null)
                            {
                                if (!String.IsNullOrEmpty(Sheet.Cells[i, 1].Value.ToString()))
                                {
                                    if (!String.IsNullOrEmpty(Sheet.Cells[i, 5].Value.ToString()))
                                    {
                                        double v;
                                        if (double.TryParse(Sheet.Cells[i, 5].Value.ToString(), out v))
                                        {
                                            stavki.Add(Sheet.Cells[i, 1].Value.ToString().Trim().Replace(". ", ".").Replace(". ",".").ToUpper(), v);
                                        }
                                    }
                                }
                            }
                            i++;
                        } while (i <= lastRow);

                        PaymentsGroup pg = new PaymentsGroup() { State = PaymentsGroupState.InProcess, WhenCreated = DateTime.Now, WhenPaidOut = DateTime.Now };
                        pg = db.PaymentsGroups.Add(pg);

                        foreach (KeyValuePair<string,double> s in stavki)
                        {
                            ApplicationUser user = db.Users.FirstOrDefault(x => x.ShortName.Replace(". ", ".").ToUpper() == s.Key.Replace(". ", ".").ToUpper());
                            if( user != null )
                            {
                                double limit = input.SalaryRate * s.Value / 100;
                                double c_sum = 0;
                                foreach(var b in user.UserBalances.OrderBy(x=>x.Sum).ToList())
                                {
                                    if ( b.Sum == 0 ) continue;
                                    UserPayment p = null;

                                    if (b.Sum > (limit - c_sum))
                                    {
                                        double rsum = PaymentRequest.TruncSum(limit - c_sum);
                                        if (((uint)rsum) == 0) continue;
                                        p = new UserPayment() {
                                            PaymentGroup = pg, Project = b.Project, User = user, Sum = rsum };

                                        p = db.UserPayments.Add(p);
                                        break;
                                    }
                                    else
                                    {
                                        double rsum = PaymentRequest.TruncSum(b.Sum);
                                        if (((uint)rsum) == 0) continue;
                                        p = new UserPayment() {
                                            PaymentGroup = pg, Project = b.Project, User = user, Sum = rsum };
                                        c_sum += p.Sum;
                                        p = db.UserPayments.Add(p);
                                    }

                                }

                            }
                            else 
                                ModelState.AddModelError("", "в БД не найден " + s.Key);
                        }

                        db.SaveChanges();

                        if (input.regularPaymentIds != null)
                        {
                            //  обработка регулярных платежей 
                            for (int l = 0; l < input.regularPaymentIds.Length; l++)
                            {
                                if (input.accrueRegularPayments[l])
                                {
                                    RegularPayment rp = db.RegularPayments.Find(input.regularPaymentIds[l]);
                                    if (rp != null)
                                    {
                                        // проверяем дату
                                        DateTime d_from = rp.PayoutFrom == null ? new DateTime(DateTime.Now.Year - 10, DateTime.Now.Month, 1) :
                                            new DateTime(rp.PayoutFrom.Value.Year, rp.PayoutFrom.Value.Month, 1);
                                        DateTime d_to = rp.PayoutTo == null ? new DateTime(DateTime.Now.Year + 10, DateTime.Now.Month, 1) :
                                            new DateTime(rp.PayoutTo.Value.Year, rp.PayoutTo.Value.Month, 1);
                                        DateTime d_now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                                        if (d_now >= d_from && d_now <= d_to)
                                        {
                                            UserPayment p = pg.UsersPayments.FirstOrDefault(x => x.Project.ProjectId == rp.Project.ProjectId && x.User.Id == rp.RecipientUser.Id);
                                            if (p == null)
                                            {
                                                p = db.UserPayments.Add(new UserPayment { PaymentGroup = pg, Project = rp.Project, User = rp.RecipientUser, Sum = 0 });
                                            }

                                            p.Sum += rp.Sum;
                                            p.RegularPaymentSum = rp.Sum;
                                            db.SaveChanges();
                                        }
                                    }
                                }
                            }
                        }

                        return RedirectToAction("CreateConfirm/" + pg.PaymentsGroupId);
                    }
                }
                else
                {
                    UploadFormErros.Add("Не выбран файл ставок");
                }
            }

            return RedirectToAction("UploadForm");
        }

        [HttpPost]
        [MultipleButton(Name = "action", Argument = "ConfirmCreate")]
        public ActionResult ConfirmCreate()
        {
            return RedirectToAction("Index");
        }

        [HttpPost]
        [MultipleButton(Name = "action", Argument = "CancelCreate")]
        public ActionResult CancelCreate(int? id)
        {
            return DeleteConfirmed(id);
        }

        [HttpPost]
        [MultipleButton(Name = "action", Argument = "CancelDelete")]
        public ActionResult CancelDelete()
        {
            return RedirectToAction("Index");
        }

        [HttpPost]
        [MultipleButton(Name = "action", Argument = "CancelEdit")]
        public ActionResult CancelEdit()
        {
            return RedirectToAction("Index");
        }

        [HttpPost]
        [MultipleButton(Name = "action", Argument = "ConfirmDelete")]
        public ActionResult ConfirmDelete(int? id)
        {
            return DeleteConfirmed(id);
        }

        static List<string> PG_EditErrors = new List<string>();

        // GET: PaymentsGroups/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            db.UserPayments.RemoveRange(db.UserPayments.Where(x => x.PaymentGroup.PaymentsGroupId == id && x.Sum == 0));
            db.SaveChanges();

            PG_ListViewModel model = getPG_ListsViewModel(id);
            if (model == null)
            {
                return HttpNotFound();
            }
            if( PG_EditErrors == null ) { PG_EditErrors = new List<string>(); }
            PG_EditErrors.Clear();
            model.Header.ShowEditable = true;
            return View("Edit",model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [MultipleButton(Name = "action", Argument = "SaveEdit")]
        public ActionResult SaveEdit(PG_ListViewModel input)
        {
            if (input != null)
            {
                if (input.Header != null)
                {
                    Edit(input);
                    db.UserPayments.RemoveRange(db.UserPayments.Where(x => x.PaymentGroup.PaymentsGroupId == input.Header.PaymentsGroupId && x.Sum == 0));
                    db.SaveChanges();
                    if (PG_EditErrors.Count == 0 && input.Header.State == PaymentsGroupState.PaidOut)
                    {
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        return View(input);
                    }
                }
            }
            ModelState.AddModelError("","Неверный запрос");
            return View("Edit", input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [MultipleButton(Name = "action", Argument = "AddPaymentLine")]
        public ActionResult AddPaymentLine(PG_ListViewModel input)
        {
            IsAddPaymentLine = true;

            PaymentsGroup g = db.PaymentsGroups.Find(input.Header.PaymentsGroupId);
            if (g != null)
            {
                if (!string.IsNullOrEmpty(input.NewUserPaymentName) && input.NewUserPaymentProject != null && input.NewUserPaymenSum != null)
                {
                    var b = db.UserBalances.SingleOrDefault(x => x.User.FullName == input.NewUserPaymentName && x.Project.ProjectId == input.NewUserPaymentProject);
                    if (b != null)
                    {
                        var pm = g.UsersPayments.FirstOrDefault(x => x.Project.ProjectId == input.NewUserPaymentProject);
                        if (pm == null)
                        {

                            if (b.Sum >= (int)input.NewUserPaymenSum && (int)input.NewUserPaymenSum != 0)
                            {
                                UserPayment p = new UserPayment()
                                {
                                    User = b.User,
                                    Project = b.Project,
                                    Sum = (double)input.NewUserPaymenSum,
                                    PaymentGroup = db.PaymentsGroups.Find(input.Header.PaymentsGroupId)
                                };
                                db.UserPayments.Add(p);
                                db.SaveChanges();

                                input = getPG_ListsViewModel(input.Header.PaymentsGroupId);
                            }
                            else
                            {
                                ModelState.AddModelError("", "Ошибка, сумма платежа превышает баланс или равна нулю");
                                input.NewUserPaymentName = "";
                                return View("Edit", input);
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("", "Ошибка, выплата по проекту уже есть в ведомости");
                            input.NewUserPaymentName = "";
                            return View("Edit", input);
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", "Ошибка, нет выплат по проекту");
                        input.NewUserPaymentName = "";
                        return View("Edit", input);
                    }
                }
            }
            input.NewUserPaymentName = "";
            return Edit(input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [MultipleButton(Name = "action", Argument = "Edit")]
        public ActionResult Edit(PG_ListViewModel model)
        {
            PG_EditErrors.Clear();
            bool was_apply = false;
            bool has_error = false;

            if (ModelState.IsValid)
            {

                PaymentsGroup paymentsGroup = db.PaymentsGroups.Find(model.Header.PaymentsGroupId);
                if (paymentsGroup != null)
                {

                    for (int i=0; i<model.Header.UserIds.Length; i++)
                    {
                        ApplicationUser user = db.Users.Find(model.Header.UserIds[i]);
                        if (user != null)
                        {
                            for (int j = 0; j < model.ProjectIds[i].Length; j++)
                            {
                                UserBalance user_b = user.UserBalances.FirstOrDefault(x => x.Project.ProjectId == model.ProjectIds[i][j]);
                                UserPayment user_p = paymentsGroup.UsersPayments.FirstOrDefault(x => x.Project.ProjectId == model.ProjectIds[i][j] && x.User.Id == model.Header.UserIds[i]);

                                if( user_b == null )
                                {
                                    user_b = db.UserBalances.Add(new UserBalance() { Project = db.Projects.Find(model.ProjectIds[i][j]), Sum = 0, User = user });
                                    db.SaveChanges();
                                }

                                if ( user_b != null && user_p != null )
                                {
                                    if(user_b.Sum >= model.Header.Payments[i][j] )
                                    {
                                        if (model.Header.State == PaymentsGroupState.PaidOut)
                                        {
                                            user_b.Sum -= model.Header.Payments[i][j];
                                        }
                                        user_p.Sum = model.Header.Payments[i][j] + model.Header.RegularPayments[i][j];
                                        user_p.RegularPaymentSum = model.Header.RegularPayments[i][j];
                                        was_apply = true;
                                    }
                                    else
                                    {
                                        has_error = true;
                                        PG_EditErrors.Add("Сумма платежа первышает остаток, Пользователь " + model.Header.UserNames[i] + " Проект " + model.ProjectNames[i][j]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            has_error = true;
                            PG_EditErrors.Add("Пользователь " + model.Header.UserNames[i] + " не найден");
                        }
                    }


                    if (was_apply && !has_error)
                    {
                        var UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                        string uid = User.Identity.GetUserId();
                        ApplicationUser admin = db.Users.FirstOrDefault(x=>x.Id == uid);
                        if (model.Header.State == PaymentsGroupState.PaidOut)
                        {
                            paymentsGroup.UserClosed = admin;
                            paymentsGroup.State = PaymentsGroupState.PaidOut;
                            paymentsGroup.WhenPaidOut = DateTime.Now;


                            // создаем список рассылки
                            foreach (var p in paymentsGroup.UsersPayments)
                            {
                                db.JobForMailings.Add(new JobForMailing() { UserPayment = p });
                            }

                            HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => new MailSendWorker().SendPaidOut(cancellationToken));

                        }

                        db.SaveChanges();

                        foreach(var err in PG_EditErrors)
                        {
                            ModelState.AddModelError("", err);
                        }

                        if (IsAddPaymentLine)
                        {
                            return RedirectToAction("Edit/" + model.Header.PaymentsGroupId);
                        }
                        else
                            return RedirectToAction("Index");
                    }
                    else
                    {
                        if( !was_apply )
                            PG_EditErrors.Add("В счете не найдено корректных платежей");
                    }
                }
                else
                    ModelState.AddModelError("", "Счет не найден, id=" + model.Header.PaymentsGroupId);
            }
            else
                ModelState.AddModelError("", "Ошибка ввода ");

            if (!was_apply || has_error)
            {
                foreach (var err in PG_EditErrors)
                {
                    ModelState.AddModelError("", err);
                }
            }

            
            
            return View(model);
        }

        public ActionResult EditByTable(PaymentsGroupViewModel model)
        {
            if (ModelState.IsValid)
            {
                PaymentsGroup paymentsGroup = db.PaymentsGroups.Find(model.Header.PaymentsGroupId);
                if (paymentsGroup != null)
                {
                    bool was_apply = false;
                    bool has_error = false;
                    for (int i = 0; i < model.Header.UserIds.Length; i++)
                    {
                        ApplicationUser user = db.Users.Find(model.Header.UserIds[i]);
                        if (user != null)
                        {
                            for (int j = 0; j < model.ProjectsIds.Length; j++)
                            {
                                if (model.Header.Payments[i][j] == -1) continue;
                                UserBalance user_b = user.UserBalances.FirstOrDefault(x => x.Project.ProjectId == model.ProjectsIds[j]);
                                UserPayment user_p = paymentsGroup.UsersPayments.FirstOrDefault(x => x.Project.ProjectId == model.ProjectsIds[j] && x.User.Id == model.Header.UserIds[i]);
                                if (user_b != null && user_p != null)
                                {
                                    if (user_b.Sum >= model.Header.Payments[i][j])
                                    {
                                        if (model.Header.State == PaymentsGroupState.PaidOut)
                                        {
                                            user_b.Sum -= model.Header.Payments[i][j];
                                        }
                                        user_p.Sum = model.Header.Payments[i][j];
                                        was_apply = true;
                                    }
                                    else
                                    {
                                        has_error = true;
                                        ModelState.AddModelError("", "Сумма платежа первышает остаток, Пользователь " + model.Header.UserNames[i] + " Проект " + model.ProjectsNames[j]);
                                    }
                                }
                                else
                                {
                                    has_error = true;
                                    if (user_b == null)
                                        ModelState.AddModelError("", "Не найден счет, Пользователь " + model.Header.UserNames[i] + " Проект " + model.ProjectsNames[j]);
                                    if (user_p == null)
                                        ModelState.AddModelError("", "Не найден платеж, Пользователь " + model.Header.UserNames[i] + " Проект " + model.ProjectsNames[j]);
                                }
                            }
                        }
                        else
                        {
                            has_error = true;
                            ModelState.AddModelError("", "Пользователь " + model.Header.UserNames[i] + " не найден");
                        }
                    }


                    if (was_apply && !has_error)
                    {
                        var UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
                        string uid = User.Identity.GetUserId();
                        ApplicationUser admin = db.Users.FirstOrDefault(x => x.Id == uid);
                        if (model.Header.State == PaymentsGroupState.PaidOut)
                        {
                            paymentsGroup.UserClosed = admin;
                            paymentsGroup.State = PaymentsGroupState.PaidOut;
                            paymentsGroup.WhenPaidOut = DateTime.Now;
                        }

                        db.SaveChanges();

                        return RedirectToAction("Index");
                    }
                    else
                    {
                        if (!was_apply)
                            ModelState.AddModelError("", "В счете не найдено корректных платежей");
                    }
                }
                else
                    ModelState.AddModelError("", "Счет не найден, id=" + model.Header.PaymentsGroupId);
            }
            else
                ModelState.AddModelError("", "Ошибка ввода ");

            return View(model);
        }
        // GET: PaymentsGroups/Delete/5
        public ActionResult Delete(int? id)
        {

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PG_ListViewModel model = getPG_ListsViewModel(id);
            if (model == null)
            {
                return HttpNotFound();
            }
            return View(model);
        }

        // POST: PaymentsGroups/Delete/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            PaymentsGroup paymentsGroup = db.PaymentsGroups.Find(id);
            db.UserPayments.RemoveRange(db.UserPayments.Where(x => x.PaymentGroup.PaymentsGroupId == id));
            db.PaymentsGroups.Remove(paymentsGroup);
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
