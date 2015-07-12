using Eyskens.AutoTaggerWeb.Helpers;
using Eyskens.AutoTaggerWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    public class AdministratorsController : Controller
    {
        // GET: Administrators
        public ActionResult Index()
        {
            AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
            List<Administrator> model = hlp.GetAdministrators();
            return View(model);
            
        }
        
        public ActionResult Create()
        {
            return View();
        }

        // POST: Administrators/Create
        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.AddAdministrator(collection["LoginName"] as string);
                return View("Index", hlp.GetAdministrators());
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }

       

        // GET: Administrators/Delete/5
        public ActionResult Delete(int id)
        {
            try
            {
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                return View(hlp.GetAdministrator(id));
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }

        // POST: Administrators/Delete/5
        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.DeleteAdministrator(id);
                return View("Index", hlp.GetAdministrators());
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }
    }
}
