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
using SalaryApp.Models;

namespace SalaryApp.Controllers
{
    [Authorize(Roles = "Administrators,Directors")]
    public class RegularPaymentsController : Controller
    {
        private AppDbContext db = new AppDbContext();
        private UserManager<ApplicationUser> manager;

        public RegularPaymentsController()
        {
            db = new AppDbContext();
            manager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
        }

        // GET: RegularPayments
        public ActionResult Index()
        {
            return View(db.RegularPayments.OrderBy(x=>x.RecipientUser.ShortName).ToList());
        }

        // GET: RegularPayments/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: RegularPayments/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(RegularPaymentEditModel input)
        {
            if (ModelState.IsValid)
            {
                var currentUser = manager.FindById(User.Identity.GetUserId());
                input.isDublicateErr = false;

                if ( input.PayoutFrom == null && input.PayoutTo == null )
                {
                    ModelState.AddModelError("", "Ошибка ввода даты");
                    return View(input);
                }
                ApplicationUser user = db.Users.FirstOrDefault(x => x.FullName == input.UserName);
                if (user == null)
                {
                    ModelState.AddModelError("", "Не найден пользователь");
                    return View(input);
                }
                Project project = PaymentRequestsController.ProjectFind(db, input.ProjectCode, input.ProjectShortName);
                if ((input.ProjectCode != null && input.ProjectShortName != null) || project != null)
                {
                    if (project == null)
                    {
                        Department dept = db.Departments.SingleOrDefault(x => x.Name == "_Резерв");
                        if (dept == null)
                        {
                            dept = db.Departments.Add(new Department { Name = "_Резерв", Boss = currentUser });
                            db.SaveChanges();
                            dept = db.Departments.SingleOrDefault(x => x.Name == "_Резерв");
                        }
                        string ProjectName = input.ProjectCode + "_" + input.ProjectShortName;
                        project = db.Projects.Add(new Project() { Department = dept, Name = ProjectName });
                        db.SaveChanges();
                    }

                    RegularPayment rp = db.RegularPayments.FirstOrDefault(x => x.Project.ProjectId == project.ProjectId && x.RecipientUser.Id == user.Id);
                    if (rp == null)
                    {
                        db.RegularPayments.Add(
                            new RegularPayment()
                            {
                                Project = project,
                                RecipientUser = user,
                                Sum = input.Sum,
                                AppointedUser = currentUser,
                                PayoutFrom = input.PayoutFrom,
                                PayoutTo = input.PayoutTo
                            });
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Регулярная выплата для " + user.FullName + " по проекту " + project.Name + " уже существует");
                        input.isDublicateErr = true;
                        input.RegularPaymentId = rp.RegularPaymentId;
                        return View(input);
                    }

                    

                }
                else {
                    ModelState.AddModelError("", "Ошибка создания проекта");
                    return View(input);
                }
            }

            return View(input);
        }

        // GET: RegularPayments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            RegularPayment regularPayment = db.RegularPayments.Find(id);
            
            if (regularPayment == null)
            {
                return HttpNotFound();
            }
            RegularPaymentEditModel model = 
                new RegularPaymentEditModel() { RegularPaymentId = regularPayment.RegularPaymentId, Sum = regularPayment.Sum, UserName = regularPayment.RecipientUser.FullName,
                    PayoutFrom = regularPayment.PayoutFrom, PayoutTo = regularPayment.PayoutTo,
                    ProjectCode = PaymentRequestEditModel.getProjectCode(regularPayment.Project.Name),
                    ProjectShortName = PaymentRequestEditModel.getProjectShortName(regularPayment.Project.Name)
                };
            return View(model);
        }

        // POST: RegularPayments/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(RegularPaymentEditModel input)
        {
            if (ModelState.IsValid)
            {
                RegularPayment regularPayment = db.RegularPayments.Find(input.RegularPaymentId);
                if (regularPayment == null)
                {
                    ModelState.AddModelError("", "Не найден регулярный платеж");
                    return View(input);
                }
                else if ((input.PayoutFrom == null && input.PayoutTo == null) || input.Sum == 0)
                {
                    ModelState.AddModelError("", "Ошибка ввода");
                    return View(input);
                }
                else {
                    regularPayment.Sum = input.Sum;
                    regularPayment.PayoutFrom = input.PayoutFrom;
                    regularPayment.PayoutTo = input.PayoutTo;
                    db.SaveChanges();

                    return RedirectToAction("Index");
                }
            }
            return View(input);
        }

        // GET: RegularPayments/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            RegularPayment regularPayment = db.RegularPayments.Find(id);
            if (regularPayment == null)
            {
                return HttpNotFound();
            }
            return View(regularPayment);
        }

        // POST: RegularPayments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            RegularPayment regularPayment = db.RegularPayments.Find(id);
            db.RegularPayments.Remove(regularPayment);
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
