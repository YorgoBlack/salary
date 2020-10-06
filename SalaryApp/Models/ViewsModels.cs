using System;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PagedList;
using System.Web.Routing;
using System.Web.Mvc;
using System.Linq;

namespace SalaryApp.Models
{

    public class UploadResultModel
    {
        public bool HasError { get; set; }
        public List<string> NewUsers { get; set; }
        public List<string> LoseUsers { get; set; }
    }
    public class PaymentSearchModel
    {
        public int? Page { get; set; }

        public string DuringSortOrder { get; set; }
        public string ProjectSortOrder { get; set; }
        public string sortby { get; set; }
        public string presortby { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateFrom { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateTo { get; set; }

        public int? Project { get; set; }
        public IPagedList<UserPayment> SearchResults { get; set; }

        public RouteValueDictionary getQueryParams(object page)
        {
            RouteValueDictionary dict = new RouteValueDictionary();
            if (page is String)
            {
                dict["sortby"] = page;
                dict["presortby"] = page;
            }
            else
            {
                dict["page"] = page;
                dict["presortby"] = presortby;
            }

            dict["DateFrom"] = DateFrom == null ? "" : DateFrom.Value.ToString("dd/MM/yyyy");
            dict["DateTo"] = DateTo == null ? "" : DateTo.Value.ToString("dd/MM/yyyy");
            dict["Project"] = Project;
            dict["DuringSortOrder"] = DuringSortOrder;
            dict["ProjectSortOrder"] = ProjectSortOrder;
            return dict;
        }
    }

    public class UserInfoModel
    {
        [Display(Name = "Логин")]
        public string UserName { get; set; }

        [Display(Name = "ФИО (доменное)")]
        public string FullName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class DepartmentsList
    {
        public string Administrator { get; set; }
        public string Director { get; set; }
        public List<Department> depts { get; set; }

        [Display(Name = "Перевод")]
        public string NewHideSalaryUser { get; set; }

        public List<UsersExchangeData> HideSalaryUsers { get; set; }
    }

    public class DepartmentEditModel
    {
        public int id { get; set; }

        [Display(Name = "Направление")]
        public string Name { get; set; }

        [Display(Name = "Лидер")]
        public string BossName { get; set; }

        [Display(Name = "Руководитель проектов")]
        public string AssistantName { get; set; }

        [Display(Name = "Перевод")]
        public string NewMemeberName { get; set; }

        public List<String> MembersNames { get; set; }
    }

    public class ProjectsSearchModel
    {
        public int? Page { get; set; }

        public int? RecordsPerPage { get; set; }
        
        public string ProjectNamePattern { get; set; }
        public int? FilterDepartment { get; set; }
        public int? ChangeProjectId { get; set; }
        public int? ChangeDepartmentId{ get; set; }
        public IPagedList<Project> SearchResults { get; set; }
        public bool[] Checked { get; set; }
        public int[] CheckedIds { get; set; }
        public RouteValueDictionary getQueryParams(object page)
        {
            RouteValueDictionary dict = new RouteValueDictionary();

            dict["page"] = page;
            dict["RecordsPerPage"] = RecordsPerPage;
            dict["ProjectNamePattern"] = ProjectNamePattern == null ? null : ProjectNamePattern;
            dict["FilterDepartment"] = FilterDepartment == null ? null : FilterDepartment;
            return dict;
        }

    }

    public class PaymentRequestSearchModel
    {
        public int? Page { get; set; }

        public PaymentRequestState[] SelectedStates { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateFrom { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateTo { get; set; }

        public int? Project { get; set; }
        public string User { get; set; }

        public IPagedList<PaymentRequest> SearchResults { get; set; }

        public Dictionary<int, int?> Comments { get; set; } = new Dictionary<int, int?>();

        public MultiSelectList GetRequestStatesFor(System.Security.Principal.IPrincipal User)
        {
            List<UserRequestState> states = new List<UserRequestState>();

            states.Add(new UserRequestState() { RequestState = PaymentRequestState.InProcess });
            states.Add(new UserRequestState() { RequestState = PaymentRequestState.WaitConfirm });
            states.Add(new UserRequestState() { RequestState = PaymentRequestState.Confirmed });
            states.Add(new UserRequestState() { RequestState = PaymentRequestState.Rejected });
            states.Add(new UserRequestState() { RequestState = PaymentRequestState.ReWorked });
            states.Add(new UserRequestState() { RequestState = PaymentRequestState.Credited });

            for (int i = 0; i < states.Count; i++)
            {
                var x = states[i].RequestState.GetType().GetMember(states[i].RequestState.ToString()).First().GetCustomAttribute<DisplayAttribute>();
                states[i].Name = x.Name;
                states[i].id = (byte)states[i].RequestState;
            }

            return new MultiSelectList(states, "id", "Name");
        }

        public RouteValueDictionary getQueryParams(object page)
        {
            RouteValueDictionary dict = new RouteValueDictionary();

            dict["page"] = page;
            if (SelectedStates != null)
            {
                int i = 0;
                foreach (var v in SelectedStates)
                {
                    dict["SelectedStates[" + i + "]"] = v;
                    i++;
                }

            }
            dict["DateFrom"] = DateFrom == null ? "" : DateFrom.Value.ToString("dd.MM.yyyy");
            dict["DateTo"] = DateTo == null ? "" : DateTo.Value.ToString("dd.MM.yyyy");
            dict["Project"] = Project;
            return dict;
        }


    }

    public class UserRequestState
    {
        public PaymentRequestState RequestState { get; set; }
        public string Name { get; set; }
        public byte id { get; set; }
    }
    public class RegularPaymentEditModel
    {
        public int RegularPaymentId { get; set; }

        [Display(Name = "Шифр проекта")]
        public string ProjectCode { get; set; }

        [Display(Name = "Название")]
        public string ProjectShortName { get; set; }

        [Display(Name = "Кому")]
        public string UserName { get; set; }

        [Display(Name = "Сумма")]
        [Range(0.1, 1000000)]
        public double Sum { get; set; }

        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Начало выплат ")]
        [DataType(DataType.Date)]
        public DateTime? PayoutFrom { get; set; } = DateTime.Now;

        [DisplayFormat(DataFormatString = "{0:dd.MM.yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Завершение выплат")]
        [DataType(DataType.Date)]
        public DateTime? PayoutTo { get; set; }

        public bool isDublicateErr { get; set; } = false;

    }

    public class PaymentRequestEditModel
    {
        public int PaymentRequestId { get; set; }

        [Display(Name = "Кому")]
        public string UserName { get; set; }
        public string[] UserName_Add { get; set; }

        
        [Display(Name = "Шифр проекта")]
        public string ProjectCode { get; set; }

        [Display(Name = "Название")]
        public string ProjectShortName { get; set; }

        public static string getProjectCode(string ProjectName)
        {
            string ret = "";
            if (!String.IsNullOrEmpty(ProjectName))
            {
                string[] arr = ProjectName.Split(new Char[] { '_', '-' });
                if (arr.Length > 1)
                {
                    ret = arr[0].Trim();
                }
            }
            return ret;
        }

        public static string getProjectShortName(string ProjectName)
        {
            string ret = "";
            if (!String.IsNullOrEmpty(ProjectName))
            {
                string[] arr = ProjectName.Split(new Char[] { '_', '-' });
                if (arr.Length == 1)
                {
                    ret = arr[0].Trim();
                }
                else if (arr.Length > 1)
                {
                    int p1 = ProjectName.IndexOf("_") != -1 ? ProjectName.IndexOf("_") : 10000;
                    int p2 = ProjectName.IndexOf("-") != -1 ? ProjectName.IndexOf("-") : 10000;
                    int p = p1 > p2 ? p2 : p1;
                    ret = ProjectName.Substring(p + 1);
                }
            }
            return ret;
        }

        [Display(Name = "Проект")]
        public string ProjectName { get; set; }

        [Display(Name = "Направление")]
        public int DepartmentId { get; set; }


        [Display(Name = "Сумма")]
        [Range(0.1, 1000000)]
        public double Sum { get; set; }
        public double[] Sum_Add { get; set; }

        [Display(Name = "Тип")]
        public PaymentRequestType type { get; set; }
        public PaymentRequestType[] type_Add { get; set; }

        [Display(Name = "Согласована с ПМ")]
        public bool AgreedPM { get; set; }


        [Display(Name = "Файл")]
        public string AttachFileName { get; set; }

        [Display(Name = "Прикрепить файл")]
        public System.Web.HttpPostedFileBase File { get; set; }

        [Display(Name = "Удалить прикрепленный файл")]
        public bool DeleteAttachedFile { get; set; }

        [Display(Name = "Комментарии")]
        public string Comments { get; set; }

        [Display(Name = "Все сообщения")]
        public string HistoryComments { get; set; }

        [Display(Name = "Отправить на утверждение")]
        public bool SetStateWaitConfirm { get; set; } = false;

        [Display(Name = "Отправить на начисление")]
        public bool SetStateCredited { get; set; } = false;

    }

    public class PG_HeaderViewModel
    {
        [Display(Name = "Статус оплаты")]
        public PaymentsGroupState State { get; set; }
        public int PaymentsGroupId { get; set; }

        [Display(Name = "Дата создания")]
        public DateTime WhenCreated { get; set; } = DateTime.Now;

        [Display(Name = "Дата оплаты")]
        public DateTime WhenPaidOut { get; set; }

        [Display(Name = "Исполнитель")]
        public ApplicationUser UserClosed { get; set; }

        public bool ShowEditable { get; set; } = false;
        public string[] UserNames { get; set; }
        public string[] UserIds { get; set; }
        public double[][] Payments { get; set; }
        public double[][] RegularPayments { get; set; }
    }


    public class ProjectRenameModel
    {
        public string NewName { get; set; }

        public int DepartmentId { get; set; }

        public Dictionary<int, string> Depts { get; set; }
        public List<UserProjectBalance> Projects { get; set; } = new List<UserProjectBalance>();
    }
    public class UserProjectBalance
    {
        public string Name { get; set; }
        public int ProjectId { get; set; }
        public double Sum { get; set; }
    }
    public class PG_ListViewModel
    {
        public PG_HeaderViewModel Header { get; set; } = new PG_HeaderViewModel();
        public double[][] Balances { get; set; }
        public string[][] ProjectNames { get; set; }
        public int[][] ProjectIds { get; set; }

        [Display(Name = "Добавление ФИО")]
        public string NewUserPaymentName { get; set; }

        [Display(Name = "Добавление Проект")]
        public int? NewUserPaymentProject { get; set; }

        [Display(Name = "Добавление Сумма")]
        public double? NewUserPaymenSum { get; set; }


    }
    public class PaymentsGroupViewModel
    {
        public PG_HeaderViewModel Header { get; set; } = new PG_HeaderViewModel();

        public string[] ProjectsNames { get; set; }
        public int[] ProjectsIds { get; set; }


        public int PaymentsIndexByShortName(string shortname)
        {
            int index = -1;
            for(int i=0; i<Header.UserNames.Length;i++)
            {
                if( shortname.Trim().Replace(". ",".").Replace(". ", ".").ToUpper() == Header.UserNames[i].Trim().Replace(". ", ".").Replace(". ", ".").ToUpper())
                {
                    index = i;
                    break;
                }
            }
            return index;
        }
    }

    public class RateUploadModel
    {
        [Display(Name = "Файл")]
        public System.Web.HttpPostedFileBase UploadFile { get; set; }

        [Range(1, 100)]
        [Display(Name = "Процентная ставка")]
        public int SalaryRate { get; set; }

        public int? PaymentsGroupId { get; set; } // 

        [Display(Name = "Не включать в счет регулярные платежи")]
        public bool NotUseReqularPayment { get; set; } = false;
        public RegularPayment[] regularPayments;

        public bool[] accrueRegularPayments { get; set; }
        public int[] regularPaymentIds { get; set; }

    }
    public class UsersListItemModel
    {
        public int id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectShortName { get; set; }
    }

    public class UsersExchangeData
    {
        public string UserLogin { get; set; }
        public string FullName { get; set; }
    }

    public class MemberProjectView
    {
        [Display(Name = "Остаток к выплате, грн")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public double Balance { get; set; }

        [Display(Name = "Выплачено по проекту, грн")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public double Debet { get; set; }

        [Display(Name = "Начислено по проекту, грн")]
        [DisplayFormat(DataFormatString = "{0:N0}")]
        public double TotalSum { get; set; }

        [Display(Name = "Проект")]
        public string ProjectName { get; set; }

        [Display(Name = "Состояние")]
        public ProjectActiveState ProjectState { get; set; }

        public int ProjectId { get; set; }
    }

    public class UserSalaryTable
    {
        public List<DateTime> PaymentsGroupsColumns { get; set; } = new List<DateTime>();
        public List<UserSalaryViewModel> Users { get; set; } = new List<UserSalaryViewModel>();
    }
    public class UserSalaryViewModel
    {
        public string id;
        public string ShortName { get; set; }
        public string Dept { get; set; }

        public IEnumerable<UserProjectSalaryViewModel> ProjectsBalances { get; set; }
    }

    public class UserProjectSalaryViewModel
    {
        public int id;
        public string ProjectName { get; set; }
        public double Balance { get; set; }
        public Dictionary<DateTime,double> PaidsOut { get; set; }
        public Dictionary<DateTime, double> Balanced { get; set; }

    }


    public class AccruiesViewModel
    {
        public List<AccrueItem> Accruies { get; set; }
        public List<int> RequestsIds { get; set; }
    }
    public class AccrueItem
    {
        public int RequestId { get; set; }
        public string UserId { get; set; }
        public string UserShortName { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public double Sum { get; set; }

        public string Comments { get; set; }
    }


    public class URolesItem
    {
        public string name { get; set; }
        public List<string> Roles { get; set; }

    }

}