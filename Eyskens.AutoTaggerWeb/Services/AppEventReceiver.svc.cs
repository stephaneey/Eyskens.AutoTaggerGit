using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;
using Eyskens.AutoTaggerWeb.Helpers;
using Microsoft.SharePoint.Client.Taxonomy;
using System.Reflection;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using System.Xml;
using org.apache.pdfbox.pdmodel;
using org.apache.pdfbox.util;
using System.Xml.Linq;
using System.Web;


namespace Eyskens.AutoTaggerWeb.Services
{
    public class AppEventReceiver : IRemoteEventService
    {
        Guid EntKeywords = new Guid("d32c22a3-d00b-49a5-9a23-4eec8e042d00");
        Guid HashTags = new Guid("3ceb0050-69a1-40e7-a427-83e2fac80c27");
        /// <summary>
        /// Handles app events that occur after the app is installed or upgraded, or when app is being uninstalled.
        /// </summary>
        /// <param name="properties">Holds information about the app event.</param>
        /// <returns>Holds information returned from the app event.</returns>
        public SPRemoteEventResult ProcessEvent(SPRemoteEventProperties properties)
        {
            SPRemoteEventResult result = new SPRemoteEventResult();
            try
            {
                switch (properties.EventType)
                {
                    case SPRemoteEventType.ItemAdded:                        
                        HandleAutoTaggingItemAdded(properties);
                        break;
                       
                    case SPRemoteEventType.AppInstalled:
                        AppInstalled(properties);
                        break;

                    case SPRemoteEventType.AppUninstalling:
                        AppUninstalling(properties);
                        break;

                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
            }

            return result;
        }

        /// <summary>
        /// The RER are a little buggy. Depending on the event triggered, using this:
        /// TokenHelper.CreateAppEventClientContext(properties, true) returns a null appweb although the 
        /// app web exists...Therefore, as I know the name of my App and since the AppWeb's url corresponds to it
        /// I assume that the default appweb location is https://hostweb/appweb. This isn't valid when the App is deployed
        /// trough the AppCatalog. That's why I record the URL of the AppCatalog in case the App gets installed in the
        /// AppCatalog since this info isn't available from CSOM.
        /// </summary>
        /// <param name="properties"></param>
        void AppInstalled(SPRemoteEventProperties properties)
        {
            LogHelper.Log("The application was installed on "+properties.AppEventProperties.HostWebFullUrl);
            using (ClientContext ctx = TokenHelper.CreateAppEventClientContext(properties, false))
            {
                ctx.Load(ctx.Web);
                ctx.Load(ctx.Web.CurrentUser);         
                ctx.ExecuteQuery();
                if(ctx.Web.WebTemplate == Constants.AppCatalogTemplate)
                {
                    LogHelper.Log("Writing app catalog url", LogSeverity.Error);
                    using(StreamWriter sw =  new StreamWriter(HttpContext.Current.Server.MapPath("~/App_Data/AppCataLog.xml")))
                    {
                        sw.Write(ctx.Web.Url);
                    }
                }
                AppWebHelper hlp = new AppWebHelper(properties.AppEventProperties.AppWebFullUrl.AbsoluteUri, true);
                hlp.AddAdministrator(ctx.Web.CurrentUser.LoginName);

            }
        }

        void AppUninstalling(SPRemoteEventProperties properties)
        {
            LogHelper.Log("The application was uninstalled from " + properties.AppEventProperties.HostWebFullUrl);   
            try
            {
                using (ClientContext ctx = TokenHelper.CreateAppEventClientContext(properties, false))
                {
                    List<EventReceiverDefinition> events = new List<EventReceiverDefinition>();
                    var lists = ctx.LoadQuery(ctx.Web.Lists.Where(l => l.BaseType == BaseType.DocumentLibrary).Include(
                        ll => ll.Title, ll => ll.EventReceivers));
                    ctx.ExecuteQuery();
                    foreach (var list in lists)
                    {
                        foreach (var ev in list.EventReceivers)
                        {
                            if (ev.ReceiverName.Equals(Constants.ItemAddedEventReceiverName) ||
                                ev.ReceiverName.Equals(Constants.FieldDeletedEventReceiverName))
                            {
                                events.Add(ev);
                            }
                        }
                    }
                    foreach (var eve in events)
                    {
                        eve.DeleteObject();
                    }
                    ctx.ExecuteQuery();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
            
        }

        /// <summary>
        /// This method is a required placeholder, but is not used by app events.
        /// </summary>
        /// <param name="properties">Unused.</param>
        public void ProcessOneWayEvent(SPRemoteEventProperties properties)
        {
            try
            {
                
                switch (properties.EventType)
                {
                    case SPRemoteEventType.ItemAdded:                        
                        HandleAutoTaggingItemAdded(properties);
                        break;                    
                    case SPRemoteEventType.FieldDeleted:
                        HandleAutoTaggingFieldDeleted(properties);
                        break;

                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);

            }
        }
        private string GetFieldId(string schema)
        {
            XDocument doc = XDocument.Parse(schema);
            return doc.Element(Constants.FieldElement).Attribute(Constants.IdAttribute).Value;
        }
        public void HandleAutoTaggingFieldDeleted(SPRemoteEventProperties properties)
        {
            
            string TargetFieldId = GetFieldId(properties.ListEventProperties.FieldXml);
            LogHelper.Log("Field "+TargetFieldId+" was deleted, cleaning config list");
            Uri webUri = new Uri(properties.ListEventProperties.WebUrl);
            string realm = TokenHelper.GetRealmFromTargetUrl(webUri);
            string accessToken = TokenHelper.GetAppOnlyAccessToken(
                TokenHelper.SharePointPrincipal, webUri.Authority, realm).AccessToken;
            using (var ctx = TokenHelper.GetClientContextWithAccessToken(properties.ListEventProperties.WebUrl, accessToken))
            {
                if (ctx != null)
                {
                    ctx.Load(ctx.Web.AllProperties);
                    ctx.ExecuteQuery();
                    AppWebHelper hlp = new AppWebHelper(properties.ListEventProperties.WebUrl,false);
                    if (hlp.DisableTaggingOnListField(TargetFieldId, properties.ListEventProperties.ListId.ToString()) == 0) //Only delete the RER if no more fields are enabled.
                    {
                        

                
                                List TargetList = ctx.Web.Lists.GetById(properties.ListEventProperties.ListId);
                                ctx.Load(TargetList.EventReceivers);
                                ctx.Load(TargetList);
                                ctx.ExecuteQuery();
                                LogHelper.Log("Before EnableDisableTagging");
                                ConfigurationHelper.EnableDisableTagging(ctx, TargetList, false,hlp);
                                LogHelper.Log("After EnableDisableTagging");
                    }
                }                
            }
        }

        

        public void HandleAutoTaggingItemAdded(SPRemoteEventProperties properties)
        {
            string webUrl = properties.ItemEventProperties.WebUrl;
            Uri webUri = new Uri(webUrl);
            
            using (var ctx = TokenHelper.CreateRemoteEventReceiverClientContext(properties))
            {                
                if (ctx != null)
                {
                    ListItem DocumentItem = null;
                    
                    string FileContent = FileHelper.GetFileContent(
                        ctx, properties.ItemEventProperties.ListId, properties.ItemEventProperties.ListItemId, out DocumentItem);
                    if (FileContent != null)
                    {
                        LogHelper.Log("filee content is not null and document item is "+(DocumentItem == null));
                        AutoTaggingHelper.SetTaxonomyFields(
                            ctx, DocumentItem,
                            FileContent.Replace("\u00a0", "\u0020"),
                            properties.ItemEventProperties.ListId.ToString(), webUrl);

                    }
                    else
                    {
                        LogHelper.Log("The parsing did not return any character");
                    }

                }
            }
            
        }

        

        

        
        

    }
}
