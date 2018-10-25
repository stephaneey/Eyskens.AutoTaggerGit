# Security alert
GitHub reported the following potential security alert, since I do not maintain this repo, I let you upgrade the odata library yourself:
1 Microsoft.Data.OData vulnerability found in Eyskens.AutoTaggerWeb/packages.config
Remediation
Upgrade Microsoft.Data.OData to version 5.8.4 or later. For example:

<package id="Microsoft.Data.OData" version="5.8.4" />
Always verify the validity and compatibility of suggestions with your codebase.

Details
CVE-2018-8269 More information
moderate severity
Vulnerable versions: < 5.8.4
Patched version: 5.8.4
A denial of service vulnerability exists when OData Library improperly handles web requests, aka "OData Denial of Service Vulnerability." This affects Microsoft.Data.OData.
# Eyskens.AutoTaggerGit
An auto-tagging add-in for SharePoint Online

This add-on for SharePoint Online facilitates auto-tagging of Word and PDF documents. It leverages SharePoint taxonomies and
allows a very granular configuration to instruct the tagging on how to match taxonomy terms and document content in order to
avoid noisy associations.

On top of managed termsets, the Add-In is also capable of Enterprise Keywords recognition and creation. The creation process involves tokenization, lemmatization and named entity recognition. A noise words list can be created and thresholds can be set to maximize efficiency for both recognition and creation processes. 

Several videos demonstrating the Add-In are available on my Youtube Channel https://www.youtube.com/channel/UCJhxiAUkEaeg09H3mPjtgTw and a full documentation is available in PDF format on my blog http://www.silver-it.com/sites/default/files/EyskensAutoTagging.pdf
