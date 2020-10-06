using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using System.Reflection;
using OfficeOpenXml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SalaryApp.Models
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static AppDbContext Create()
        {
            return new AppDbContext();
        }

        public DbSet<Department> Departments { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<UserBalance> UserBalances { get; set; }
        public DbSet<PaymentsGroup> PaymentsGroups { get; set; }
        public DbSet<UserPayment> UserPayments { get; set; }
        public DbSet<PaymentRequest> PaymentRequests { get; set; }
        public DbSet<RegularPayment> RegularPayments { get; set; }
        public DbSet<ImportUser> ImportUsers { get; set; }
        public DbSet<ImportUserPayment> ImportUserPayments { get; set; }
        public DbSet<RequestHistory> RequestHistories { get; set; }
        public DbSet<JobForMailing> JobForMailings { get; set; }


        public void updShowSalary(ApplicationUser user, double val)
        {
            if (user == null) return;
            try { Database.ExecuteSqlCommand("exec updShowSalary '" + user.Id + "', '" + val.ToString() + "'"); }
            catch (Exception) { }
        }

    }


    public class Department
    {
        public int DepartmentId { get; set; }

        [Display(Name = "Направление")]
        public string Name { get; set; }

        [Display(Name = "Лидер")]
        public virtual ApplicationUser Boss { get; set; }

        [Display(Name = "Руководитель проектов")]
        public virtual ApplicationUser Assistant { get; set; }

        public virtual ICollection<Project> Projects { get; set; }
    }
    public enum ProjectActiveState : byte
    {
        [Display(Name = @"Активен")]
        Active,

        [Display(Name = @"Закрыт")]
        Closed
    }
    public class Project
    {
        public virtual Department Department { get; set; }

        [Display(Name = "Автор")]
        public virtual ApplicationUser Author { get; set; }

        public int ProjectId { get; set; }

        [Display(Name = "Проект")]
        public string Name { get; set; }

        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Display(Name = "Состояние")]
        public ProjectActiveState ProjectState { get; set; }

        [Display(Name = "Дата создания")]
        public DateTime WhenCreated { get; set; } = DateTime.Now;

        [Display(Name = "Дата завершения")]
        public DateTime WhenWasClosed { get; set; } = DateTime.Now.AddMonths(6);
        public virtual ICollection<UserPayment> UsersPayments { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PaymentRequestState : byte
    {
        [Display(Name = @"Не отправлена")]
        InProcess,

        [Display(Name = @"На утверждении")]
        WaitConfirm,

        [Display(Name = @"Утверждена")]
        Confirmed,

        [Display(Name = @"Отклонена")]
        Rejected,

        [Display(Name = @"На доработке")]
        ReWorked,

        [Display(Name = @"Исполнена")]
        Credited
    }
    public enum PaymentRequestType : byte
    {
        [Display(Name = @"грн")]
        Monetary,
        [Display(Name = @"часов")]
        Times
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RegularPaymentState : byte
    {
        [Display(Name = @"Активна")]
        Active,
        [Display(Name = @"Приостановлена")]
        Suspended,
        [Display(Name = @"Отменена")]
        Aborted,
    }
    public class RegularPayment
    {
        public virtual Project Project { get; set; }
        [Display(Name = @"ФИО")]
        public virtual ApplicationUser RecipientUser { get; set; }
        public virtual ApplicationUser AppointedUser { get; set; }
        public int RegularPaymentId { get; set; }
        [Display(Name = @"Сумма")]
        public double Sum { get; set; }
        [Display(Name = @"Начало выплат")]
        public DateTime? PayoutFrom { get; set; }

        [Display(Name = @"Завершение выплат")]
        public DateTime? PayoutTo { get; set; }
        public RegularPaymentState State { get; set; } = RegularPaymentState.Active;
    }

    public class PaymentRequest
    {
        public virtual Project Project { get; set; }
        public virtual ApplicationUser RecipientUser { get; set; }
        public virtual ApplicationUser AppointedUser { get; set; }
        public virtual ApplicationUser ConfirmedUser { get; set; }
        public int PaymentRequestId { get; set; }
        public PaymentRequestState RequestState { get; set; }
        public double TimesOrSum { get; set; }
        public PaymentRequestType SumType { get; set; }
        public bool AgreedPM { get; set; }
        public DateTime WhenCreated { get; set; } = DateTime.Now;
        public DateTime WhenStateChanged { get; set; } = DateTime.Now;

        public string AttachedFileName { get; set; }

        public SelectList GetRequestStatesFor(System.Security.Principal.IPrincipal User, PaymentRequest Request)
        {
            List<UserRequestState> states = new List<UserRequestState>();
            UserRequestState selected = new UserRequestState() { RequestState = RequestState };
            selected.Name = selected.RequestState.GetType().GetMember(selected.RequestState.ToString()).First().GetCustomAttribute<DisplayAttribute>().Name;
            states.Add(selected);

            if (User.IsInRole(SalaryRoles.Managers) || User.IsInRole(SalaryRoles.Assistant))
            {
                if (RequestState == PaymentRequestState.InProcess)
                {
                    states.Add(new UserRequestState() { RequestState = PaymentRequestState.WaitConfirm });
                }
                if (RequestState == PaymentRequestState.ReWorked)
                {
                    states.Add(new UserRequestState() { RequestState = PaymentRequestState.WaitConfirm });
                }
            }
            if (User.IsInRole(SalaryRoles.Directors))
            {
                if (RequestState == PaymentRequestState.WaitConfirm)
                {
                    states.Add(new UserRequestState() { RequestState = PaymentRequestState.Confirmed });
                    if (Request.AppointedUser.UserName != User.Identity.Name)
                    {
                        states.Add(new UserRequestState() { RequestState = PaymentRequestState.Rejected });
                        states.Add(new UserRequestState() { RequestState = PaymentRequestState.ReWorked });
                    }
                }

            }
            if (User.IsInRole(SalaryRoles.Administrators))
            {
                if (RequestState == PaymentRequestState.Confirmed)
                {
                    states.Add(new UserRequestState() { RequestState = PaymentRequestState.Credited });
                }

            }

            for (int i = 0; i < states.Count; i++)
            {
                var x = states[i].RequestState.GetType().GetMember(states[i].RequestState.ToString()).First().GetCustomAttribute<DisplayAttribute>();
                states[i].Name = x.Name;
                states[i].id = (byte)states[i].RequestState;
            }

            return new SelectList(states, "id", "Name", selected);
        }

        public static double RoundSum(double input)
        {
            double rez = input - input % 10;
            return rez + 10 * Math.Round((0.01 + input % 10) / 10);
        }
        public static double TruncSum(double input)
        {
            return input - input % 10;
        }

        public static PaymentRequestState FromString(string state)
        {
            PaymentRequestState s = PaymentRequestState.InProcess;
            switch (state)
            {
                case "InProcess":
                    s = PaymentRequestState.InProcess;
                    break;
                case "WaitConfirm":
                    s = PaymentRequestState.WaitConfirm;
                    break;
                case "Confirmed":
                    s = PaymentRequestState.Confirmed;
                    break;
                case "Rejected":
                    s = PaymentRequestState.Rejected;
                    break;
                case "ReWorked":
                    s = PaymentRequestState.ReWorked;
                    break;
                case "Credited":
                    s = PaymentRequestState.Credited;
                    break;
            }
            return s;
        }

    }

    public class RequestHistory
    {
        public int RequestHistoryId { get; set; }

        public virtual PaymentRequest Request { get; set; }

        public virtual ApplicationUser Author { get; set; }

        public DateTime WhenPosted { get; set; }

        public string Comments { get; set; }
    }

    public enum PaymentsGroupState : byte
    {
        [Display(Name = @"Оплачен")]
        PaidOut,
        [Display(Name = @"На доработке")]
        InProcess
    }

    public class UserBalance
    {
        public int UserBalanceId { get; set; }

        public virtual ApplicationUser User { get; set; }

        public virtual Project Project {get;set;}

        public double Sum { get; set; }
    }

    public class UserPayment
    {
        [Display(Name = "Проект")]
        public virtual Project Project { get; set; }
        public virtual PaymentsGroup PaymentGroup { get; set; }
        public virtual ApplicationUser User { get; set; }
        public int UserPaymentId { get; set; }

        [Display(Name = "Выплачено, грн")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public double Sum { get; set; }

        [Display(Name = "В т.ч. выплачено по регулярным платежам, грн")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public double RegularPaymentSum { get; set; } = 0;
    }

    public class PaymentsGroup
    {
        public int PaymentsGroupId {get;set;}

        [Display(Name = "Дата создания")]
        public DateTime WhenCreated { get; set; } = DateTime.Now;

        [Display(Name = "Дата оплаты")]
        public DateTime WhenPaidOut { get; set; }

        [Display(Name = "Исполнитель")]
        public ApplicationUser UserClosed { get; set; }

        [Display(Name = "Статус оплаты")]
        public PaymentsGroupState State { get; set; } = PaymentsGroupState.InProcess;

        public virtual ICollection<UserPayment> UsersPayments { get; set; }
    }

    public class JobForMailing
    {
        public int JobForMailingId { get; set; }
        public DateTime WhenCreated { get; set; } = DateTime.Now;
        public UserPayment UserPayment { get; set; }
    }

    public class ImportUser
    {
        public int ImportUserId { get; set; }
        public string Fio { get; set; }
        public Department Department { get; set; }
        public string Project { get; set; }

        public virtual ICollection<ImportUserPayment> ImportUserPayments { get; set; }
    }

    public class ImportUserPayment
    {
        public virtual ImportUser ImportUser { get; set; }
        public int ImportUserPaymentId { get; set; }
        public DateTime WhenPaidOut { get; set; }
        public PaymentsGroupState State { get; set; }
        public int Sum { get; set; }
        public DateTime WhenCreated { get; set; } = DateTime.Now;
    }

}