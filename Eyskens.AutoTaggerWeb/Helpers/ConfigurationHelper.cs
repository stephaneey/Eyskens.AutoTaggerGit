using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Configuration;

namespace Eyskens.AutoTaggerWeb.Helpers
{
    /// <summary>
    /// Gets configuration data from the web.config file or the remote web.
    /// </summary>
    public class ConfigurationHelper
    {
        
        public static string[] GetTargetListNames()
        {
            return WebConfigurationManager.AppSettings[Constants.TargetListName].Split(new char[]{'|'});
        }
        public static string GetReceiverClass()
        {
            return WebConfigurationManager.AppSettings[Constants.ReceiverClass];
        }
        public static string GetReceiverUrl()
        {
            return WebConfigurationManager.AppSettings[Constants.ReceiverUrl];
        }
        public static string GetLogQueueName()
        {
            return WebConfigurationManager.AppSettings[Constants.LogQueue];
        }

        public static string GetStorageCn()
        {
            return WebConfigurationManager.AppSettings[Constants.StorageCN];
        }

        public static string GetAppCatalogUrl()
        {
            using(StreamReader sr = new StreamReader(HttpContext.Current.Server.MapPath("~/App_Data/AppCataLog.xml")))
            {
                return sr.ReadToEnd();
            }
        }

        public static void EnableDisableTagging(ClientContext ctx,List TargetList,bool AttachEvents,AppWebHelper hlp)
        {
            LogHelper.Log("Entering EnableDisableTagging");
            
            if (!AttachEvents)
            {
                try
                {
                    
                    var TargetItemAddedEvent = TargetList.EventReceivers.Where(
                        ev => ev.ReceiverName == Constants.ItemAddedEventReceiverName).SingleOrDefault();

                    if (TargetItemAddedEvent != null)
                    {
                        TargetItemAddedEvent.DeleteObject();                        
                    }
                    var TargetFieldAddedEvent = TargetList.EventReceivers.Where(
                        ev => ev.ReceiverName == Constants.FieldDeletedEventReceiverName).SingleOrDefault();

                    if (TargetFieldAddedEvent != null)
                    {
                        TargetFieldAddedEvent.DeleteObject();
                    }
                    
                    if(TargetItemAddedEvent != null || TargetFieldAddedEvent != null)
                    {
                        ctx.ExecuteQuery();
                    }

        
                }
                catch (Exception ex)
                {
                    LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                }

            }
            else
            {
                try
                {
                    int Sequence = 10;                            
                    EventReceiverDefinitionCreationInformation ItemAddedDef = new EventReceiverDefinitionCreationInformation();
                    ItemAddedDef.EventType = EventReceiverType.ItemAdded;
                    ItemAddedDef.ReceiverName = Constants.ItemAddedEventReceiverName;
                    ItemAddedDef.ReceiverUrl = ConfigurationHelper.GetReceiverUrl();
                    ItemAddedDef.ReceiverAssembly = Assembly.GetExecutingAssembly().FullName;
                    ItemAddedDef.ReceiverClass = ConfigurationHelper.GetReceiverClass();
                    if(!hlp.ListsInfo.ContainsKey(TargetList.Id.ToString()))
                    {
                        ItemAddedDef.Synchronization = EventReceiverSynchronization.Asynchronous;
                    }
                    else
                    {
                        if (hlp.ListsInfo[TargetList.Id.ToString()].Asynchronous)
                        {
                            ItemAddedDef.Synchronization = EventReceiverSynchronization.Asynchronous;
                        }
                        else
                        {
                            ItemAddedDef.Synchronization = EventReceiverSynchronization.Synchronous;
                        }   
                    }
                                     
                    ItemAddedDef.SequenceNumber = Sequence;

                    EventReceiverDefinitionCreationInformation FieldDeletedDef = new EventReceiverDefinitionCreationInformation();
                    FieldDeletedDef.EventType = EventReceiverType.FieldDeleted;
                    FieldDeletedDef.ReceiverName = Constants.FieldDeletedEventReceiverName;
                    FieldDeletedDef.ReceiverUrl = ConfigurationHelper.GetReceiverUrl();
                    FieldDeletedDef.ReceiverAssembly = Assembly.GetExecutingAssembly().FullName;
                    FieldDeletedDef.ReceiverClass = ConfigurationHelper.GetReceiverClass();
                    FieldDeletedDef.Synchronization = EventReceiverSynchronization.Asynchronous;
                    TargetList.EventReceivers.Add(ItemAddedDef);
                    TargetList.EventReceivers.Add(FieldDeletedDef);

                    TargetList.Update();                    
                    ctx.ExecuteQuery();
                    LogHelper.Log("Attached AutoTagging for " + ItemAddedDef.EventType.ToString() + " on " + TargetList.Id);
                }
                catch (Exception ex)
                {
                    LogHelper.Log(ex.Message+ex.StackTrace,LogSeverity.Error);
                }
            
            }
        }


        internal static string GetCurrentSeverityLevel()
        {
            return WebConfigurationManager.AppSettings[Constants.LogSeverity];
        }
    }
}