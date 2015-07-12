using Eyskens.AutoTaggerWeb.Helpers;
using Eyskens.AutoTaggerWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    public class EmptyWordsController : Controller
    {
        [SharePointContextFilter]
        public ActionResult Index(string letter = "A")
        {            
            List<EmptyWord> model = null;
            try
            {              
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);                   
                model = hlp.GetEmptyWords(letter);                                  
                return View(model);
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }   
            
        }

        public ActionResult ByLetter(string letter)
        {
            List<EmptyWord> model = null;
            try
            {                
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                model = hlp.GetEmptyWords(letter);                
                return PartialView("words", model);
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }

        }

        
        public ActionResult Create()
        {
            return View();
        }

        // POST: EmptyWords/Create
        [HttpPost]
        public ActionResult Create(FormCollection collection)
        {
            try
            {                                
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.CreateEmptyWord(collection);
                return View("Index", hlp.GetEmptyWords(collection["word"].Substring(0,1)));                

            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }

        // GET: EmptyWords/Edit/5
        public ActionResult Edit(int id)
        {
            try
            {
               
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                return View(hlp.GetEmptyWord(id));
               
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
            
           
        }

        // POST: EmptyWords/Edit/5
        [HttpPost]
        public ActionResult Edit(int id, FormCollection collection)
        {
            try
            {                
                string w = collection["word"];
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);   
                hlp.UpdateEmptyWord(id,w);
                return View("Index", hlp.GetEmptyWords(w.Substring(0,1)));                                
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }

        // GET: EmptyWords/Delete/5
        public ActionResult Delete(int id)
        {
            try
            {                
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                return View(hlp.GetEmptyWord(id));                
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
            
        }

        // POST: EmptyWords/Delete/5
        [HttpPost]
        public ActionResult Delete(int id, FormCollection collection)
        {
            try
            {                
                AppWebHelper hlp = new AppWebHelper(
                    HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.DeleteEmptyWord(id);
                return View("Index", hlp.GetEmptyWords(collection["word"].Substring(0,1)));               
            }
            catch(Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }
    }
}
