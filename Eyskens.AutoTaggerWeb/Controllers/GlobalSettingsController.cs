using Eyskens.AutoTaggerWeb.Helpers;
using Eyskens.AutoTaggerWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    
    public class GlobalSettingsController : Controller
    {
        [SharePointContextFilter]
        public ActionResult Index()
        {
            AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
            List<GlobalSetting> model = hlp.GetGlobalSettings();
            return View(model);
        }                       
        // GET: GlobalSettings/Edit/5
        public ActionResult Edit(int id)
        {
            try
            {
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                return View(hlp.GetGlobalSetting(id));
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }

        // POST: GlobalSettings/Edit/5
        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {                
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.UpdateGlobalSetting(id,collection["key"],collection["value"]);
                AutoTaggingHelper.GlobalConfigNeedsRefresh = true;
                return View("Index", hlp.GetGlobalSettings());
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }      
    }
}
