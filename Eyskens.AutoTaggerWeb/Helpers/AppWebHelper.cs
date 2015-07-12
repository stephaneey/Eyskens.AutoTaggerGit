using Eyskens.AutoTaggerWeb.Models;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        public AppWebHelper(string url,bool isappweburl=true)
        {
            Uri webUri = new Uri(url);
            string realm = null;
            string accessToken = null;

            if(!isappweburl)
            {
                webUri = new Uri(string.Concat(url, "/", Constants.AppWebUrl));  
                if(!AppWebExists(webUri.AbsoluteUri))
                {
                    LogHelper.Log("App web does not exist at url " + webUri.AbsoluteUri, LogSeverity.Error);
                    webUri = new Uri(string.Concat(ConfigurationHelper.GetAppCatalogUrl(), "/", Constants.AppWebUrl));
                    LogHelper.Log("trying now with appcatalog at url " + webUri.AbsoluteUri, LogSeverity.Error);
                }
            }       
            realm = TokenHelper.GetRealmFromTargetUrl(webUri);
            accessToken = TokenHelper.GetAppOnlyAccessToken(
                    TokenHelper.SharePointPrincipal, webUri.Authority, realm).AccessToken;
            ctx = TokenHelper.GetClientContextWithAccessToken(webUri.AbsoluteUri, accessToken);  
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

        
        bool AppWebExists(string url)
        {
            try
            {
                HttpWebRequest req = HttpWebRequest.Create(url) as HttpWebRequest;
                HttpWebResponse resp = req.GetResponse() as HttpWebResponse;
            }
            catch (WebException webex)
            {
                return !(((HttpWebResponse)webex.Response).StatusCode == HttpStatusCode.NotFound);
               
            }
            return true;
        }

        public List<string> ListTaxFields(string ListId)
        {
            List<string> ListTaxFieldIds = new List<string>();           
            LogHelper.Log("Inside ListTaxFields id=" + ListId);            
            CamlQuery q = new CamlQuery();
            q.ViewXml = "<View><Query><Where><Eq><FieldRef Name='ListId'/><Value Type='Text'>" + ListId + "</Value></Eq></Where></Query></View>";
            ListItemCollection FieldsForList = ctx.Web.Lists.GetByTitle(Constants.TaggingConfigList).GetItems(q);
            ctx.Load(FieldsForList);
            ctx.Load(ctx.Web);
            ctx.ExecuteQuery();
            if (FieldsForList.Count > 0)
            {
                //foreach (var item in CurrentlyEnabledFields)
                foreach (var item in FieldsForList)
                {
                    string FieldId = (item[Constants.FieldIdField] != null) ?
                        item[Constants.FieldIdField].ToString() : string.Empty;
                    LogHelper.Log("Adding fieldid:" + FieldId);
                    ListTaxFieldIds.Add(FieldId);
                }
            }
            LogHelper.Log("CurrentlyEnabledFields.Count=" + FieldsForList.Count+" web="+ctx.Web.Url);
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
            int lt = this.ListTaxFields(ids[0]).Count;
            LogHelper.Log("EnableTaggingOnListField count :" + lt + " of list :" + ids[0]);
            return lt;
        }
        
        public int DisableTaggingOnListField(string id,string listid)
        {
            LogHelper.Log("Inside DisableTaggingOnListField id:" + CleanId(id));            
            CamlQuery q = new CamlQuery();
            q.ViewXml = "<View><Query><Where><And><Eq><FieldRef Name='ListId'/><Value Type='Text'>"+CleanId(listid)+"</Value></Eq><Eq><FieldRef Name='FieldId'/><Value Type='Text'>" + CleanId(id) + "</Value></Eq></And></Where></Query></View>";
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
        internal List<EmptyWord> GetEmptyWords(string letter="")
        {
            
            List<EmptyWord> EmptyWords = new List<EmptyWord>();
            CamlQuery q = null;
            if(letter != "")
            {
                q=new CamlQuery();
                q.ViewXml = "<View><Query><Where><BeginsWith><FieldRef Name='Title'/><Value Type='Text'>" 
                    + letter + "</Value></BeginsWith></Where></Query></View>";
            }
            else
            {
                q = CamlQuery.CreateAllItemsQuery();
            }
            
            ListItemCollection words = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).GetItems(
                q);
            ctx.Load(words);
            ctx.ExecuteQuery();
            foreach(var word in words)
            {
                EmptyWords.Add(new EmptyWord
                {
                    id=word.Id,
                    lang = word[Constants.LangField] as string,
                    word = word[Constants.TitleField] as string
                });
            }
            return EmptyWords;
        }

        internal EmptyWord GetEmptyWord(int id)
        {
            EmptyWord w = new EmptyWord();

            ListItem word = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).GetItemById(id);
            ctx.Load(word);
            ctx.ExecuteQuery();
            w.id = id;
            w.lang = word[Constants.LangField] as string;
            w.word = word[Constants.TitleField] as string;

            return w;
        }

        internal GlobalSetting GetGlobalSetting(int id)
        {
            GlobalSetting s = new GlobalSetting();

            ListItem setting = ctx.Web.Lists.GetByTitle(Constants.GlobalConfigList).GetItemById(id);
            ctx.Load(setting);
            ctx.ExecuteQuery();
            s.id = id;
            s.key = setting[Constants.TitleField] as string;
            s.value = setting[Constants.ValueField] as string;

            return s;
        }

        internal void UpdateGlobalSetting(int id, string key,string value)
        {

            ListItem TargetItem = ctx.Web.Lists.GetByTitle(Constants.GlobalConfigList).GetItemById(id);
            TargetItem[Constants.TitleField] = key;
            TargetItem[Constants.ValueField] = value;
            TargetItem.Update();
            ctx.ExecuteQuery();
        }

        internal List<GlobalSetting> GetGlobalSettings()
        {
            List<GlobalSetting> settings = new List<GlobalSetting>();
            ListItemCollection ConfigItems=
                ctx.Web.Lists.GetByTitle(Constants.GlobalConfigList).GetItems(CamlQuery.CreateAllItemsQuery());
            ctx.Load(ConfigItems);
            ctx.ExecuteQuery();
            foreach (ListItem ConfigItem in ConfigItems)
            {
                settings.Add(new GlobalSetting
                {
                    id=ConfigItem.Id,
                    key = ConfigItem[Constants.TitleField] as string,
                    value = ConfigItem[Constants.ValueField] as string
                });
            }

            return settings;
        }
        internal void UpdateEmptyWord(int id, string p)
        {
            
            ListItem TargetItem = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).GetItemById(id);
            TargetItem[Constants.TitleField] = p;
            TargetItem.Update();
            ctx.ExecuteQuery();
            
        }
        internal void CreateEmptyWord(System.Web.Mvc.FormCollection collection)
        {
            LogHelper.Log("INside CreateEmptyWord");
            ListItem NewEmptyWord = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).AddItem(
                new ListItemCreationInformation());
            NewEmptyWord[Constants.TitleField] = collection["word"];
            NewEmptyWord[Constants.LangField] = collection["lang"];
            NewEmptyWord.Update();            
            ctx.ExecuteQuery();
            LogHelper.Log("INside CreateEmptyWord after ExecuteQuery");
        }
        internal void DeleteEmptyWord(int id)
        {
            ListItem TargetItem = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).GetItemById(id);
            TargetItem.DeleteObject();            
            ctx.ExecuteQuery();
        }

        public void AddAdministrator(string username)
        {
            ListItem DefaultAdmin = ctx.Web.Lists.GetByTitle(Constants.AdminList).AddItem(
                new ListItemCreationInformation());
            DefaultAdmin[Constants.TitleField] = username;
            DefaultAdmin.Update();            
            ctx.ExecuteQuery();
        }
        public void DeleteAdministrator(int id)
        {
            ListItem TargetAdmin = ctx.Web.Lists.GetByTitle(Constants.AdminList).GetItemById(id);
            TargetAdmin.DeleteObject();
            ctx.ExecuteQuery();
        }
        public bool IsAdministrator(string username,string email)
        {
            CamlQuery q = new CamlQuery();
            q.ViewXml = string.Concat(
                "<View><Query><Where><Or><Eq><FieldRef Name='Title'/><Value Type='Text'>", username, "</Value></Eq><Eq><FieldRef Name='Title'/><Value Type='Text'>", email, "</Value></Eq></Or></Where></Query></View>");
            ListItemCollection admins = ctx.Web.Lists.GetByTitle(Constants.AdminList).GetItems(q);
            ctx.Load(admins);
            ctx.ExecuteQuery();
            return (admins.Count == 1);
        }
        public Administrator GetAdministrator(int id)
        {
            ListItem TargetAdmin = ctx.Web.Lists.GetByTitle(Constants.AdminList).GetItemById(id);
            ctx.Load(TargetAdmin);
            ctx.ExecuteQuery();
            Administrator adm = new Administrator();
            adm.id = TargetAdmin.Id;
            adm.LoginName = TargetAdmin[Constants.TitleField] as string;
            return adm;
        }

        internal List<Administrator> GetAdministrators()
        {
            List<Administrator> ReturnedItems = new List<Administrator>();
            ListItemCollection admins = ctx.Web.Lists.GetByTitle(Constants.AdminList).GetItems(CamlQuery.CreateAllItemsQuery());
            ctx.Load(admins);
            ctx.ExecuteQuery();
            foreach(var admin in admins)
            {
                ReturnedItems.Add(new Administrator
                {
                    id = admin.Id,
                    LoginName = admin[Constants.TitleField] as string
                });
            }
            return ReturnedItems;
        }
        internal void UploadEmptyWords(string content)
        {
            LogHelper.Log("in UploadEmptyWords");
            bool UpdateNeeded = false;
            string[] lines = content.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach(string line in lines)
            {                
                string[] words = line.Split(new char[] { ',',';' });
                if (words.Count() == 2)
                {
                    if (!string.IsNullOrEmpty(words[0]) && !string.IsNullOrEmpty(words[1]))
                    {
                        ListItem NewEmptyWord = ctx.Web.Lists.GetByTitle(Constants.EmptyWordsList).AddItem(
                            new ListItemCreationInformation());
                        NewEmptyWord[Constants.TitleField] = words[0];
                        NewEmptyWord[Constants.LangField] = words[1];
                        NewEmptyWord.Update();
                        UpdateNeeded = true;
                    }
                }
            }
            if(UpdateNeeded)
            {
                ctx.ExecuteQuery();
                LogHelper.Log("After execute query");
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