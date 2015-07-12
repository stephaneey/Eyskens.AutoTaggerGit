using Eyskens.AutoTaggerWeb.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    public class UploadController : Controller
    {
        // GET: Upload
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Upload(HttpPostedFileBase file)
        {
            LogHelper.Log("in upload");
            try
            {

                if (file != null && file.ContentLength > 0)
                {
                    LogHelper.Log("in file");
                    using (StreamReader sr = new StreamReader(file.InputStream))
                    {
                        LogHelper.Log("in StreamReader");
                        
                            LogHelper.Log("in spContext " + HttpContext.Request.Form[Constants.SPAppWebUrl]);
                            LogHelper.Log("in spContext " + HttpContext.Request.QueryString[Constants.SPAppWebUrl]);
                            AppWebHelper hlp = new AppWebHelper(
                                HttpContext.Request.Form[Constants.SPAppWebUrl] as string);
                            hlp.UploadEmptyWords(sr.ReadToEnd());                                                                           
                    }                    
                }
                LogHelper.Log("appweb:" + HttpContext.Request.QueryString["SPAppWebUrl"]);
                return RedirectToAction("Index", "EmptyWords",
                    new
                    {
                       SPHostUrl = HttpContext.Request.Form["SPHostUrl"],
                       SPAppWebUrl = HttpContext.Request.Form["SPAppWebUrl"]
                    });
            }
            catch(Exception ex)
            {
                LogHelper.Log(ex.Message+ex.StackTrace,LogSeverity.Error);
                throw;
            }


           
        }
    }
}