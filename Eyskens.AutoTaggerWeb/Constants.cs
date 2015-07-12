using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Eyskens.AutoTaggerWeb
{
    public class Constants
    {
        public const string ItemAddedEventReceiverName = "ItemAddedEyskensAutoTagging";
        public const string FieldDeletedEventReceiverName = "FieldDeletedEyskensAutoTagging";        
        public const string ReceiverUrl = "ReceiverUrl";
        public const string ReceiverClass = "ReceiverClass";
        public const string TargetListName = "TargetListNames";
        public const string StorageCN = "StorageCn";
        public const string LogQueue = "LogQueue";
        public const string AutoTaggable = "AutoTaggable";
        public const string TagOnlyTagSynonyms = "TagOnlyTagSynonyms";
        public const string RegexBasedTagging = "RegexBasedTagging";
        public const int DefaultOccurrenceCount = 5;
        public const string TaggingConfigList = "TaggingConfig";
        public const string TitleField = "Title";
        public const string ListIdField = "ListId";
        public const string FieldIdField = "FieldId";
        public const string AsynchronousField = "Synchronous";
        public const string LogSeverity = "LogSeverity";
        public const string AppWebUrl = "EyskensAutoTagger";
        public const string ListConfig = "ListConfig";
        public const string IdAttribute = "ID";
        public const string FieldElement = "Field";
        public const int KeywordIntervalRefresh = 10;
        public const string KeywordRecognitionTreshold = "KeywordRecognitionTreshold";
        public const string KeywordCreationTreshold = "KeywordCreationTreshold";
        public const string EnableKeywordCreation = "EnableKeywordCreation";
        public const string TaxFieldInternalName = "TaxKeyword";
        public static string EmptyWordsList = "EmptyWords";
        public static string LangField="Lang";
        public static string ValueField = "Value1";
        public static string GlobalConfigList="GlobalSettings";
        public static string AutoTaggingAppWebUrl="AutoTaggingAppWebUrl";
        public static string AppCatalogTemplate = "APPCATALOG";
        public static string SPAppWebUrl="SPAppWebUrl";
        public static string OrganizationNamedEntity = "ORGANIZATION";
        public static string PersonNamedEntity = "PERSON";
        public static string LocationNamedEntity = "LOCATION";
        public static string Administrators="TaggingAdmins";
        public static string AdminList="Administrators";
    }
}