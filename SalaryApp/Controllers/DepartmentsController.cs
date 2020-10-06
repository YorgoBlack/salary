using System;
using System.Collections.Generic;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Drawing;
using System.DirectoryServices;
using SalaryApp.Models;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using PagedList;
using System.IO;

namespace SalaryApp.Controllers
{

    [Authorize]
    public class DepartmentsController : Controller
    {
        private AppDbContext db;
        private UserManager<ApplicationUser> manager;

        public DepartmentsController()
        {
            db = new AppDbContext();
            manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
        }

        // GET: Departments
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult Index()
        {
            var currentUser = manager.FindById(User.Identity.GetUserId());
            ApplicationUser boss = GetFirstUserInRole("Directors");
            ApplicationUser admin = GetFirstUserInRole("Administrators");
            DepartmentsList l = new DepartmentsList();
            l.depts = db.Departments.ToList();
            if (manager.IsInRole(currentUser.Id, "Administrators") || manager.IsInRole(currentUser.Id, "Directors"))
            {
                l.HideSalaryUsers = (from p in db.Users where p.HideSalary select new UsersExchangeData { FullName = p.FullName, UserLogin = p.UserName }).ToList();
            }
            else
                l.HideSalaryUsers = null;
            

            l.Director = boss == null ? "" : boss.FullName;
            l.Administrator = admin == null ? "" : admin.FullName;
            l.NewHideSalaryUser = null;
            return View(l);
        }

        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult UploadForm()
        {
            return View();
        }

        public ActionResult ShowUserSalary(string UserName)
        {
            ApplicationUser u = db.Users.SingleOrDefault(x => x.UserName == UserName);
            if (u != null)
            {
                u.HideSalary = false;
                db.SaveChanges();
            }
            return RedirectToAction("Index", "Departments");
        }

        public ActionResult HideUserSalary(DepartmentsList l)
        {
            ApplicationUser u = db.Users.SingleOrDefault(x => x.FullName == l.NewHideSalaryUser);
            if( u != null )
            {
                u.HideSalary = true;
                db.SaveChanges();
            }
            return RedirectToAction("Index","Departments");
        }

        private ApplicationUser GetFirstUserInRole(string roleName)
        {
            var RoleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            var user = RoleManager.FindByName(roleName).Users.FirstOrDefault();
            return user == null ? null : db.Users.Find(user.UserId);
        }

        public ActionResult ChangeDirector()
        {
            UsersListItemModel model = new UsersListItemModel();
            return View(model);
        }

