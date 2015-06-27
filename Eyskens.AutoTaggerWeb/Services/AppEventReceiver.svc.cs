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
                        LogHelper.Log("Entering ItemAdded");
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

        void AppInstalled(SPRemoteEventProperties properties)
        {
            LogHelper.Log("The application was installed on "+properties.AppEventProperties.HostWebFullUrl);
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
                        LogHelper.Log("Entering ItemAdded");
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
            AppWebHelper hlp = new AppWebHelper(properties.ListEventProperties.WebUrl);
            if (hlp.DisableTaggingOnListField(TargetFieldId, properties.ListEventProperties.ListId.ToString()) == 0) //Only delete the RER if no more fields are enabled.
            {
                Uri webUri = new Uri(properties.ListEventProperties.WebUrl);
                string realm = TokenHelper.GetRealmFromTargetUrl(webUri);
                string accessToken = TokenHelper.GetAppOnlyAccessToken(
                    TokenHelper.SharePointPrincipal, webUri.Authority, realm).AccessToken;

                using (var ctx = TokenHelper.GetClientContextWithAccessToken(properties.ListEventProperties.WebUrl, accessToken))
                {
                    if (ctx != null)
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
            string localFilePath = null;

            string webUrl = properties.ItemEventProperties.WebUrl;
            Uri webUri = new Uri(webUrl);

            try
            {
                int Attempt = 0;                
                using (var ctx = TokenHelper.CreateRemoteEventReceiverClientContext(properties))                
                {
                    if (ctx != null)
                    {
                        LogHelper.Log("Ctx not null");
                        List _library = ctx.Web.Lists.GetById(properties.ItemEventProperties.ListId);
                        var _itemToUpdate = _library.GetItemById(properties.ItemEventProperties.ListItemId);
                        ctx.Load(_itemToUpdate);
                        ctx.Load(_itemToUpdate.ContentType);
                        Microsoft.SharePoint.Client.File file = _itemToUpdate.File;
                        ctx.Load(file);
                        ClientResult<Stream> data = file.OpenBinaryStream();
                        ctx.ExecuteQuery();
                        if (_itemToUpdate.File.Length == 0 && Attempt == 0)
                        {
                            System.Threading.Thread.Sleep(20000);
                            _itemToUpdate = _library.GetItemById(properties.ItemEventProperties.ListItemId);
                            ctx.Load(_itemToUpdate);
                            file = _itemToUpdate.File;
                            ctx.Load(file);
                            data = file.OpenBinaryStream();
                            ctx.ExecuteQuery();
                            Attempt++;

                        }
                        if (data != null)
                        {
                            LogHelper.Log("data not null");
                            string FileContent = null;
                            if (file.Name.EndsWith(".pdf"))
                            {
                                int bufferSize = Convert.ToInt32(_itemToUpdate.File.Length);
                                int position = 1;
                                localFilePath = string.Concat(System.IO.Path.GetTempFileName(), Guid.NewGuid(), ".pdf");


                                Byte[] readBuffer = new Byte[bufferSize];
                                using (System.IO.Stream stream = System.IO.File.Create(localFilePath))
                                {
                                    while (position > 0)
                                    {
                                        position = data.Value.Read(readBuffer, 0, bufferSize);
                                        stream.Write(readBuffer, 0, position);
                                        readBuffer = new Byte[bufferSize];
                                    }
                                    stream.Flush();
                                }

                                FileContent = parseUsingPDFBox(localFilePath);

                            }
                            else if (file.Name.EndsWith(".docx"))
                            {
                                FileContent = ParseWordDoc(data.Value);
                            }
                            else
                            {
                                LogHelper.Log(string.Format("File format of file {0} not supported", file.Name));
                            }

                            LogHelper.Log("current title is " + _itemToUpdate["Title"]);
                            _itemToUpdate["Title"] = file.Name.Substring(0, file.Name.LastIndexOf("."));
                            LogHelper.Log("after title is " + _itemToUpdate["Title"]);
                            if (FileContent != null)
                            {
                                AutoTaggingHelper.SetTaxonomyField(
                                    ctx, _itemToUpdate,
                                    FileContent.Replace("\u00a0", "\u0020"),
                                    properties.ItemEventProperties.ListId.ToString(),webUrl);
                            }
                            else
                            {
                                LogHelper.Log("The parsing did not return any character");
                            }


                        }
                        else
                        {
                            LogHelper.Log("data is null");
                        }

                    }
                    else
                    {
                        LogHelper.Log("Ctx is null");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + " " + ex.StackTrace, LogSeverity.Error);

            }
            finally
            {
                if (localFilePath != null && System.IO.File.Exists(localFilePath))
                {
                    System.IO.File.Delete(localFilePath);
                }
            }


        }

        private static string ParseWordDoc(Stream file)
        {
            const string wordmlNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            System.Text.StringBuilder textBuilder = new System.Text.StringBuilder();
            using (WordprocessingDocument wdDoc = WordprocessingDocument.Open(file, false))
            {
                // Manage namespaces to perform XPath queries.  
                NameTable nt = new NameTable();
                XmlNamespaceManager nsManager = new XmlNamespaceManager(nt);
                nsManager.AddNamespace("w", wordmlNamespace);

                // Get the document part from the package.  
                // Load the XML in the document part into an XmlDocument instance.  
                XmlDocument xdoc = new XmlDocument(nt);
                xdoc.Load(wdDoc.MainDocumentPart.GetStream());

                XmlNodeList paragraphNodes = xdoc.SelectNodes("//w:p", nsManager);
                foreach (XmlNode paragraphNode in paragraphNodes)
                {
                    XmlNodeList textNodes = paragraphNode.SelectNodes(".//w:t", nsManager);
                    foreach (System.Xml.XmlNode textNode in textNodes)
                    {
                        textBuilder.Append(textNode.InnerText);
                    }
                    textBuilder.Append(Environment.NewLine);
                }

            }
            file.Close();
            return textBuilder.ToString();

        }
        private string parseUsingPDFBox(string input)
        {
            PDDocument doc = null;
            try
            {
                doc = PDDocument.load(input);
                PDFTextStripper stripper = new PDFTextStripper();
                return stripper.getText(doc);
            }
            finally
            {
                if (doc != null)
                {
                    doc.close();
                }
            }

        }        

    }
}
