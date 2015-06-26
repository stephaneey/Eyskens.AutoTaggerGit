using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Web;

namespace Eyskens.AutoTaggerWeb.Helpers
{
    /// <summary>
    /// This class targets the AppWeb in order to save the App configuration.
    /// </summary>
    public class AppWebHelper
    {
        ClientContext ctx = null;
        Dictionary<string, ListInfo> _ListsInfo = null;
        public Dictionary<string, ListInfo> ListsInfo
        {
            get
            {
                if(_ListsInfo==null)
                {
                    _ListsInfo = new Dictionary<string, ListInfo>();
                    ListItemCollection Lists = ctx.Web.Lists.GetByTitle(Constants.ListConfig).GetItems(
                        CamlQuery.CreateAllItemsQuery());
                    ctx.Load(Lists);
                    ctx.ExecuteQuery();
                    foreach (var List in Lists)
                    {
                        if (List[Constants.TitleField] != null)
                        {
                            _ListsInfo.Add(List[Constants.TitleField].ToString(), new ListInfo
                            {
                                ChildCount = CurrentlyEnabledFields.Where(
                                    c => c[Constants.ListIdField].Equals(List[Constants.TitleField].ToString())).Count(),
                                Asynchronous = Convert.ToBoolean(List[Constants.AsynchronousField])
                            });
                        }
                    }
                }
                return _ListsInfo;                
            }
            

        }
        public AppWebHelper(string url)
        {
            url = string.Concat(url, "/", Constants.AppWebUrl);
            Uri webUri = new Uri(url);
            string realm = TokenHelper.GetRealmFromTargetUrl(webUri);
                string accessToken = TokenHelper.GetAppOnlyAccessToken(
                    TokenHelper.SharePointPrincipal, webUri.Authority, realm).AccessToken;
            ctx = TokenHelper.GetClientContextWithAccessToken(url, accessToken);
                
        }

        ListItemCollection _CurrentlyEnabledFields = null;
        public ListItemCollection CurrentlyEnabledFields
        {
            get
            {
                if (_CurrentlyEnabledFields==null)
                {
                    _CurrentlyEnabledFields = ctx.Web.Lists.GetByTitle(Constants.TaggingConfigList).GetItems(
                        CamlQuery.CreateAllItemsQuery());
                    ctx.Load(_CurrentlyEnabledFields);
                    ctx.ExecuteQuery();
                }
                return _CurrentlyEnabledFields;
            }
        }


        public AppWebHelper(SharePointContext SPContext)
        {
            ctx = SPContext.CreateAppOnlyClientContextForSPAppWeb();
        }
        public List<string> ListTaxFields(string ListId)
        {
            List<string> ListTaxFieldIds = new List<string>();           
            LogHelper.Log("Inside ListTaxFields id=" + ListId);            
            CamlQuery q = new CamlQuery();
            q.ViewXml = "<View><Query><Where><Eq><FieldRef Name='ListId'/><Value Type='Text'>" + ListId + "</Value></Eq></Where></Query></View>";
            ListItemCollection FieldsForList = ctx.Web.Lists.GetByTitle(Constants.TaggingConfigList).GetItems(q);
            ctx.Load(FieldsForList);
            ctx.ExecuteQuery();
            if (FieldsForList.Count > 0)
            {
                foreach (var item in CurrentlyEnabledFields)
                {
                    string FieldId = (item[Constants.FieldIdField] != null) ?
                        item[Constants.FieldIdField].ToString() : string.Empty;
                    LogHelper.Log("Adding fieldid:" + FieldId);
                    ListTaxFieldIds.Add(FieldId);
                }
            }
            LogHelper.Log("CurrentlyEnabledFields.Count=" + FieldsForList.Count);
            return ListTaxFieldIds;
        }        
        
        public int EnableTaggingOnListField(string id)
        {
            string[] ids = id.Split(new char[] { '_' });
            ListItem NewItem = ctx.Web.Lists.GetByTitle(
                Constants.TaggingConfigList).AddItem(new ListItemCreationInformation());
            NewItem[Constants.TitleField] = id;
            NewItem[Constants.ListIdField] = ids[0];
            NewItem[Constants.FieldIdField] = ids[1];
            NewItem.Update();
            ctx.Load(ctx.Web);
            ctx.ExecuteQuery();
            this.SetSync(ids[0], true,false);
            return this.ListTaxFields(ids[0]).Count;
        }
        
        public int DisableTaggingOnListField(string id,string listid)
        {
            LogHelper.Log("Inside DisableTaggingOnListField id:" + CleanId(id));            
            CamlQuery q = new CamlQuery();
            q.ViewXml = "<View><Query><Where><Eq><FieldRef Name='FieldId'/><Value Type='Text'>" + CleanId(id) + "</Value></Eq></Where></Query></View>";
            ListItemCollection items = ctx.Web.Lists.GetByTitle(Constants.TaggingConfigList).GetItems(q);
            ctx.Load(items);
            ctx.ExecuteQuery();
            LogHelper.Log("Inside DisableTaggingOnListField after executequery:"+items.Count);
            if (items.Count == 1)
            {
                items[0].DeleteObject();
                ctx.ExecuteQuery();   
            }
            return this.ListTaxFields(CleanId(listid)).Count;
        }

        string CleanId(string id)
        {
            return id.Replace("{", "").Replace("}", "");
        }
        
        public void SetSync(string id, bool sync,bool overwrite=true)
        {
            CamlQuery q = new CamlQuery();
            q.ViewXml=string.Concat("<View><Query><Where><Eq><FieldRef Name='Title'/><Value Type='Text'>",id,"</Value></Eq></Where></Query></View>");
            ListItemCollection Lists = ctx.Web.Lists.GetByTitle(
                Constants.ListConfig).GetItems(q);
            ctx.Load(Lists);
            ctx.ExecuteQuery();
            if(Lists.Count == 1)
            {
                if(overwrite)
                {
                    ListItem TargetList = Lists[0];
                    TargetList[Constants.AsynchronousField] = sync;
                    TargetList.Update();
                    ctx.ExecuteQuery();
                }                
            }
            else if(Lists.Count == 0)
            {                
                ListItem TargetList = ctx.Web.Lists.GetByTitle(Constants.ListConfig).AddItem(new ListItemCreationInformation());
                TargetList["Title"]=id;
                TargetList["Synchronous"] = sync;
                TargetList.Update();
                ctx.ExecuteQuery();
            }
        }
        ~AppWebHelper()
        {
            if(ctx != null)
            {
                ctx.Dispose();
            }
        }
    }
}