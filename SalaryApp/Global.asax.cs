using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Data.Entity;
using SalaryApp.Models;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.ComponentModel;
using System.Web.Hosting;

namespace SalaryApp
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            bool exists = System.IO.Directory.Exists(Server.MapPath("~/App_Data"));
            if (!exists)
            {
                System.IO.Directory.CreateDirectory(Server.MapPath("~/App_Data"));
            }

            try
                {
                AreaRegistration.RegisterAllAreas();
                FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
                RouteConfig.RegisterRoutes(RouteTable.Routes);
                BundleConfig.RegisterBundles(BundleTable.Bundles);

                //Database.SetInitializer<AppDbContext>(new AppDbInitializer());
                Database.SetInitializer<AppDbContext>(null);

                ModelBinders.Binders.Add(typeof(double), new DecimalModelBinder());
                ModelBinders.Binders.Add(typeof(DateTime), new DateTimeBinder());
                ModelBinders.Binders.Add(typeof(DateTime?), new NullableDateTimeBinder());

                HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => new MailSendWorker().SendPaidOut(cancellationToken));
            }
            catch(Exception e)
            {
                System.IO.File.WriteAllText(Server.MapPath("~/App_Data/log.txt"), DateTime.Now.ToString() + ": " + e.Message);
            }
        }

    }


    public class DecimalModelBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            ValueProviderResult valueResult = bindingContext.ValueProvider
                .GetValue(bindingContext.ModelName);

            ModelState modelState = new ModelState { Value = valueResult };

            object actualValue = null;

            if (valueResult.AttemptedValue != string.Empty)
            {
                try
                {
                    actualValue = Convert.ToDouble(valueResult.AttemptedValue.Replace(",","."), CultureInfo.InvariantCulture);
                }
                catch (FormatException e)
                {
                    modelState.Errors.Add(e);
                }
            }

            bindingContext.ModelState.Add(bindingContext.ModelName, modelState);

            return actualValue;
        }
    }

    public class DateTimeBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

            return value.ConvertTo(typeof(DateTime), CultureInfo.CurrentCulture);
        }
    }

    public class NullableDateTimeBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

            return value == null
                ? null
                : value.ConvertTo(typeof(DateTime), CultureInfo.CurrentCulture);
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MultipleButtonAttribute : ActionNameSelectorAttribute
    {
        public string Name { get; set; }
        public string Argument { get; set; }

        public override bool IsValidName(ControllerContext controllerContext, string actionName, MethodInfo methodInfo)
        {
            var isValidName = false;
            var keyValue = string.Format("{0}:{1}", Name, Argument);
            var value = controllerContext.Controller.ValueProvider.GetValue(keyValue);

            if (value != null)
            {
                controllerContext.Controller.ControllerContext.RouteData.Values[Name] = Argument;
                isValidName = true;
            }

            return isValidName;
        }
    }

}
