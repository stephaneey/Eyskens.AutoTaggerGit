using Microsoft.SharePoint.Client;
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
        ClientContext CurrentContext = null;
        Dictionary<TermKey, string> _TermsDictionary = null;
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
                     tt => tt.Id, tt => tt.Labels,tt => tt.CustomProperties,tt=>tt.Terms,tt=>tt.Terms.Include(ttt=>ttt.Labels)));
            }        
            
            CurrentContext.ExecuteQuery();
            LogHelper.Log("Inside TaxonomyTerms " + _termCollection.Count);
            AddTerms(_termCollection, IsRootTerm);
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
    }

    /// <summary>
    /// Helper class that helps assigning terms to the taxonomy fields
    /// </summary>
    public class AutoTaggingHelper
    {     
        
        private static object LockObject = new object();

        /// <summary>
        /// Helper Method to set a Taxonomy Field on a list item
        /// </summary>
        /// <param name="ctx">The Authenticated ClientContext</param>
        /// <param name="listItem">The listitem to modify</param>
        /// <param name="model">Domain Object of key/value pairs of the taxonomy field & value</param>
        public static void SetTaxonomyField(ClientContext ctx, ListItem listItem,string FileContent,string ListId,string url)
        {   
            FieldCollection _fields = listItem.ParentList.Fields;
            ctx.Load(_fields);
            ctx.ExecuteQuery();
            AppWebHelper hlp = new AppWebHelper(url);
            List<string> ConfiguredFields = hlp.ListTaxFields(ListId);
            foreach(var _f in _fields)
            {
                
                if(ConfiguredFields.Contains(_f.Id.ToString()))
                {
                    
                    TaxonomyField _field = ctx.CastTo<TaxonomyField>(_fields.GetById(_f.Id));
                    ctx.Load(_field);
                    ctx.ExecuteQuery();
                    Collection<Term> MatchingTerms = null;
                    MatchingTerms = AutoTaggingHelper.MatchingTerms(FileContent, ctx, _field.TermSetId,_field.AnchorId);
                    
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
    }
}