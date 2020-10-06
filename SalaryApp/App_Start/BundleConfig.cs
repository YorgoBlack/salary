using System.Web;
using System.Web.Optimization;

namespace SalaryApp
{
    public class BundleConfig
    {
        // Дополнительные сведения об объединении см. на странице https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/comma_validate").Include(
                        "~/Scripts/comma_validate.js"));

            bundles.Add(new ScriptBundle("~/bundles/floatingHeaderTable").Include(
                        "~/Scripts/jquery.floatingHeaderTable-1.0.0.min.js"));


            bundles.Add(new ScriptBundle("~/bundles/gridviewscroll").Include(
                        "~/Scripts/gridviewscroll.js"));

            // Используйте версию Modernizr для разработчиков, чтобы учиться работать. Когда вы будете готовы перейти к работе,
            // готово к выпуску, используйте средство сборки по адресу https://modernizr.com, чтобы выбрать только необходимые тесты.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/bootstrap-datepicker.min.js",
                      "~/Scripts/locales/bootstrap-datepicker.ru.min.js",
                      "~/Scripts/bootstrap-multiselect.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new ScriptBundle("~/bundles/typeahead").Include(
                      "~/Scripts/typeahead.bundle.min.js",
                      "~/Scripts/typeahead.mvc.model.js",
                      "~/Scripts/respond.js"));


            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/bootstrap-datepicker.min.css",
                      "~/Content/bootstrap-multiselect/bootstrap-multiselect.less",
                      "~/Content/site.css"));
        }
    }
}
