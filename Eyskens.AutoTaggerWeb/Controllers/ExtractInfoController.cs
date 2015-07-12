using Eyskens.AutoTaggerWeb.Helpers;
using Eyskens.AutoTaggerWeb.Models;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Eyskens.AutoTaggerWeb.Controllers
{
    [HandleError]
    public class ExtractInfoController : Controller
    {
        [SharePointContextFilter]
        public ActionResult Index()
        {
            try
            {
                DocumentInformation model = new DocumentInformation();
                model.Tokens = new List<string>();
                var spContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);

                using (var ctx = spContext.CreateUserClientContextForSPHost())
                {
                    LogHelper.Log("ctx is null " + (ctx == null));
                    ListItem DocumentItem = null;
                    string FileContent = FileHelper.GetFileContent(
                        ctx, 
                        new Guid(HttpContext.Request.QueryString["SPListId"]),
                        Convert.ToInt32(HttpContext.Request.QueryString["SPListItemId"]),
                        out DocumentItem);
                    
                    Dictionary<string,int> tokens = AutoTaggingHelper.Tokenize(FileContent);
                    var SortedTokens = tokens.OrderByDescending(x => x.Value);

                    foreach (var token in SortedTokens)
                    {
                        model.Tokens.Add(string.Concat(token.Key, " (", token.Value, " ) | "));
                    }
                    System.Text.StringBuilder locations = new System.Text.StringBuilder();
                    System.Text.StringBuilder organizations = new System.Text.StringBuilder();
                    System.Text.StringBuilder persons = new System.Text.StringBuilder();
                    Dictionary<string,int> entities=NlpHelper.GetNamedEntititesForText(FileContent,false);
                    LogHelper.Log("entities : " + entities.Count);
                    foreach(KeyValuePair<string,int> entity in entities)
                    {
                        switch(entity.Value)
                        {
                            case 1:
                                //location
                                locations.AppendFormat("{0} - ", entity.Key);
                                break;
                            case 2:
                                //person
                                persons.AppendFormat("{0} - ", entity.Key);
                                break;
                            case 3:
                                //org
                                organizations.AppendFormat("{0} - ", entity.Key);
                                break;
                        }
                    }
                    model.Locations = locations.ToString();
                    model.Persons = persons.ToString();
                    model.Organizations = organizations.ToString();
                    
                }
                return View(model);
            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + ex.StackTrace, LogSeverity.Error);
                throw;
            }
        }
    }
}