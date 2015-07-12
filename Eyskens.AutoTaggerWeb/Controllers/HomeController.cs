using Eyskens.AutoTaggerWeb.Helpers;
using Eyskens.AutoTaggerWeb.Models;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Taxonomy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {                
        
        [SharePointContextFilter]
        public ActionResult Index()
        {            
            List<SPTaggableList> model = null;
            try
            {
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);
                
                using (var ctx = spContext.CreateUserClientContextForSPHost())
                {
                    model = GetModel(ctx, new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string));
                }
                return View(model);
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }            
            
        }

        List<SPTaggableList> GetModel(ClientContext ctx,AppWebHelper hlp)
        {
            List<SPTaggableList> model = null;
            try
            {
                model = new List<SPTaggableList>();
                LogHelper.Log("Inside getmodel");
                if (ctx != null)
                {   
                    ListCollection lists = ctx.Web.Lists;
                    
                    ctx.Load(lists, ListQuery => ListQuery.Include(
                        l=>l.Id,l => l.Title, l => l.BaseType,
                        l => l.Fields.Where(
                            f => f.TypeAsString == "TaxonomyFieldTypeMulti" || f.TypeAsString == "TaxonomyFieldType")
                       ));
                    
                    ctx.ExecuteQuery();
                   
                    foreach (List list in lists)
                    {
                        
                        if (list.BaseType == BaseType.DocumentLibrary && list.Fields.Count > 0)
                        {
                            
                            SPTaggableList NewList = new SPTaggableList();
                            NewList.Title = list.Title;
                            NewList.Id = list.Id.ToString();
                            NewList.Disabled = (hlp.ListsInfo.ContainsKey(NewList.Id) && hlp.ListsInfo[NewList.Id].ChildCount > 0) ? "Disabled" : string.Empty;
                            NewList.Asynchronous = (hlp.ListsInfo.ContainsKey(NewList.Id)) ? hlp.ListsInfo[NewList.Id].Asynchronous : true;
                            
                            List<SPTaggableField> ListFields = new List<SPTaggableField>();
                            foreach (Field field in list.Fields)
                            {
                                TaxonomyField TaxField = field as TaxonomyField;
                                string key = string.Concat(NewList.Id, "_", TaxField.Id.ToString());
                                var isEnabled = hlp.CurrentlyEnabledFields.Where(c=>c["Title"].Equals(key)).SingleOrDefault();
                                ListFields.Add(new SPTaggableField
                                {
                                    Id=TaxField.Id.ToString(),
                                    Title = TaxField.Title,
                                    TaggingEnabled = (isEnabled!=null) ? true : false
                                        
                                });
                            }
                            NewList.Fields = ListFields;                                
                            model.Add(NewList);
                        }
                    }
                }
                
                return model;
            }
            catch(Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace);
                throw;
            }

        }    
        [HttpPost]
        public ActionResult SetSync(bool sync,string id)
        {
            try
            {
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                hlp.SetSync(id, sync);
                LogHelper.Log("sync:" + sync + " id:" + id);
                SPEnabledList m = new SPEnabledList();
                m.Disabled = (hlp.ListsInfo.ContainsKey(id) && hlp.ListsInfo[id].ChildCount > 0) ? true : false;
                return PartialView("SyncStatus", m);
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace);
                throw;
            }
            
        }

        [HttpPost]
        public ActionResult EnableAutoTagging(string id)
        {             
            LogHelper.Log("Entering tEnableAutoTagging");
            
            try
            {
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                using (var ctx = spContext.CreateUserClientContextForSPHost())
                {
                    if (hlp.EnableTaggingOnListField(id) == 1) //we register only one RER per list.
                    {
                        string[] ids = id.Split(new char[] { '_' });
                        List TargetList = ctx.Web.Lists.GetById(new Guid(ids[0]));
                        ctx.Load(TargetList);
                        ctx.Load(TargetList.EventReceivers);
                        ctx.ExecuteQuery();
                        LogHelper.Log("Adding Event Receivers");
                        ConfigurationHelper.EnableDisableTagging(ctx, TargetList, true,hlp);
                    }                    
                    return PartialView("ListFields", GetModel(ctx, hlp));
                }                
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }            
        }
        [HttpPost]
        public ActionResult DisableAutoTagging(string id)
        {
            LogHelper.Log("Entering DisableAutoTagging");
            try
            {
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);
                AppWebHelper hlp = new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string);
                using (var ctx = spContext.CreateUserClientContextForSPHost())
                {
                    string[] ids = id.Split(new char[] { '_' });
                    LogHelper.Log("Inside DisableAutoTagging");
                    if (hlp.DisableTaggingOnListField(ids[1],ids[0]) == 0) //Only delete the RER if no more fields are enabled.
                    {
                        
                        List TargetList = ctx.Web.Lists.GetById(new Guid(ids[0]));
                        ctx.Load(TargetList.EventReceivers);
                        ctx.Load(TargetList);
                        ctx.ExecuteQuery();
                        LogHelper.Log("Before EnableDisableTagging");
                        ConfigurationHelper.EnableDisableTagging(ctx, TargetList, false,hlp);
                        LogHelper.Log("After EnableDisableTagging");
                    }

                    return PartialView("ListFields", GetModel(ctx, new AppWebHelper(HttpContext.Request.QueryString[Constants.SPAppWebUrl] as string)));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
            
        }
        
    }
}