        public ActionResult ChangeAdminstrator()
        {
            UsersListItemModel model = new UsersListItemModel();
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeDirector(UsersListItemModel model)
        {
            return ChangeUserRole(model, SalaryRoles.Directors);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeAdminstrator(UsersListItemModel model)
        {
            return ChangeUserRole(model, SalaryRoles.Administrators);
        }

        private ActionResult ChangeUserRole(UsersListItemModel model, string RoleName)
        {
            var user = db.Users.FirstOrDefault(u => u.FullName == model.Name);
            var prev = GetFirstUserInRole(RoleName);
            string err;
            bool IsMoreOnceUser = false;
            var currentUser = manager.FindById(User.Identity.GetUserId());

            if (user != null)
            {
                if (prev != null)
                {
                    manager.RemoveFromRole(prev.Id, RoleName);
                    if (prev.UserName == "root")
                    {
                        db.Users.Remove(prev);
                        db.SaveChanges();
                        if( currentUser.UserName == "root")
                        {
                            HttpContext.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
                if (!manager.IsInRole(user.Id, RoleName))
                {
                    manager.AddToRole(user.Id, RoleName);
                }
            }
            else
            {
                user = SingleOrDefaultDomainUser(model.Name, out err, out IsMoreOnceUser);
                if (user == null)
                {
                    ModelState.AddModelError("", "Пользователь не найден");
                    return View();
                }
                else
                {
                    var chk = db.Users.FirstOrDefault(x => x.UserName == user.UserName);
                    user = chk == null ? db.Users.Add(user) : chk;
                    db.SaveChanges();
                    manager.AddToRole(user.Id, RoleName);

                    if (prev != null)
                    {
                        manager.RemoveFromRole(prev.Id, RoleName);
                    }
                }
            }

            if (manager.IsInRole(currentUser.Id, SalaryRoles.Directors) || manager.IsInRole(currentUser.Id, SalaryRoles.Administrators))
            {
                return RedirectToAction("Index");
            }
            else
            {
                HttpContext.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                return RedirectToAction("Index", "Home");
            }
        }


        static public IEnumerable<UsersExchangeData> getDomainUsers()
        {
            List<UsersExchangeData> users = new List<UsersExchangeData>();
            string path = "LDAP://" + AccountController.Domain + "/" + AccountController.Container;
            using (DirectoryEntry root = new DirectoryEntry(path, AccountController.sServiceUser, AccountController.sServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(root))
                {
                    searcher.PropertiesToLoad.Add("SamAccountName");
                    searcher.PropertiesToLoad.Add("DisplayName");
                    foreach (System.DirectoryServices.SearchResult resEnt in searcher.FindAll())
                    {
                        DirectoryEntry de = resEnt.GetDirectoryEntry();
                        if (de.Properties["SamAccountName"].Value != null && de.Properties["DisplayName"].Value != null)
                        {
                            string name = de.Properties["DisplayName"].Value.ToString();
                            string lg = de.Properties["SamAccountName"].Value.ToString();

                            if ( lg.Contains("prokhor"))
                            {
                                string ml = de.Properties["mail"].Value.ToString();
                            }
                            if (Regex.IsMatch(name, @"\p{IsCyrillic}"))
                            {
                                users.Add(new UsersExchangeData()
                                {
                                    UserLogin = de.Properties["SamAccountName"].Value.ToString(),
                                    FullName = name,
                                });
                            }
                        }
                    }
                }
            }
            return users;
        }

        static string GetCellValueFromPossiblyMergedCell(ExcelWorksheet wks, int row, int col)
        {
            int x;
            return GetCellValueFromPossiblyMergedCell(wks, row, col, out x);
        }
        static string GetCellValueFromPossiblyMergedCell(ExcelWorksheet wks, int row, int col, out int height)
        {
            var cell = wks.Cells[row, col];
            if (cell.Merge)
            {
                var mergedId = wks.MergedCells[row, col];
                height = wks.Cells[mergedId].Rows;
                return wks.Cells[mergedId].First().Value != null ? wks.Cells[mergedId].First().Value.ToString() : "";
            }
            else
            {
                height = 1;
                return cell.Value != null ? cell.Value.ToString() : "";
            }
        }

        public static bool TryNormalyzeDomainName(string full_name, out string[] fio)
        {
            fio = new string[2] { "", "" };
            string[] s = Regex.Replace(full_name, @"\s+", " ").Split(' ');
            
            if (s.Length > 2)
            {
                fio[0] = s[2];
                fio[0] += " " + s[0].Substring(0, 1) + ".";
                fio[0] += s[1].Substring(0, 1) + ".";
                fio[0] = fio[0].Trim().Replace(". ", ".").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").ToUpper();

                fio[0] = fio[0].Replace("Ё", "Е");

                fio[1] = s[0];
                fio[1] += " " + s[1].Substring(0, 1) + ".";
                fio[1] += s[2].Substring(0, 1) + ".";
                fio[1] = fio[1].Trim().Replace(". ", ".").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").ToUpper().Replace("Ё", "Е");

                fio[1] = fio[1].Replace("Ё", "Е");

                return true;
            }
            else
            {
                return false;
            }
        }

        static public UsersExchangeData CheckDomainUserExists(string name, IEnumerable<UsersExchangeData> users)
        {
            UsersExchangeData ret = null;
            string b = "";
            foreach (UsersExchangeData user in users)
            {
                string[] fio;
                if( TryNormalyzeDomainName(user.FullName, out fio))
                { 
                    
                    if ( fio.Contains( name.Trim().Replace(". ", ".").Replace("  "," ").Replace("  ", " ").Replace("  ", " ").ToUpper().Replace("Ё", "Е")) )
                    {
                        ret = user;
                        break;
                    }
                }
                b += user.FullName + ";";
            }
            
            return ret;
        }

        private static decimal GetExcelDecimalValueForDate(DateTime date)
        {
            DateTime start = new DateTime(1900, 1, 1);
            TimeSpan diff = date - start;
            return diff.Days + 2;
        }

        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult Export(HttpPostedFileBase upload)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                using (var package = new ExcelPackage())
                {
                    ExcelWorksheet ws = package.Workbook.Worksheets.Add("Выплаты");
                    var results = (from u in db.Users select u);
                    List<ApplicationUser> users = new List<ApplicationUser>();
                    users = results.Where(x=>x.UserName != "root").OrderBy(x => x.Department.Name).ToList();
                    UserSalaryTable model = HomeController.BuildSalaryTable(users);
                    
                    ws.Cells[1, 1].Value = "ФИО";       ws.Column(1).Width = 25;
                    ws.Cells[1, 2].Value = "Ячейка";    ws.Column(2).Width = 12;
                    ws.Cells[1, 3].Value = "Лимит";     ws.Column(3).Width = 7;
                    ws.Cells[1, 4].Value = "Проект";    ws.Column(4).Width = 15;
                    ws.Cells[1, 5].Value = "Сумма";     ws.Column(5).Width = 10;
                    ws.Cells[1, 6].Value = "Остаток";   ws.Column(6).Width = 10;

                    for (int i = 1; i <= 6; i++)
                    {
                        ws.Column(i).Style.HorizontalAlignment = i <= 4 ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Left : OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        ws.Column(i).Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    }

                    for (int i=0; i<model.PaymentsGroupsColumns.Count;i++)
                    {
                        ws.Cells[1, i + 7].Style.Numberformat.Format = "mmm-yy";
                        ws.Cells[1, i + 7].Value = GetExcelDecimalValueForDate(model.PaymentsGroupsColumns[i]); //model.PaymentsGroupsColumns[i].ToString("MM-yyyy");
                        ws.Column(i + 7).Width = 7;
                        ws.Column(i+7).Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                        ws.Column(i+7).Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    }

                    ws.Row(1).Style.Font.Size = 10;
                    ws.Row(1).Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Row(1).Style.Fill.BackgroundColor.SetColor(Color.Black);
                    ws.Row(1).Style.Font.Color.SetColor(Color.White);

                    int index = 2;
                    for(int i=0; i < model.Users.Count; i++)
                    {
                        int prj_cnt = model.Users[i].ProjectsBalances.Count();
                        ws.Cells[index + i, 1].Value = users[i].ShortName == "" ? users[i].UserName : users[i].ShortName;
                        ws.Cells[index + i, 1].Style.WrapText = true;
                        ws.Cells[index + i, 2].Value = users[i].Department == null ? "Не задано" : users[i].Department.Name;
                        ws.Cells[index + i, 2].Style.WrapText = true;
                        ws.Cells[index + i, 3].Value = " ";
                        for(int j=0; j < prj_cnt; j++ )
                        {

                            ws.Cells[index + i + 2*j, 4].Value = model.Users[i].ProjectsBalances.ElementAt(j).ProjectName;
                            ws.Cells[index + i + 2*j, 4].Style.WrapText = true;
                            ws.Cells[index + i + 2*j + 1, 4].Value = "";

                            int credit = (int)model.Users[i].ProjectsBalances.ElementAt(j).Balance;
                            int payout = (int)model.Users[i].ProjectsBalances.ElementAt(j).PaidsOut.Sum(x => x.Value);
                            if ( (credit+ payout) > 0 )
                            {
                                ws.Cells[index + i + 2*j, 5].Value = credit + payout;
                            }
                            if (credit != 0)
                            {
                                ws.Cells[index + i + 2 * j, 6].Value = credit;
                            }

                            ws.Cells[index + i + 2 * j, 6].FormulaR1C1 = "RC[-1] - SUM(RC[1]:RC[" + model.PaymentsGroupsColumns.Count + "])";
                            ws.Cells[index + i + 2 * j + 1, 6].FormulaR1C1 = "R[-1]C[-1] - SUM(RC[1]:RC[" + model.PaymentsGroupsColumns.Count + "])";

                            
                            for (int l = 0; l < model.PaymentsGroupsColumns.Count; l++)
                            {
                                if( model.Users[i].ProjectsBalances.ElementAt(j).PaidsOut.ContainsKey(model.PaymentsGroupsColumns[l]) )
                                {
                                    ws.Cells[index + i + 2*j, l + 7].Value = model.Users[i].ProjectsBalances.ElementAt(j).PaidsOut[model.PaymentsGroupsColumns[l]];
                                    ws.Cells[index + i + 2*j + 1, l + 7].Value = model.Users[i].ProjectsBalances.ElementAt(j).PaidsOut[model.PaymentsGroupsColumns[l]];
                                }
                                else if( credit > 0 )
                                {
                                    ws.Cells[index + i + 2 * j, l + 7].Value = credit;
                                    credit = 0;
                                }
                            }
                            ws.Cells[index + i + 2 * j, 3, index + i + 2 * j + 1, 3].Merge = true;
                            ws.Cells[index + i + 2 * j, 4, index + i + 2 * j + 1, 4].Merge = true;
                            ws.Cells[index + i + 2 * j, 5, index + i + 2 * j + 1, 5].Merge = true;
                        }
                        if (prj_cnt > 0 )
                        {
                            ws.Cells[index + i + 2 * prj_cnt, 4].Value = "Итого,мес";
                            ws.Cells[index + i + 2 * prj_cnt, 4].Style.Font.Bold = true;
                            ws.Cells[index + i + 2 * prj_cnt, 4].Style.Font.Italic = true;
                            ws.Cells[index + i, 1, index + i + 2 * prj_cnt, 1].Merge = true;
                            ws.Cells[index + i, 2, index + i + 2 * prj_cnt, 2].Merge = true;
                            ws.Cells[index + i, 3, index + i + 2 * prj_cnt, 3].Merge = true;
                        }

                        index += prj_cnt > 0 ? 2* prj_cnt : 0;

                    }

                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                    //
                    //
                    //
                    ws = package.Workbook.Worksheets.Add("Проекты");
                    index = 2;
                    foreach(var item in db.Projects.ToList() )
                    {
                        if (!string.IsNullOrEmpty(item.Name))
                        {
                            ws.Cells[index, 1].Value = item.Department.Name;
                            ws.Cells[index, 2].Value = item.Name;
                        }
                        index++;
                    }
                    ws.Column(1).Width = 30;
                    ws.Column(2).Width = 100;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                    //
                    //
                    //

                    ws = package.Workbook.Worksheets.Add("Сотрудники");
                    ApplicationUser boss = GetFirstUserInRole("Directors");
                    ApplicationUser admin = GetFirstUserInRole("Administrators");
                    ws.Cells[1, 1].Value = "Директор";
                    ws.Cells[2, 1].Value = boss != null ? boss.ShortName != "" ? boss.ShortName : boss.UserName : "";
                    ws.Cells[1, 3].Value = "Админ";
                    ws.Cells[2, 3].Value = admin != null ? admin.ShortName != "" ? admin.ShortName : admin.UserName : "";
                    index = 4;
                    foreach (var item in db.Departments.ToList() )
                    {
                        ws.Cells[index, 1].Value = item.Name;
                        ws.Cells[index, 2].Value = item.Boss != null ? item.Boss.ShortName != "" ? item.Boss.ShortName : item.Boss.UserName : "";
                        ws.Cells[index, 3].Value = item.Assistant != null ? item.Assistant.ShortName != "" ? item.Assistant.ShortName : item.Assistant.UserName : "";
                        index++;
                    }
                    ws.Column(1).Width = 30;
                    ws.Column(2).Width = 30;
                    ws.Column(3).Width = 30;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                    //
                    //
                    //
                    ws = package.Workbook.Worksheets.Add("Регулярные выплаты");
                    var regpayments = (from p in db.RegularPayments select p);
                    ws.Cells[1, 1].Value = "ФИО"; ws.Column(1).Width = 25;
                    ws.Cells[1, 2].Value = "Ячейка"; ws.Column(2).Width = 12;
                    ws.Cells[1, 3].Value = "Проект"; ws.Column(3).Width = 30;
                    ws.Cells[1, 4].Value = "Начало"; ws.Column(4).Width = 20;
                    ws.Cells[1, 5].Value = "Завершение"; ws.Column(4).Width = 20;
                    ws.Cells[1, 6].Value = "Сумма"; ws.Column(5).Width = 15;

                    index = 2;
                    foreach (var item in regpayments)
                    {
                        ws.Cells[index, 1].Value = item.RecipientUser.ShortName == null ? 
                            (item.RecipientUser.FullName == null ? item.RecipientUser.UserName : item.RecipientUser.FullName) : item.RecipientUser.ShortName;
                        ws.Cells[index, 2].Value = item.RecipientUser.Department == null ? "" : item.RecipientUser.Department.Name;
                        ws.Cells[index, 3].Value = item.Project.Name;
                        if(item.PayoutFrom == null )
                        {
                            ws.Cells[index, 4].Value = "";
                        }
                        else
                        {
                            ws.Cells[index, 4].Style.Numberformat.Format = "mmm-yy";
                            ws.Cells[index, 4].Value = GetExcelDecimalValueForDate((DateTime)item.PayoutFrom);
                        }
                        if (item.PayoutTo == null)
                        {
                            ws.Cells[index, 5].Value = "";
                        }
                        else
                        {
                            ws.Cells[index, 5].Style.Numberformat.Format = "mmm-yy";
                            ws.Cells[index, 5].Value = GetExcelDecimalValueForDate((DateTime)item.PayoutTo);
                        }
                        ws.Cells[index, 6].Value = item.Sum;
                        index++;
                    }

                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    ws.Cells[1, 1, ws.Dimension.End.Row, ws.Dimension.End.Column].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                    package.SaveAs(ms);
                }

                ms.Position = 0;
                return new FileStreamResult(ms, "application/xlsx")
                {
                    FileDownloadName = "Настройки КБ Харьков.xlsx"
                };

            }
            catch (Exception e)
            {
                ModelState.AddModelError("", "Ошибка экспорта " + e.Message);
            }

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult ImportForm()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ImportForm(HttpPostedFileBase upload) // импорт платежей и настроек
        {
            try
            {
                var currentUser = manager.FindById(User.Identity.GetUserId());
                IEnumerable<UsersExchangeData> users_all = getDomainUsers();
                UploadResultModel model = new UploadResultModel() { HasError = false, LoseUsers = new List<string>(), NewUsers = new List<string>() };


                using (var package = new ExcelPackage(upload.InputStream))
                {
                    var Sheet = package.Workbook.Worksheets[1];

                    db.UserBalances.RemoveRange(db.UserBalances);
                    db.UserPayments.RemoveRange(db.UserPayments);
                    db.ImportUserPayments.RemoveRange(db.ImportUserPayments);
                    db.ImportUsers.RemoveRange(db.ImportUsers);
                    db.PaymentsGroups.RemoveRange(db.PaymentsGroups);
                    db.PaymentRequests.RemoveRange(db.PaymentRequests);
                    db.RequestHistories.RemoveRange(db.RequestHistories);
                    

                    db.Departments.RemoveRange(db.Departments);
                    db.Projects.RemoveRange(db.Projects);

                    db.RegularPayments.RemoveRange(db.RegularPayments);

                    var list = db.Users.ToList();
                    foreach(var u in list)
                    {
                        if( !(manager.IsInRole(u.Id,SalaryRoles.Administrators) || manager.IsInRole(u.Id, SalaryRoles.Directors)) )
                        {
                            var roles = db.Users.Find(u.Id).Roles;
                            if( roles != null && roles.Count > 0 )
                            {
                                roles.Clear();
                            }
                            manager.AddToRole(u.Id, SalaryRoles.Members);
                        }
                    }
                    db.SaveChanges();

                    model = ImportPayments(Sheet, users_all, currentUser); // импорт выплат
                    db.SaveChanges();

                    Sheet = package.Workbook.Worksheets[2]; // импорт таблицы направления - проекты
                    for (int i = 0; i < Sheet.Dimension.End.Row; i++)
                    {
                        if (Sheet.Cells[i + 2, 1].Value != null && Sheet.Cells[i + 2, 2].Value != null)
                        {
                            string str = Sheet.Cells[i + 2, 1].Value.ToString();
                            var d = db.Departments.SingleOrDefault(x => x.Name == str);
                            if (d == null)
                            {
                                d = db.Departments.Add(new Department() { Name = str });
                                db.SaveChanges();
                            }

                            str = Sheet.Cells[i + 2, 2].Value.ToString();
                            var p = db.Projects.SingleOrDefault(x => x.Name == str);
                            if( p == null )
                            {
                                p = db.Projects.Add(new Project() { Name = str, ProjectState = ProjectActiveState.Active });
                                db.SaveChanges();
                            }

                            p.Department = d;
                        }
                    }
                    db.SaveChanges();

                    Sheet = package.Workbook.Worksheets[3]; // импорт таблицы настройки
                    for (int i = 0; i < Sheet.Dimension.End.Row; i++)
                    {
                        if (Sheet.Cells[i + 4, 1].Value != null && (Sheet.Cells[i + 4, 2].Value != null || Sheet.Cells[i + 4, 3].Value != null) )
                        {
                            string str = Sheet.Cells[i + 4, 1].Value.ToString();
                            var d = db.Departments.SingleOrDefault(x => x.Name == str);
                            if (d == null)
                            {
                                d = db.Departments.Add(new Department() { Name = str });
                                db.SaveChanges();
                            }

                            if ( Sheet.Cells[i + 4, 2].Value != null )
                            {
                                str = Sheet.Cells[i + 4, 2].Value.ToString();
                                var boss = db.Users.SingleOrDefault(x => x.ShortName == str);
                                if( boss != null )
                                {
                                    d.Boss = boss;
                                    if( !manager.IsInRole(boss.Id,SalaryRoles.Managers) )
                                    {
                                        manager.AddToRole(boss.Id, SalaryRoles.Managers);
                                        db.SaveChanges();
                                    }
                                }
                            }
                            if (Sheet.Cells[i + 4, 3].Value != null)
                            {
                                str = Sheet.Cells[i + 4, 3].Value.ToString();
                                var assist = db.Users.SingleOrDefault(x => x.ShortName == str);
                                if (assist != null)
                                {
                                    d.Assistant = assist;
                                    if (!manager.IsInRole(assist.Id, SalaryRoles.Assistant))
                                    {
                                        manager.AddToRole(assist.Id, SalaryRoles.Assistant);
                                        db.SaveChanges();
                                    }
                                }
                            }
                        }
                    }
                    db.SaveChanges();

                    if (package.Workbook.Worksheets.Count > 3)
                    {
                        Sheet = package.Workbook.Worksheets[4]; // импорт регулярных выплат
                        for (int i = 2; i <= (Sheet.Dimension.End.Row + 1); i++)
                        {
                            if (Sheet.Cells[i, 1].Value != null)
                            {
                                string username = Sheet.Cells[i, 1].Value.ToString();
                                UsersExchangeData userdata = CheckDomainUserExists(username, users_all);
                                ApplicationUser user = null;
                                if (userdata != null) // если пользователь найден в домене
                                {
                                    // создаем пользователя, если не существует
                                    user = manager.FindByName(userdata.UserLogin);
                                    if (user == null)
                                    {
                                        user = new ApplicationUser();
                                        user.UserName = userdata.UserLogin;
                                        user.FullName = userdata.FullName;
                                        user.Email = "";
                                        user.ShortName = username;
                                        var res = manager.Create(user);
                                        if (res.Succeeded)
                                        {
                                            manager.AddToRole(user.Id, "Members");
                                        }
                                        model.NewUsers.Add(username);
                                    }
                                    else
                                    {
                                        user.ShortName = username;
                                    }
                                    db.SaveChanges();

                                }

                                if (Sheet.Cells[i, 3].Value != null)
                                {
                                    string prjname = Sheet.Cells[i, 3].Value.ToString();
                                    Project project = db.Projects.SingleOrDefault(x => x.Name == prjname);
                                    if (project != null)
                                    {
                                        if (Sheet.Cells[i, 6].Value != null)
                                        {
                                            int sum;
                                            if (int.TryParse(Sheet.Cells[i, 6].Value.ToString(), out sum))
                                            {
                                                RegularPayment rp = new RegularPayment() { RecipientUser = user, Project = project, Sum = sum };
                                                DateTime dt;
                                                if (Sheet.Cells[i, 4].Value != null)
                                                {
                                                    if (DateTime.TryParse(Sheet.Cells[i, 4].Value.ToString(), out dt))
                                                    {
                                                        rp.PayoutFrom = dt;
                                                    }
                                                }
                                                if (Sheet.Cells[i, 5].Value != null)
                                                {
                                                    if (DateTime.TryParse(Sheet.Cells[i, 5].Value.ToString(), out dt))
                                                    {
                                                        rp.PayoutTo = dt;
                                                    }
                                                }
                                                if (rp.PayoutFrom != null || rp.PayoutTo != null)
                                                {
                                                    db.RegularPayments.Add(rp);

                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        db.SaveChanges();
                    }
                }

            }
            catch (Exception e)
            {
                ModelState.AddModelError("", "Ошибка импорта " + e.Message);
                return View();
            }

            return RedirectToAction("Index");
        }

        UploadResultModel ImportPayments(ExcelWorksheet Sheet, IEnumerable<UsersExchangeData> users_all, ApplicationUser currentUser)
        {
            UploadResultModel model = new UploadResultModel() { HasError = false, LoseUsers = new List<string>(), NewUsers = new List<string>() };
            int lastRow = Sheet.Dimension.End.Row;
            int lastCol = Sheet.Dimension.End.Column;
            DateTime[] times = new DateTime[lastCol - 7 + 1];

            for (int k = 7; k <= lastCol; k++)
            {
                times[k - 7] = DateTime.Parse(Sheet.Cells[1, k].Value.ToString());
            }

            int i = 2;
            do
            {
                int height;
                string username = GetCellValueFromPossiblyMergedCell(Sheet, i, 1, out height);
                if (!String.IsNullOrEmpty(username))
                {
                    UsersExchangeData userdata = CheckDomainUserExists(username, users_all);
                    ApplicationUser user = null;
                    if (userdata != null) // если пользователь найден в домене
                    {
                        // создаем пользователя, если не существует
                        user = manager.FindByName(userdata.UserLogin);
                        if (user == null)
                        {
                            user = new ApplicationUser();
                            user.UserName = userdata.UserLogin;
                            user.FullName = userdata.FullName;
                            user.Email = "";
                            user.ShortName = username;
                            var res = manager.Create(user);
                            if (res.Succeeded)
                            {
                                manager.AddToRole(user.Id, "Members");
                            }
                            model.NewUsers.Add(username);
                        }
                        else
                        {
                            user.ShortName = username;
                        }
                        db.SaveChanges();

                    }

                    string deptname = GetCellValueFromPossiblyMergedCell(Sheet, i, 2);
                    if (!String.IsNullOrEmpty(deptname))
                    {
                        // создаем направление, если не существует
                        Department department = db.Departments.SingleOrDefault(x => x.Name == deptname);
                        if (department == null)
                        {
                            department = new Department() { Name = deptname };
                            db.Departments.Add(department);
                            db.SaveChanges();
                        }
                        if (user != null)
                        {
                            user.Department = department;
                            db.SaveChanges();
                        }

                        int cnt = (height / 2);
                        for (int j = 0; j < cnt; j++)
                        {
                            string prjname = GetCellValueFromPossiblyMergedCell(Sheet, i + 2 * j, 4);
                            if (!String.IsNullOrEmpty(prjname))
                            {
                                // создаем проект, если не существует
                                Project project = db.Projects.SingleOrDefault(x => x.Name == prjname);
                                if (project == null)
                                {
                                    project = new Project() { Name = prjname, Department = department, Description = "", WhenWasClosed = DateTime.Now.AddMonths(6) };
                                    db.Projects.Add(project);
                                    db.SaveChanges();
                                }

                                ImportUser import_user = null;


                                for (int k = 7; k <= lastCol; k++)
                                {
                                    string sum = GetCellValueFromPossiblyMergedCell(Sheet, i + 2 * j, k);
                                    string sum_completed = GetCellValueFromPossiblyMergedCell(Sheet, i + 2 * j + 1, k);
                                    DateTime _dt = times[k - 7];

                                    if (user != null && (!String.IsNullOrEmpty(sum) || !String.IsNullOrEmpty(sum_completed)))
                                    {
                                        user.ShortName = username.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ");
                                        int v;
                                        if (int.TryParse(sum, out v))
                                        {
                                            UserPayment payment = new UserPayment() { User = user, Project = project, Sum = int.Parse(sum) };

                                            PaymentsGroupState grp_state = (String.IsNullOrEmpty(sum_completed) ? PaymentsGroupState.InProcess : PaymentsGroupState.PaidOut);
                                            PaymentsGroup pgrp = db.PaymentsGroups.FirstOrDefault(x => x.WhenCreated == _dt && x.State == grp_state);
                                            if (pgrp == null)
                                            {
                                                pgrp = new PaymentsGroup() { WhenCreated = _dt, WhenPaidOut = _dt, UserClosed = currentUser, State = grp_state };
                                                db.PaymentsGroups.Add(pgrp);
                                            }

                                            payment.PaymentGroup = pgrp;
                                            payment = db.UserPayments.Add(payment);
                                            // создаем или пополняем баланс, если не выплачено
                                            var user_balance = db.UserBalances.FirstOrDefault(x => x.User.Id == user.Id && x.Project.ProjectId == payment.Project.ProjectId);
                                            if (user_balance == null)
                                            {
                                                user_balance = new UserBalance() { User = user, Project = payment.Project, Sum = 0 };
                                                user_balance = db.UserBalances.Add(user_balance);
                                            }
                                            if (pgrp.State == PaymentsGroupState.InProcess)
                                            {
                                                user_balance.Sum += payment.Sum;
                                                var rlist = pgrp.UsersPayments.ToList();
                                                // удалаем автоматически созданные счета, если они не оплачены
                                                foreach (var item in rlist)
                                                {
                                                    db.UserPayments.Remove(item);
                                                }
                                                db.PaymentsGroups.Remove(pgrp);
                                            }
                                            db.SaveChanges();
                                        }
                                    }
                                    else if (!String.IsNullOrEmpty(sum) || !String.IsNullOrEmpty(sum_completed))
                                    { // пользователя нет в домене, сохраняем строку из ексель файла
                                        if (import_user == null)
                                        {
                                            import_user = new ImportUser() { Fio = username, Department = department, Project = prjname };
                                            db.ImportUsers.Add(import_user);
                                            db.SaveChanges();
                                        }
                                        ImportUserPayment p = new ImportUserPayment() { ImportUser = import_user, WhenPaidOut = _dt, Sum = int.Parse(sum) };
                                        p.State = String.IsNullOrEmpty(sum_completed) ? PaymentsGroupState.InProcess : PaymentsGroupState.PaidOut;
                                        db.ImportUserPayments.Add(p);
                                    }
                                    else if (user == null)
                                    {
                                        if (!model.LoseUsers.Contains(username))
                                            model.LoseUsers.Add(username);
                                    }

                                    db.SaveChanges();


                                }
                            }
                        }
                    }
                }
                i += height;

            } while (i <= lastRow);


            return model;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult Upload(HttpPostedFileBase upload)
        {
            var currentUser = manager.FindById(User.Identity.GetUserId());
            IEnumerable<UsersExchangeData> users_all = getDomainUsers();
            UploadResultModel model = new UploadResultModel() { HasError = false, LoseUsers = new List<string>(), NewUsers = new List<string>() };

            try
            {
                if (upload != null)
                {
                    // получаем имя файла
                    string fileName = System.IO.Path.GetFileName(upload.FileName);
                    string fileContentType = upload.ContentType;
                    byte[] fileBytes = new byte[upload.ContentLength];
                    var data = upload.InputStream.Read(fileBytes, 0, Convert.ToInt32(upload.ContentLength));

                    using (var package = new ExcelPackage(upload.InputStream))
                    {
                        var Sheet = package.Workbook.Worksheets[2];

                        db.UserBalances.RemoveRange(db.UserBalances);
                        db.UserPayments.RemoveRange(db.UserPayments);
                        db.ImportUserPayments.RemoveRange(db.ImportUserPayments);
                        db.ImportUsers.RemoveRange(db.ImportUsers);
                        db.PaymentsGroups.RemoveRange(db.PaymentsGroups);
                        db.PaymentRequests.RemoveRange(db.PaymentRequests);
                        db.RequestHistories.RemoveRange(db.RequestHistories);

                        model = ImportPayments(Sheet, users_all, currentUser);
                    }
                }
            }
            catch(Exception e)
            {
                ModelState.AddModelError("", "Ошибка импорта " + e.Message);
                model.HasError = false;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult RenameProjects(ProjectRenameModel model)
        {
            if (model != null)
            {
                if (!string.IsNullOrEmpty(model.NewName))
                {
                    Department dept = db.Departments.Find(model.DepartmentId);
                    if (dept != null)
                    {
                        Project newp = db.Projects.Add(new Project() { Department = dept, Name = model.NewName, WhenWasClosed = DateTime.Now.AddMonths(6) });
                        List<int> ids = model.Projects.Select(x => x.ProjectId).ToList();
                        List<Project> SafeProjects = (from p in db.Projects where ids.Contains(p.ProjectId) select p).ToList();
                        Dictionary<int, bool> SafeProjectToDelete = new Dictionary<int, bool>(); foreach (var x in ids) SafeProjectToDelete.Add(x, true);

                        foreach (var user in db.Users.ToList())
                        {
                            // объединяем балансы
                            var list = (from b in db.UserBalances where b.User.Id == user.Id && ids.Contains(b.Project.ProjectId) select b).ToList();
                            if (list.Count > 0)
                            {
                                double total = list.Select(x => x.Sum).Sum();
                                if (total > 0)
                                {
                                    db.UserBalances.Add(new UserBalance() { Project = newp, User = user, Sum = total });
                                }

                                list.ForEach(x=> { x.Sum = 0; });
                            }

                            // объединяем платежи 
                            var plist = (from b in db.UserPayments where b.User.Id == user.Id && b.PaymentGroup.State == PaymentsGroupState.InProcess && ids.Contains(b.Project.ProjectId) select b).ToList();
                            if (plist.Count > 0)
                            {
                                var pglist = plist.Select(p => p.PaymentGroup).Distinct().ToList();
                                // объединяем платежи по каждому счету
                                foreach (var pg in pglist)
                                {
                                    double total = plist.Where(x => x.PaymentGroup.PaymentsGroupId == pg.PaymentsGroupId).Select(x => x.Sum).Sum();
                                    if (total > 0)
                                    {
                                        db.UserPayments.Add(new UserPayment() { Project = newp, User = user, Sum = total, PaymentGroup = pg });
                                    }
                                }
                                db.UserPayments.RemoveRange(plist);
                            }
                        }

                        SafeProjects.ForEach(x => { x.ProjectState = ProjectActiveState.Closed; });
                        db.SaveChanges();

                        var plist_a = (from b in db.UserPayments where b.PaymentGroup.State == PaymentsGroupState.PaidOut select b.Project.ProjectId).Distinct().ToList();
                        var list_a = (from b in db.UserBalances where b.Sum == 0 && !plist_a.Contains(b.Project.ProjectId) select b).ToList();
                        db.UserBalances.RemoveRange(list_a);
                        db.SaveChanges();

                        return RedirectToAction("ProjectsIndex");
                    }
                }
            }
            model.Depts = (from d in db.Departments select d).ToDictionary(d => d.DepartmentId, d => d.Name);
            ModelState.AddModelError("", "Ошибка ввода");
            return View("RenameProjectsConfirm", model);
        }

        [HttpPost]
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult RenameProjectsConfirm(ProjectsSearchModel input)
        {
            ProjectRenameModel model = new ProjectRenameModel() { Projects = new List<UserProjectBalance>(), NewName = "" };
            model.DepartmentId = -1;
            for (int i=0; i<input.CheckedIds.Length; i++)
            {
                if (input.Checked[i])
                {
                    var p = db.Projects.Find(input.CheckedIds[i]);
                    if (p != null)
                    {
                        model.Projects.Add(new UserProjectBalance { Name = p.Name + ", направление " + p.Department.Name, ProjectId = p.ProjectId });
                        if( model.DepartmentId == -1 )
                        {
                            model.DepartmentId = p.Department.DepartmentId;
                        }
                    }
                }
            }

            model.Depts = (from d in db.Departments select d).ToDictionary(d => d.DepartmentId, d => d.Name);

            return View(model);
        }

        public ActionResult ChangeProjectDepartment(ProjectsSearchModel model)
        {
            if( model.ChangeDepartmentId != null && model.ChangeProjectId != null )
            {
                var p = db.Projects.Find(model.ChangeProjectId);
                var d = db.Departments.Find(model.ChangeDepartmentId);
                if(p!=null && d!=null)
                {
                    p.Department = d;
                    db.SaveChanges();
                }
            }
            return RedirectToAction("ProjectsIndex", model);
        }

        
        [Authorize(Roles = "Administrators,Directors")]
        public ActionResult ProjectsIndex(ProjectsSearchModel model)
        {
            //const int RecordsPerPage = 20;
            var RecordsPerPage = model.RecordsPerPage ?? 20;
            var pageIndex = model.Page ?? 1;

            var results = (from p in db.Projects orderby p.Department.Name select p);
            if (!String.IsNullOrEmpty(model.ProjectNamePattern))
            {
                results = (from p in results where p.Name.ToUpper().Contains(model.ProjectNamePattern.ToUpper()) orderby p.Department.Name select p);
                //results = (from p in db.Projects where p.Name.ToUpper().Contains(model.ProjectNamePattern.ToUpper()) orderby p.Department.Name select p);
            }
            if( model.FilterDepartment != null )
            {
                results = (from p in results where p.Department.DepartmentId == model.FilterDepartment orderby p.Department.Name select p);
            }
            results = results.OrderBy(x => x.Department.Name + x.ProjectState);

            model.SearchResults = results.ToList().ToPagedList(pageIndex, RecordsPerPage);
            model.Checked = new bool[model.SearchResults.Count];
            model.CheckedIds = model.SearchResults.Select(x=>x.ProjectId).ToArray();
            ViewBag.Departments = db.Departments.Select(x => x).ToList();
            return View(model);
        }

        [Authorize(Roles = "Administrators,Directors,Managers")]
        public ActionResult ProjectsList(int? id)
        {
            Department dept = null;
            if (id == null)
            {
                var currentUser = manager.FindById(User.Identity.GetUserId());
                if (manager.IsInRole(currentUser.Id, SalaryRoles.Managers))
                {
                    dept = db.Departments.SingleOrDefault(x => x.Boss.Id == currentUser.Id);
                }
                else if (manager.IsInRole(currentUser.Id, SalaryRoles.Assistant))
                {
                    dept = db.Departments.SingleOrDefault(x => x.Assistant.Id == currentUser.Id);
                }
            }
            else
                dept = db.Departments.Find(id);

            var result = dept == null ? new List<Project>() : (from p in db.Projects where p.Department.DepartmentId == dept.DepartmentId orderby p.Name select p).ToList();

            ViewBag.BossName = "";
            if (dept != null)
            {
                if (dept.Boss != null)
                {
                    ViewBag.BossName = dept.Boss.FullName;
                }
            }
            ViewBag.DeptName = dept == null ? "" : dept.Name;
            return View(result);
        }


        // GET: Departments/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Department department = db.Departments.Find(id);
            if (department == null)
            {
                return HttpNotFound();
            }


            return View(department);
        }

        // GET: Departments/Create
        public ActionResult Create()
        {
            return View();
        }

        public ActionResult FindUserByName(string query)
        {
            var users_all = db.Users.ToList();
            List<UsersListItemModel> list = new List<UsersListItemModel>();
            foreach (var item in users_all)
            {
                if ( item.FullName.ToUpper().Contains(query.ToUpper() ) && item.UserName != "root")
                    list.Add(new UsersListItemModel() { Name = item.FullName, UserName = item.FullName });
            }
            return Json(list, JsonRequestBehavior.AllowGet);
        }

        // POST: Departments/Create
        // Чтобы защититься от атак чрезмерной передачи данных, включите определенные свойства, для которых следует установить привязку. Дополнительные 
        // сведения см. в статье https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,Name,BossName")] Department department)
        {
            if (ModelState.IsValid)
            {
                db.Departments.Add(department);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(department);
        }

        // GET: Departments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Department department = db.Departments.Find(id);
            if (department == null)
            {
                return HttpNotFound();
            }

            DepartmentEditModel model = new DepartmentEditModel()
            {
                id = department.DepartmentId,
                Name = department.Name,
                BossName = department.Boss == null ? null : department.Boss.FullName,
                AssistantName = department.Assistant == null ? null : department.Assistant.FullName,
                NewMemeberName = "",
                MembersNames = db.Users.Where(x=>x.Department.DepartmentId == department.DepartmentId).Select(x=>x.FullName).ToList(),
            };

            return View( model );
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MoveMemberTo(DepartmentEditModel dept_input)
        {
            Department department = db.Departments.Find(dept_input.id);
            if (department == null)
            {
                return HttpNotFound();
            }

            ApplicationUser user = db.Users.SingleOrDefault(x => x.FullName == dept_input.NewMemeberName);
            if( user != null )
            {
                user.Department = department;
                db.SaveChanges();
            }
            else
            {
                ModelState.AddModelError("", "Пользователь " + dept_input.NewMemeberName + " не найден");
            }

            return RedirectToAction("Edit/" + dept_input.id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(DepartmentEditModel dept_input)
        {
            if (ModelState.IsValid)
            {
                List<string> UsersIds = new List<string>();
                Department department = db.Departments.Find(dept_input.id);
                if (department != null)
                {
                    ApplicationUser boss = db.Users.SingleOrDefault(x => x.FullName == dept_input.BossName);
                    ApplicationUser assistant = db.Users.SingleOrDefault(x => x.FullName == dept_input.AssistantName);

                    if( assistant == null )
                    {
                        department.Assistant = null;
                        db.Entry(department).Reference(p => p.Assistant).CurrentValue = null;
                    }
                    else
                    {
                        department.Assistant = assistant;
                    }

                    if (boss == null)
                    {
                        department.Boss = null;
                        db.Entry(department).Reference(p => p.Boss).CurrentValue = null;
                    }
                    else
                    {
                        department.Boss = boss;
                    }

                    department.Name = dept_input.Name;
                    db.Entry(department).State = EntityState.Modified;
                    db.SaveChanges();
                    // сохраняем изменения и обновляем роли пользователей


                    List<string> bosses = (from p in db.Departments where p.Boss != null select p.Boss.Id).ToList();
                    List<string> assistantes = (from p in db.Departments where p.Assistant != null select p.Assistant.Id).ToList();
                    List<string> users = (from p in db.Users select p.Id).ToList();
                    
                    foreach(var item in users)
                    {
                        if( bosses.Contains(item) )
                        {
                            if( !manager.IsInRole(item, SalaryRoles.Managers) )
                            {
                                manager.AddToRole(item, SalaryRoles.Managers);
                            }
                        }
                        else
                        {
                            if (manager.IsInRole(item, SalaryRoles.Managers))
                            {
                                manager.RemoveFromRole(item, SalaryRoles.Managers);
                            }
                        }

                        if (assistantes.Contains(item))
                        {
                            if (!manager.IsInRole(item, SalaryRoles.Assistant))
                            {
                                manager.AddToRole(item, SalaryRoles.Assistant);
                            }
                        }
                        else
                        {
                            if (manager.IsInRole(item, SalaryRoles.Assistant))
                            {
                                manager.RemoveFromRole(item, SalaryRoles.Assistant);
                            }
                        }

                    }

                    

                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Ошибка ввода, такое подразделение не существует");
                    return View(dept_input);
                }
            }
            else
            {
                ModelState.AddModelError("", "Ошибка ввода");
                return View(dept_input);
            }
        }

        ApplicationUser SingleOrDefaultDomainUser(string query, out string err)
        {
            bool IsMoreOnceUser = false;
            ApplicationUser u = SingleOrDefaultDomainUser(query, out err, out IsMoreOnceUser);
            return u;
        }
        ApplicationUser SingleOrDefaultDomainUser(string query, out string err, out bool IsMoreOnceUser)
        {
            ApplicationUser user = null;
            err = "";
            IsMoreOnceUser = false;
            if (String.IsNullOrEmpty(query)) return null;
            IEnumerable<UsersExchangeData> domain_users = getDomainUsers();
            foreach (var item in domain_users)
            {
                string str = item.FullName;
                if (str.Split(' ').Contains(query))
                {
                    if (user != null)
                    {
                        err = "Найдено более одного пользователя по заданному критерию ";
                        IsMoreOnceUser = true;
                        return null;
                    }
                    else
                    {

                        user = new ApplicationUser();
                        user.FullName = item.FullName;
                        user.UserName = item.UserLogin;
                    }
                }
            }
            if (user == null)
            {
                err = "Пользователь не найден";
            }
            return user;
        }
        public ApplicationUser TrySetBossToDept(string BossName)
        {
            ApplicationUser boss = null;
            return boss;
        }
        // GET: Departments/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Department department = db.Departments.Find(id);
            if (department == null)
            {
                return HttpNotFound();
            }
            return View(department);
        }

        // POST: Departments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Department department = db.Departments.Find(id);
            db.Departments.Remove(department);
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
