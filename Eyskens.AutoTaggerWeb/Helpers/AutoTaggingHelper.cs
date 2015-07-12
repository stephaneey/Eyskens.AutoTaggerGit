using Eyskens.AutoTaggerWeb.Models;
using IvanAkcheurov.NTextCat.Lib;
using LemmaSharp;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;
using Microsoft.SharePoint.Client.Taxonomy;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Eyskens.AutoTaggerWeb.Helpers
{   
    public class TermKey
    {
        public Guid InternalKey
        {
            get;
            set;
        }
        public Guid TermId
        {
            get;
            set;
        }
        public Term term
        {
            get;
            set;
        }
    }

    /// <summary>
    /// This class helps getting terms from either an entire TermSet either a TermItem. If the taxonomy field
    /// is branched to a TermItem, only two levels are taken into account. The GetTerms() method and the .Terms property do
    /// not return all the terms unlike the GetTermsWithCustomProperty() of a TermSet.
    /// Therefore, for performance reason, I only take two levels into account else I'd need to call ctx.ExecuteQuery recursively for every term that would have child terms.
    /// </summary>
    public class TaxonomyTerms
    {
        static object locker = new object();
        
        static DateTime LastRefreshedKeywords = DateTime.Now;

        ClientContext CurrentContext = null;
        Dictionary<TermKey, string> _TermsDictionary = null;

        public static TermCollection keywords = null;
        
        public TaxonomyTerms(ClientContext ctx, string TermSetId,Guid AnchorId)
        {
            
            bool IsRootTerm = false;
            LogHelper.Log("Inside TaxonomyTerms termsetid:" + TermSetId);
            CurrentContext = ctx;
            _TermsDictionary = new Dictionary<TermKey, string>();
            var _taxSession = TaxonomySession.GetTaxonomySession(CurrentContext);
            
            var _termStore = _taxSession.GetDefaultSiteCollectionTermStore();
            
            var _termSet = _termStore.GetTermSet(new Guid(TermSetId));
            TermCollection _termCollection=null;

            
            if(AnchorId == Guid.Empty)
            {
                CustomPropertyMatchInformation match = new CustomPropertyMatchInformation(ctx);
                match.CustomPropertyName = Constants.AutoTaggable;
                match.CustomPropertyValue = "1";
                match.TrimUnavailable = true;
                match.StringMatchOption = StringMatchOption.ExactMatch;
                _termCollection = _termSet.GetTermsWithCustomProperty(match);
                CurrentContext.Load(_termCollection,
                terms => terms.Include(
                     tt => tt.Id, tt => tt.Labels), terms => terms.Include(tt => tt.CustomProperties));
                IsRootTerm = true;
            }
            else
            {
                //No way to match a property when taking terms of a term. The CustomPropertyMatch...only works when
                //getting terms from a _termSet. Tried to deal with LoadQuery but no way to query terms based on their properties
                var _targetTerm = _termSet.GetTerm(AnchorId);
                _termCollection = _targetTerm.Terms;
                CurrentContext.Load(_termCollection,
                terms => terms.Include(
                     tt => tt.Id, tt => tt.Labels,
                     tt => tt.CustomProperties,
                     tt=>tt.Terms,tt=>tt.Terms.Include(ttt=>ttt.Labels)));
            }
            if (keywords == null || (DateTime.Now - LastRefreshedKeywords).Minutes > Constants.KeywordIntervalRefresh)
            {
                lock (locker)
                {
                    if (keywords == null || (DateTime.Now - LastRefreshedKeywords).Minutes > Constants.KeywordIntervalRefresh)
                    {
                        keywords = _taxSession.GetDefaultKeywordsTermStore().KeywordsTermSet.GetAllTerms();
                        ctx.Load(keywords);
                        CurrentContext.ExecuteQuery();
                        LastRefreshedKeywords = DateTime.Now;
                        LogHelper.Log("Refreshed the keyword list",LogSeverity.Error);
                    }
                }
            }
            else
            {
                CurrentContext.ExecuteQuery();
            }
            
            LogHelper.Log("Inside TaxonomyTerms " + _termCollection.Count);
            AddTerms(_termCollection, IsRootTerm);
        }

        public TaxonomyTerms(ClientContext ctx)
        {
            if (keywords == null || (DateTime.Now - LastRefreshedKeywords).Minutes > Constants.KeywordIntervalRefresh)
            {
                lock (locker)
                {
                    if (keywords == null || (DateTime.Now - LastRefreshedKeywords).Minutes > Constants.KeywordIntervalRefresh)
                    {
                        var _taxSession = TaxonomySession.GetTaxonomySession(ctx);
                        keywords = _taxSession.GetDefaultKeywordsTermStore().KeywordsTermSet.GetAllTerms();
                        ctx.Load(keywords);
                        ctx.ExecuteQuery();
                        LastRefreshedKeywords = DateTime.Now;
                        LogHelper.Log("Refreshed the keyword list", LogSeverity.Error);
                    }
                }
            }
        }

        private void AddLabels(Term t)
        {
            foreach (var label in t.Labels)
            {
                if (label.IsDefaultForLanguage)
                {
                    if (!t.CustomProperties.ContainsKey(Constants.TagOnlyTagSynonyms))
                    {
                        _TermsDictionary.Add(new TermKey
                        {
                            InternalKey = Guid.NewGuid(),
                            TermId = t.Id,
                            term = t
                        }, label.Value);
                    }
                }
                else
                {

                    _TermsDictionary.Add(new TermKey
                    {
                        InternalKey = Guid.NewGuid(),
                        TermId = t.Id,
                        term = t
                    }, label.Value);
                }
            }
        }
        private void AddTerms(TermCollection _termCollection,bool isroot)
        {
            LogHelper.Log("Isroot:" + isroot);
            if (_termCollection.Count() > 0)
            {                
                foreach (var t in _termCollection)
                {
                    if (t.CustomProperties.ContainsKey(Constants.AutoTaggable))//redundant in case the whole termset is loaded but no other way
                    {
                        AddLabels(t); // in case of termset.
                        if(!isroot && t.Terms.Count > 0) // in case of subterm
                        {
                            
                            foreach(var tt in t.Terms)
                            {
                                if(tt.CustomProperties.ContainsKey(Constants.AutoTaggable))
                                {
                                    
                                    AddLabels(tt);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!isroot && t.Terms.Count > 0)
                        {                            
                            foreach (var tt in t.Terms)
                            {
                                if (tt.CustomProperties.ContainsKey(Constants.AutoTaggable))
                                {
                                    AddLabels(tt);
                                }
                            }
                        }
                    }
                }
            }
        }

        public Dictionary<TermKey, string> TermsDictionary
        {
            get
            {
                return _TermsDictionary;
            }

        }

        
        public static Guid GetKeyword(string label)
        {            
            var keyword=keywords.Where(k => k.Name.ToLowerInvariant() == label.ToLowerInvariant()).SingleOrDefault();
            if(keyword != null)
            {
                return keyword.Id;
            }
            else
            {
                return Guid.Empty;
            }
        }       
        
    }

    /// <summary>
    /// Helper class that helps assigning terms to the taxonomy fields
    /// </summary>
    public class AutoTaggingHelper
    {     
        
        private static object LockObject = new object();
        static List<GlobalSetting> _GlobalConfig = null;
        static List<EmptyWord> _EmptyWords = null;
        public static bool GlobalConfigNeedsRefresh = false;
        static bool EmptyKeywordsNeedsRefresh = false;
        private static void FillEmptyWords(AppWebHelper hlp)
        {
            _EmptyWords = hlp.GetEmptyWords();
        }       

        private static List<GlobalSetting> GetGlobalConfig(AppWebHelper hlp)
        {
            if (_GlobalConfig == null || GlobalConfigNeedsRefresh)
            {
                lock(LockObject)
                {
                    if (_GlobalConfig == null || GlobalConfigNeedsRefresh)
                    {
                        _GlobalConfig = hlp.GetGlobalSettings();
                        GlobalConfigNeedsRefresh = false;
                    }
                }
            }
            
            return _GlobalConfig;
        }
        
        
        public static bool IsEmptyWord(string word,string lang,AppWebHelper hlp)
        {
            if (_EmptyWords == null || EmptyKeywordsNeedsRefresh)
            {                
                lock (LockObject)
                {
                    if (_EmptyWords == null || EmptyKeywordsNeedsRefresh)
                    {
                        AutoTaggingHelper.FillEmptyWords(hlp);
                        EmptyKeywordsNeedsRefresh = false;
                    }
                }                
            }
            var ww = _EmptyWords.Where(w => w.word == word && w.lang == lang).SingleOrDefault();
            return (ww != null) ? true:false;
        }
        static RankedLanguageIdentifier _LanguageIdentifier = null;
        public static RankedLanguageIdentifier LanguageIdentifier
        {
            get
            {
                if(_LanguageIdentifier == null)
                {
                    lock(LockObject)
                    {
                        if(_LanguageIdentifier==null)
                        {
                            var factory = new RankedLanguageIdentifierFactory();
                            _LanguageIdentifier = factory.Load(
                                HttpContext.Current.Server.MapPath("~/App_Data/Core14.profile.xml"));                            
                        }
                    }
                }
                return _LanguageIdentifier;
            }
        }
        /// <summary>
        /// Helper Method to set a Taxonomy Field on a list item
        /// </summary>
        /// <param name="ctx">The Authenticated ClientContext</param>
        /// <param name="listItem">The listitem to modify</param>
        /// <param name="model">Domain Object of key/value pairs of the taxonomy field & value</param>
        public static void SetTaxonomyFields(ClientContext ctx, ListItem listItem,string FileContent,string ListId,string url)
        {                           
            FieldCollection _fields = listItem.ParentList.Fields;
            ctx.Load(ctx.Web.AllProperties);
            ctx.Load(_fields);
            ctx.ExecuteQuery();

            AppWebHelper hlp = new AppWebHelper(url,false);
            List<GlobalSetting> settings = GetGlobalConfig(hlp);
            LogHelper.Log(settings.Count.ToString());
            var enabled = settings.Where(s => s.key == Constants.EnableKeywordCreation).SingleOrDefault();
            
            bool KeywordCreationEnabled = Convert.ToBoolean(
                settings.Where(s => s.key == Constants.EnableKeywordCreation).SingleOrDefault().value);
            int KeywordRecognitionTreshold = Convert.ToInt32(
                                settings.Where(s => s.key == Constants.KeywordRecognitionTreshold).SingleOrDefault().value);
            int KeywordCreationTreshold = Convert.ToInt32(
                                settings.Where(s => s.key == Constants.KeywordCreationTreshold).SingleOrDefault().value);

            List<string> ConfiguredFields = hlp.ListTaxFields(ListId);
            foreach(var _f in _fields)
            {                
                if(ConfiguredFields.Contains(_f.Id.ToString()))
                {
                    TaxonomyField _field = ctx.CastTo<TaxonomyField>(_fields.GetById(_f.Id));
                    if(_f.InternalName != Constants.TaxFieldInternalName)
                    {
                        
                        ctx.Load(_field);
                        ctx.ExecuteQuery();
                        Collection<Term> MatchingTerms = null;
                        MatchingTerms = AutoTaggingHelper.MatchingTerms(FileContent, ctx, _field.TermSetId, _field.AnchorId);

                        if (MatchingTerms.Count > 0)
                        {
                            LogHelper.Log("Updating taxfield " + _field.Title);
                            if (_field.AllowMultipleValues)
                            {
                                _field.SetFieldValueByCollection(listItem, MatchingTerms, 1033);
                            }
                            else
                            {
                                _field.SetFieldValueByTerm(listItem, MatchingTerms.First(), 1033);
                            }

                            listItem.Update();
                            ctx.ExecuteQuery();
                        }    
                    }
                    else
                    {
                        TaxonomyTerms tt = new TaxonomyTerms(ctx);
                        string TextLanguage=
                            AutoTaggingHelper.LanguageIdentifier.Identify(FileContent).FirstOrDefault().Item1.Iso639_3;
                        StringBuilder EntKeyWordsValue = new StringBuilder();
                        Dictionary<string, int> tokens = 
                            Tokenize(FileContent,
                            KeywordRecognitionTreshold,
                            TextLanguage);
                        StringBuilder TokenString = new StringBuilder();
                        foreach (KeyValuePair<string, int> token in tokens)
                        {
                            Guid KeywordId = TaxonomyTerms.GetKeyword(token.Key);
                            TokenString.AppendFormat("{0}|", token.Key);
                            if (KeywordId != Guid.Empty)
                            {
                                EntKeyWordsValue.AppendFormat("-1;#{0}|{1};", token.Key, KeywordId);
                            }
                            else
                            {
                                
                                if (KeywordCreationEnabled && token.Value >= KeywordCreationTreshold &&
                                    !AutoTaggingHelper.IsEmptyWord(token.Key.ToLowerInvariant(), TextLanguage,hlp))
                                {
                                    
                                    Guid g = AddKeyWord(token.Key, ctx);
                                    if (g != Guid.Empty)
                                    {
                                        EntKeyWordsValue.AppendFormat("-1;#{0}|{1};", token.Key, g);
                                    }

                                }
                            }
                        }
                        LogHelper.Log(TokenString.ToString());
                        if (EntKeyWordsValue.ToString().Length > 0)
                        {
                            LogHelper.Log("keyword value " + EntKeyWordsValue.ToString(), LogSeverity.Error);

                            TaxonomyFieldValueCollection col = new TaxonomyFieldValueCollection(ctx, string.Empty, _field);
                            col.PopulateFromLabelGuidPairs(EntKeyWordsValue.ToString());
                            _field.SetFieldValueByValueCollection(listItem, col);
                            listItem.Update();
                            ctx.ExecuteQuery();
                        }                       
                    }
                        
                }
            }

        }        
        
        public static Collection<Term> MatchingTerms(string text,ClientContext ctx,Guid TermSetId,Guid AnchorId)
        {
            LogHelper.Log("Inside Matching Term " + TermSetId);
            LogHelper.Log("Inside Matching Term text len : " + text.Length);
            List<Guid> FoundIds = new List<Guid>();
            Collection<Term> ReturnedTerms = new Collection<Term>();
            text = RemoveDiacritics(text).ToUpperInvariant();

            TaxonomyTerms TT = new TaxonomyTerms(ctx, TermSetId.ToString(), AnchorId);
            List<Guid> MatchingTermList = new List<Guid>();
            foreach (KeyValuePair<TermKey, string> kv in TT.TermsDictionary)
            {
                if (kv.Key.term.CustomProperties.ContainsKey(Constants.RegexBasedTagging))
                {
                    if (Regex.IsMatch(text, kv.Key.term.CustomProperties[Constants.RegexBasedTagging]) &&
                    !FoundIds.Contains(kv.Key.TermId))
                    {
                        FoundIds.Add(kv.Key.TermId);
                        ReturnedTerms.Add(kv.Key.term);
                    }
                }
                else if (Regex.IsMatch(text, string.Format("\\b{0}\\b", RemoveDiacritics(kv.Value).ToUpperInvariant()))
                    &&                
                    !FoundIds.Contains(kv.Key.TermId))
                {
                    FoundIds.Add(kv.Key.TermId);
                    ReturnedTerms.Add(kv.Key.term);
                }
            }
            return ReturnedTerms;
        }

        static string RemoveDiacritics(string text)
        {
            return string.Concat(
                text.Normalize(NormalizationForm.FormD)
                .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) !=
                                              UnicodeCategory.NonSpacingMark)
              ).Normalize(NormalizationForm.FormC);
        }

        private static Guid AddKeyWord(string label, ClientContext ctx)
        {
            if (string.IsNullOrEmpty(label))
                return Guid.Empty;

            LogHelper.Log("label : " + label + " ctx:" + ctx);
            TaxonomySession tax = TaxonomySession.GetTaxonomySession(ctx);
            Guid TermId = Guid.NewGuid();
            try
            {
                tax.GetDefaultKeywordsTermStore().KeywordsTermSet.CreateTerm(label, 1033, TermId);
                ctx.ExecuteQuery();
                EmptyKeywordsNeedsRefresh = true;
                return TermId;
            }
            catch (Exception ex)
            {
                LogHelper.Log("failed for label : " + label + " ctx:"+ctx+" tax:"+tax, LogSeverity.Error);
                try
                {
                    //cannot reuse tax because it is stale.
                    TaxonomySession tax1 = TaxonomySession.GetTaxonomySession(ctx);
                    LabelMatchInformation m = new LabelMatchInformation(ctx);
                    m.TermLabel = label;
                    m.TrimUnavailable = true;
                    TermCollection matches=tax1.GetDefaultKeywordsTermStore().KeywordsTermSet.GetTerms(m);
                    ctx.Load(matches);
                    ctx.ExecuteQuery();
                    if (matches.Count > 0)
                    {
                        return matches[0].Id;
                    }
                    else
                        return Guid.Empty;


                }
                catch (Exception subex)
                {
                    LogHelper.Log(subex.Message+subex.StackTrace, LogSeverity.Error);
                    return Guid.Empty;
                }

            }
            
        }

        
        /// <summary>
        /// Only taking tokens of at least 3 chars.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="threeshold"></param>
        /// <returns></returns>
        private static Dictionary<string, int> Tokenize(string text, int threeshold,string language)
        {
            Dictionary<string, int> WordCount = new Dictionary<string, int>();
            ILemmatizer lmtz = null;
            switch(language)
            {
                case "eng":
                    lmtz = new LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.English);
                    break;
                case "fra":
                    lmtz = new LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.French);
                    break;
            }
            
            text = text.Replace("\r\n", " ");
            Dictionary<string,int> entities = NlpHelper.GetNamedEntititesForText(text);
            LogHelper.Log("entities:"+entities.Count.ToString());
            string[] words = text.Split(new char[] { ' ', ',', '.', ')', '(' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length;i++ )
            {
                var word = words[i].ToLowerInvariant();
                var LeftWord = (i > 0) ? words[i - 1].ToLowerInvariant() : string.Empty;
                var RightWord = (i < (words.Length - 1)) ? words[i + 1].ToLowerInvariant() : string.Empty;
                if (word.Length < 3) //avoid unnecessary lemmatization
                    continue;
                
                string LeftBiGramKey=string.Concat(LeftWord," ",word);
                string RightBiGramKey = string.Concat(word, " ", RightWord);
                string TriGramKey = string.Concat(LeftWord, " ", word, " ", RightWord);
                string NamedEntity = null;

                if (entities.ContainsKey(word.ToLowerInvariant()))
                {
                    if (entities[word.ToLowerInvariant()] != 2)
                        NamedEntity = word;
                }
                else if(entities.ContainsKey(LeftBiGramKey))
                {
                    if (entities[LeftBiGramKey] != 2)
                        NamedEntity = string.Concat(LeftWord, " ", word);
                }
                else if(entities.ContainsKey(RightBiGramKey))
                {
                    if (entities[RightBiGramKey] != 2)
                        NamedEntity = string.Concat(word, " ", RightWord);
                }
                else if(entities.ContainsKey(TriGramKey))
                {
                    if (entities[TriGramKey] != 2)
                        NamedEntity = string.Concat(LeftWord, " ", word, " ", RightWord);
                }

                if(NamedEntity != null)
                {
                    if (!WordCount.ContainsKey(NamedEntity))
                    {
                        WordCount.Add(NamedEntity, 1);
                    }
                    else
                    {
                        WordCount[NamedEntity]++;
                    }
                }
                else{
                    string lemma = (lmtz != null) ? LemmatizeOne(lmtz, word) : word;

                    if (lemma.Length < 3) //ignore lemma of less than 3 characters
                        continue;

                    if (!WordCount.ContainsKey(lemma))
                    {
                        WordCount.Add(lemma, 1);
                    }
                    else
                    {
                        WordCount[lemma]++;
                    }    
                }
                
            }
            Dictionary<string, int> ElligibleWords = WordCount.Where(
                w => w.Value >= threeshold).Select(w => new { w.Key, w.Value }).ToDictionary(w => w.Key, w => w.Value);

            return ElligibleWords;
        }

        public static Dictionary<string, int> Tokenize(string text)
        {
            Dictionary<string, int> WordCount = new Dictionary<string, int>();
            LogHelper.Log("inside tokenize text : " + text);
            text = text.Replace("\r\n", " ");           
            string[] words = text.Split(new char[] { ' ', ',', '.', ')', '(' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].ToLowerInvariant();
                
                    if (!WordCount.ContainsKey(word))
                    {
                        WordCount.Add(word, 1);
                    }
                    else
                    {
                        WordCount[word]++;
                    }
            }
            return WordCount;
        }

        private static string LemmatizeOne(LemmaSharp.ILemmatizer lmtz, string word)
        {
            string wordLower = word.ToLower();
            string lemma = lmtz.Lemmatize(wordLower);
            Console.ForegroundColor = wordLower == lemma ? ConsoleColor.White : ConsoleColor.Red;
            return lemma;
        }
    }
}