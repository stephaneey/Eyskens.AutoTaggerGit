using edu.stanford.nlp.ie.crf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

namespace Eyskens.AutoTaggerWeb.Helpers
{
    
    internal class NlpHelper
    {
        static readonly CRFClassifier classifier = CRFClassifier.getClassifierNoExceptions(
            HttpContext.Current.Server.MapPath("~/App_Data/english.all.3class.distsim.crf.ser.gz"));

        public static Dictionary<string, int> GetNamedEntititesForText(string text,bool lowercase=true)
        {
            
            Dictionary<string, int> entities = new Dictionary<string, int>();
            string xml = classifier.classifyWithInlineXML(
                text.Replace("&", "&amp;").Replace("<", "&lt;"));
            
            XDocument doc = XDocument.Parse(string.Concat("<root>",xml,"</root>"));

            var locations = doc.Descendants(Constants.LocationNamedEntity);
            
            foreach (var location in locations)
            {
                string loc = (lowercase==true) ? location.Value.ToLowerInvariant() : location.Value;
                if (!entities.ContainsKey(loc))
                {
                    entities.Add(loc, 1);
                }
            }

            var persons = doc.Descendants(Constants.PersonNamedEntity);

            foreach (var person in persons)
            {
                string pers = (lowercase == true) ? person.Value.ToLowerInvariant() : person.Value;
                if (!entities.ContainsKey(pers))
                {
                    entities.Add(pers, 2);
                }
            }

            var organizations = doc.Descendants(Constants.OrganizationNamedEntity);

            foreach (var organization in organizations)
            {
                string org = (lowercase == true) ? organization.Value.ToLowerInvariant() : organization.Value;
                if (!entities.ContainsKey(org))
                {
                    entities.Add(org, 3);
                }
            }

            return entities;
        }
    }
}
