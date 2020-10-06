using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using SalaryApp.Models;

namespace SalaryApp.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public const string Domain = "";
        public const string Container = @"";
        public const string sServiceUser = @"";
        public const string sServicePassword = "";

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set 
            { 
                _signInManager = value; 
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }


        [Authorize]
        public ActionResult ChangeRoot()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeRoot(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string ErrMsg;
            UserPrincipal userPrincipal = TryDomainLogin(model.UserName, model.Password, out ErrMsg);
            if (userPrincipal == null)
            {
                ModelState.AddModelError("", ErrMsg);
                return View(model);
            }
            ApplicationUser user = LocateUser(model.UserName, userPrincipal, SalaryRoles.Administrators, out ErrMsg);
            if (user == null)
            {
                ModelState.AddModelError("", ErrMsg);
                return View(model);
            }
            if (!UserManager.IsInRole(user.Id, SalaryRoles.Administrators))
            {
                UserManager.AddToRole(user.Id, SalaryRoles.Administrators);
            }
            if (!UserManager.IsInRole(user.Id, SalaryRoles.Members))
            {
                UserManager.AddToRole(user.Id, SalaryRoles.Members);
            }


            ApplicationUser pre_admin = UserManager.FindById(User.Identity.GetUserId());
            await SignInAsync(user, model.RememberMe);

            if( pre_admin.UserName == "root")
            {
                UserManager.Delete(pre_admin);
            }
            else
            {
                if( UserManager.IsInRole(pre_admin.Id, SalaryRoles.Administrators))
                {
                    UserManager.RemoveFromRole(pre_admin.Id, SalaryRoles.Administrators);
                }
            }

            return RedirectToLocal("/PaymentRequests");
        }
        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (returnUrl != null)
            {
                returnUrl = returnUrl.Contains("LogOff") ? "/Home/Index" : returnUrl;
            }
            // Сбои при входе не приводят к блокированию учетной записи
            // Чтобы ошибки при вводе пароля инициировали блокирование учетной записи, замените на shouldLockout: true
            var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    if (model.UserName == "root")
                    {
                        return RedirectToLocal("/Account/ChangeRoot");
                    }
                    else 
                        return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    string ErrMsg;
                    UserPrincipal userPrincipal = TryDomainLogin(model.UserName, model.Password, out ErrMsg);
                    if( userPrincipal == null )
                    {
                        ModelState.AddModelError("", ErrMsg);
                        return View(model);
                    }

                    ApplicationUser user = LocateUser(model.UserName, userPrincipal, "Members",  out ErrMsg);
                    if( user == null )
                    {
                        ModelState.AddModelError("", ErrMsg);
                        return View(model);
                    }

                    await SignInAsync(user, model.RememberMe);

                    // проверка на наличие данных импорта (пользователь есть в ексель, не найден в домене )
                    string[] fio;
                    if (DepartmentsController.TryNormalyzeDomainName(user.FullName, out fio))
                    {
                        AppDbContext db = new AppDbContext();
                        ApplicationUser user_ctx = db.Users.Find(user.Id);
                        string shortname = TryImportUserPayments(db, user_ctx, fio[0]);
                        if (shortname != null)
                        {
                            user.ShortName = shortname;
                            UserManager.Update(user);
                        }
                        else
                        {
                            if ((shortname = TryImportUserPayments(db, user_ctx, fio[1])) != null)
                            {
                                user.ShortName = shortname;
                                UserManager.Update(user);
                            }
                        }
                    }
                    return RedirectToLocal(returnUrl);
            }
        }

        private string TryImportUserPayments(AppDbContext db, ApplicationUser user_ctx, string fio) // возвращает ексель имя пользователя
        {
            string shortname = null;
            var q1 = db.ImportUsers.Where(x => x.Fio.ToUpper().Replace("Ё", "Е") == fio).ToList();
            foreach (var item in q1)
            {
                shortname = item.Fio;

                Department department = user_ctx.Department;// db.Departments.SingleOrDefault(x => x.Name == item.Department);
                if (department == null)
                {
                    department = new Department() { Name = item.Department.Name };
                    db.Departments.Add(department);
                    db.SaveChanges();
                }
                
                Project project = db.Projects.SingleOrDefault(x => x.Name == item.Project);
                if (project == null)
                {
                    project = new Project() { Name = item.Project, Department = department, Description = "", WhenWasClosed = DateTime.Now.AddMonths(6) };
                    db.Projects.Add(project);
                    db.SaveChanges();
                }

                foreach (var p in item.ImportUserPayments)
                {
                    PaymentsGroup pgrp = db.PaymentsGroups.FirstOrDefault(x => x.WhenPaidOut == p.WhenPaidOut && x.State == p.State);
                    if( pgrp == null )
                    {
                        pgrp = new PaymentsGroup() { WhenPaidOut = p.WhenPaidOut, State = p.State };
                        db.PaymentsGroups.Add(pgrp);
                    }

                    UserPayment payment = new UserPayment() { User = user_ctx, Project = project, Sum = p.Sum, PaymentGroup = pgrp };
                    payment = db.UserPayments.Add(payment);
                    // пополняем баланс, если не выплачено
                    var user_balance = db.UserBalances.FirstOrDefault(x => x.User.Id == user_ctx.Id && x.Project.ProjectId == payment.Project.ProjectId);
                    if (user_balance == null)
                    {
                        user_balance = new UserBalance() { User = user_ctx, Project = payment.Project, Sum = 0 };
                        db.UserBalances.Add(user_balance);
                    }
                    if (pgrp.State == PaymentsGroupState.InProcess)
                    {
                        user_balance.Sum += payment.Sum;
                    }

                    db.SaveChanges();
                }
                db.ImportUserPayments.RemoveRange(item.ImportUserPayments);
                db.SaveChanges();
            }
            db.ImportUsers.RemoveRange(q1);
            db.SaveChanges();
            return shortname;
        }
        private ApplicationUser LocateUser(string UserName, UserPrincipal userPrincipal, string InitRole, out string ErrMsg)
        {
            ApplicationUser user = UserManager.FindByName(UserName);
            if (user == null)
            {
                user = new ApplicationUser();
                user.UserName = UserName;
                user.FullName = userPrincipal.Name;
                user.Email = userPrincipal.EmailAddress == null ? UserName + "@owen.ua" : userPrincipal.EmailAddress;
                var res = UserManager.Create(user);
                if (res.Succeeded)
                {
                    UserManager.AddToRole(user.Id, InitRole);
                }
                else
                {
                    ErrMsg = "Ошибка создания пользователя: ";
                    return null;
                }
            }
            else
            {
                user.FullName = userPrincipal.Name;
                user.Email = userPrincipal.EmailAddress;
                UserManager.Update(user);
            }
            ErrMsg = "";
            return user;
        }

        private UserPrincipal TryDomainLogin(string UserName, string Password, out string Error)
        {
            PrincipalContext principalContext = new PrincipalContext(ContextType.Domain, Domain, Container, sServiceUser, sServicePassword);
            bool isAuthenticated = false;
            UserPrincipal userPrincipal = null;
            Error = "";
            try
            {
                userPrincipal = UserPrincipal.FindByIdentity(principalContext, UserName);

                if (userPrincipal != null)
                {
                    isAuthenticated = principalContext.ValidateCredentials(UserName, Password, ContextOptions.Negotiate);
                }
            }
            catch (Exception exception)
            {
                Error = "Неудачная попытка входа: " + exception.Message;
                return null;
            }

            if (!isAuthenticated)
            {
                Error = "Username or Password is not correct.";
                return null;
            }

            if (userPrincipal.IsAccountLockedOut())
            {
                Error = "Your account is locked.";
                return null;
            }

            if (userPrincipal.Enabled.HasValue && userPrincipal.Enabled.Value == false)
            {
                Error = "Your account is disabled.";
                return null;
            }

            Error = "";
            return userPrincipal;
        }

        public ActionResult Detail()
        {
            ApplicationUser user = UserManager.FindByName(User.Identity.GetUserName());
            var model = new UserInfoModel
            {
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email
            };
            return View(model);
        }

        //
        // GET: /Account/VerifyCode
        [AllowAnonymous]
        public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
        {
            // Требовать предварительный вход пользователя с помощью имени пользователя и пароля или внешнего имени входа
            if (!await SignInManager.HasBeenVerifiedAsync())
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Приведенный ниже код защищает от атак методом подбора, направленных на двухфакторные коды. 
            // Если пользователь введет неправильные коды за указанное время, его учетная запись 
            // будет заблокирована на заданный период. 
            // Параметры блокирования учетных записей можно настроить в IdentityConfig
            var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent:  model.RememberMe, rememberBrowser: model.RememberBrowser);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(model.ReturnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Неправильный код.");
                    return View(model);
            }
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent:false, rememberBrowser:false);
                    
                    // Дополнительные сведения о включении подтверждения учетной записи и сброса пароля см. на странице https://go.microsoft.com/fwlink/?LinkID=320771.
                    // Отправка сообщения электронной почты с этой ссылкой
                    // string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    // await UserManager.SendEmailAsync(user.Id, "Подтверждение учетной записи", "Подтвердите вашу учетную запись, щелкнув <a href=\"" + callbackUrl + "\">здесь</a>");

                    return RedirectToAction("Index", "Home");
                }
                AddErrors(result);
            }

            // Появление этого сообщения означает наличие ошибки; повторное отображение формы
            return View(model);
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.Email);
                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    // Не показывать, что пользователь не существует или не подтвержден
                    return View("ForgotPasswordConfirmation");
                }

                // Дополнительные сведения о включении подтверждения учетной записи и сброса пароля см. на странице https://go.microsoft.com/fwlink/?LinkID=320771.
                // Отправка сообщения электронной почты с этой ссылкой
                // string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                // var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);		
                // await UserManager.SendEmailAsync(user.Id, "Сброс пароля", "Сбросьте ваш пароль, щелкнув <a href=\"" + callbackUrl + "\">здесь</a>");
                // return RedirectToAction("ForgotPasswordConfirmation", "Account");
            }

            // Появление этого сообщения означает наличие ошибки; повторное отображение формы
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Не показывать, что пользователь не существует
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            }
            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Запрос перенаправления к внешнему поставщику входа
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        //
        // GET: /Account/SendCode
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
        {
            var userId = await SignInManager.GetVerifiedUserIdAsync();
            if (userId == null)
            {
                return View("Error");
            }
            var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            // Создание и отправка маркера
            if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
            {
                return View("Error");
            }
            return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
            {
                return RedirectToAction("Login");
            }

            // Выполнение входа пользователя посредством данного внешнего поставщика входа, если у пользователя уже есть имя входа
            var result = await SignInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    // Если у пользователя нет учетной записи, то ему предлагается создать ее
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;
                    return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = loginInfo.Email });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Manage");
            }

            if (ModelState.IsValid)
            {
                // Получение сведений о пользователе от внешнего поставщика входа
                var info = await AuthenticationManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await UserManager.AddLoginAsync(user.Id, info.Login);
                    if (result.Succeeded)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public ActionResult ExternalLoginFailure()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Вспомогательные приложения

        private async Task SignInAsync(ApplicationUser user, bool isPersistent)
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ExternalCookie);
            var identity = await UserManager.CreateIdentityAsync(user, DefaultAuthenticationTypes.ApplicationCookie);
            AuthenticationManager.SignIn(new AuthenticationProperties() { IsPersistent = isPersistent }, identity);
        }



        // Используется для защиты от XSRF-атак при добавлении внешних имен входа
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}