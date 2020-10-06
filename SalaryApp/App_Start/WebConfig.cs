using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace SalaryApp
{
    public static class WebConfig
    {
        static WebConfig()
        {
            SmtpHost = WebConfigurationManager.AppSettings["SmtpHost"];
            SmtpPort = int.Parse(WebConfigurationManager.AppSettings["SmtpPort"]);
            SmtpUserName = WebConfigurationManager.AppSettings["SmtpUserName"];
            SmtpUserPass = WebConfigurationManager.AppSettings["SmtpUserPass"];
        }

        /// <summary>
        /// Email Host Urn
        /// </summary>
        public static string SmtpHost { get; private set; }
        /// <summary>
        /// Smtp Port
        /// </summary>
        public static int SmtpPort { get; private set; }
        /// <summary>
        /// Smtp UserName
        /// </summary>
        public static string SmtpUserName { get; private set; }
        /// <summary>
        /// Smtp UserPass
        /// </summary>
        public static string SmtpUserPass { get; private set; }
    }
}