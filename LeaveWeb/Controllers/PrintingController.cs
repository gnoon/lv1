using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Web.Configuration;
using System.Configuration;
using Leave.Common;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections;
using System.Text;

namespace Leave.Controllers
{
    public class PrintingController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet, FileDownload]
        public ActionResult RequestRecord(Int64? ID)
        {
            //RequestRecord head = LeaveCore.Leave.GetHeader(User, ID ?? -1);
            string template = Server.MapPath("~/Resources/docx/Template.docx");//@"D:\Nop\Verasu\Leave\Code\MailMerge\Template.docx";

            // temp file path
            string temp = Leave.Properties.Settings.Default.AttachFilePath;
            string tpath = Path.Combine(temp, Guid.NewGuid().ToString());
            string tzip = template;//Path.Combine(temp, Guid.NewGuid().ToString());

            //System.IO.File.Copy(template, tzip);

            try
            {
                //ZipEntry entry;
                //using (ZipFile zip = new ZipFile(tzip))
                //{
                //    entry = zip.GetEntry("word/document.xml");
                //    if (entry != null)
                //    {
                //        string xml = null, xmlOut;
                //        using (StreamReader stream = new StreamReader(zip.GetInputStream(entry)))
                //        {
                //            xml = stream.ReadToEnd();
                //        }
                //        if (xml != null)
                //        {
                //            xmlOut = xml
                //                .Replace("<<วันที่>>", "22/11/2013")
                //                .Replace("<<หัวหน้างาน>>", "คุณสยามแดน แสนภักดี")
                //                ;

                //            System.IO.File.WriteAllText(tpath, xmlOut, Encoding.Default);

                //            zip.BeginUpdate();
                //            zip.Add(tpath, entry.Name);
                //            zip.CommitUpdate();
                //        }
                //    }
                //}

                string name = "leave_" + ID + ".docx";

                var encoding = System.Text.UnicodeEncoding.UTF8;
                Response.Charset = encoding.WebName;
                Response.HeaderEncoding = encoding;

                Response.AddHeader("Content-Disposition", string.Format("attachment; filename=\"{0}\"", (Request.Browser.Browser == "IE") ? HttpUtility.UrlEncode(name, encoding) : name));

                // send file for save dialog
                FilePathResult file = File(tzip, System.Net.Mime.MediaTypeNames.Application.Octet, name);
                //Response.Flush();

                return file;
            }
            finally
            {
                if (System.IO.File.Exists(tpath))
                    System.IO.File.Delete(tpath);
                //if (System.IO.File.Exists(tzip))
                //    System.IO.File.Delete(tzip);
            }
        }
    }
}
