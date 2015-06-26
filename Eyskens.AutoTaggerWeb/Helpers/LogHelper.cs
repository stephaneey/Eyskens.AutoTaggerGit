using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Eyskens.AutoTaggerWeb.Helpers
{
    public enum LogSeverity
    {
        Information=1,
        Error=2
    }
    /// <summary>
    /// Logs into a queue. You must specify your Storage Connection String & the queue name to be used
    /// in the web.config of the remote web.
    /// </summary>
    public class LogHelper
    {   
        static object LockObject=new object();

        static CloudStorageAccount _storageAccount = null;
        static CloudStorageAccount storageAccount
        {
            get
            {
                if(_storageAccount==null)
                {
                    lock (LockObject)
                    {
                        if(_storageAccount==null)
                        {
                            _storageAccount=CloudStorageAccount.Parse(ConfigurationHelper.GetStorageCn());
                        }
                    }
                }
                return _storageAccount;
            }
        }

        static CloudQueueClient _queueClient = null;
        static CloudQueueClient queueClient
        {
            get
            {
                if(_queueClient==null)
                {
                    lock(LockObject)
                    {
                        if (_queueClient == null)
                        {
                            _queueClient = storageAccount.CreateCloudQueueClient();
                        }
                    }                    
                }
                return _queueClient;
            }            
        }


        static CloudQueue _queue = null;
        static CloudQueue queue
        {
            get
            {
                if(_queue==null)
                {
                    lock (LockObject)
                    {
                        if(_queue==null)
                        {
                            _queue = queueClient.GetQueueReference(ConfigurationHelper.GetLogQueueName());
                            _queue.CreateIfNotExists();
                        }
                    }                   
                }
                return _queue;
            }
        }      
        public static void Log(string msg,LogSeverity severity=LogSeverity.Information)
        {
            if (ConfigurationHelper.GetLogQueueName() != null)
            {
                if (ConfigurationHelper.GetCurrentSeverityLevel().ToLower() == LogSeverity.Information.ToString().ToLower()
                    || (severity.ToString().ToLower() == ConfigurationHelper.GetCurrentSeverityLevel().ToLower()))
                {
                    CloudQueueMessage m = new CloudQueueMessage(msg);
                    queue.AddMessage(m);
                }                
            }            
        }
    }
}