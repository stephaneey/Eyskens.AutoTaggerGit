using DocumentFormat.OpenXml.Packaging;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Eyskens.AutoTaggerWeb.Helpers
{
    class FileHelper
    {
        private static string ParseWordDoc(Stream file)
        {
            const string wordmlNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            System.Text.StringBuilder textBuilder = new System.Text.StringBuilder();
            using (WordprocessingDocument wdDoc = WordprocessingDocument.Open(file, false))
            {

                NameTable nt = new NameTable();
                XmlNamespaceManager nsManager = new XmlNamespaceManager(nt);
                nsManager.AddNamespace("w", wordmlNamespace);
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
        private static string parseUsingPDFBox(string input)
        {
            PdfReader reader = null;
            try
            {
                reader = new PdfReader(input);

                StringWriter output = new StringWriter();

                for (int i = 1; i <= reader.NumberOfPages; i++)
                    output.WriteLine(PdfTextExtractor.GetTextFromPage(reader, i, new SimpleTextExtractionStrategy()));

                return output.ToString();
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }

        }
        public static string GetFileContent(ClientContext ctx, Guid ListId, int ItemId, out ListItem DocumentItem)
        {
            string localFilePath = null;
            string FileContent = null;
            try
            {
                List _library = ctx.Web.Lists.GetById(ListId);
                DocumentItem = _library.GetItemById(ItemId);
                ctx.Load(DocumentItem);
                ctx.Load(DocumentItem.ContentType);
                Microsoft.SharePoint.Client.File file = DocumentItem.File;
                ctx.Load(file);
                ClientResult<Stream> data = file.OpenBinaryStream();
                ctx.ExecuteQuery();

                if (DocumentItem.File.Length == 0)
                {
                    System.Threading.Thread.Sleep(20000);
                    DocumentItem = _library.GetItemById(ItemId);
                    ctx.Load(DocumentItem);
                    file = DocumentItem.File;
                    ctx.Load(file);
                    data = file.OpenBinaryStream();
                    ctx.ExecuteQuery();
                }

                if (data != null)
                {
                    if (file.Name.EndsWith(".pdf"))
                    {
                        int bufferSize = Convert.ToInt32(DocumentItem.File.Length);
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
                        LogHelper.Log(string.Format("File format of file {0} not supported", file.Name), LogSeverity.Error);
                        return null;
                    }
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(ex.Message + " " + ex.StackTrace, LogSeverity.Error);
                throw;
            }
            finally
            {
                if (localFilePath != null && System.IO.File.Exists(localFilePath))
                {
                    System.IO.File.Delete(localFilePath);
                }
            }

            return FileContent;
        }
    }
}