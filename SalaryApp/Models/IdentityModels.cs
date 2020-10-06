using System.Data.Entity;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Reflection;

namespace SalaryApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string ShortName { get; set; }
        public bool HideSalary { get; set; } = false;
        public virtual Department Department { get; set; }
        public virtual ICollection<UserBalance> UserBalances { get; set; }

        public virtual ICollection<UserPayment> UsersPayments { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Обратите внимание, что authenticationType должен совпадать с типом, определенным в CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Здесь добавьте утверждения пользователя
            return userIdentity;
        }
    }

    public class SalaryRoles
    {
        public const string Administrators = "Administrators";
        public const string Directors = "Directors";
        public const string Managers = "Managers";
        public const string Assistant= "Assistant";
        public const string Members = "Members";
    }
    //DropCreateDatabaseIfModelChanges
    //CreateDatabaseIfNotExists
    public class AppDbInitializer : DropCreateDatabaseIfModelChanges<AppDbContext>
    {
        protected override void Seed(AppDbContext context)
        {
            Initialize(context);
            base.Seed(context);
        }

        private void Initialize(AppDbContext context)
        {
            var UserManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));
            var RoleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));

            //Create Roles
            foreach (var field in typeof(SalaryRoles).GetFields(BindingFlags.Static | BindingFlags.Public) )
            {
                string role = field.GetValue(null).ToString();
                if (!RoleManager.RoleExists(role))
                {
                    var roleresult = RoleManager.Create(new IdentityRole(role.ToString()));
                }
            }

            //Create Super User
            var user = new ApplicationUser();
            user.UserName = "root";
            user.FullName = "Evil Dragon";
            user.Email = "menya@tyt.net";
            user.ShortName = "E.Dragon";
            user.EmailConfirmed = true;
            var adminresult = UserManager.Create(user, "123456");
            if (adminresult.Succeeded)
            {
                var result = UserManager.AddToRole(user.Id, "Administrators");
            }

        }
    }

}