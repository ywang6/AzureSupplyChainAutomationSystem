using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.IO;
using FloorPlanParser;

namespace REAutomation.Controllers
{
    public class UploadController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public ActionResult UploadFile()
        {
            return View();
        }
        [HttpPost]
        public ActionResult UploadFile(HttpPostedFileBase file)
        {
            string _path = "";
            string _FileName = "";
            string userEmail = "";

            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    var userId = User.Identity.Name.Split('\\')[1];
                    userEmail += userId + "@gmail.com";
                }

                if (file.ContentLength > 0)
                {
                    _FileName = Path.GetFileName(file.FileName);
                    var timestamp = DateTime.Now.ToFileTime().ToString();

                    _path = "C:\\REAutomation\\UploadedFiles\\" + userEmail + "|" + timestamp + "|" + _FileName;
                    file.SaveAs(_path);
                }
                //ViewBag.Message = "File Uploaded Successfully!! You will receive an email shortly";

                return View();
            }
            catch (Exception ex)
            {
                if (!String.IsNullOrEmpty(_path) && System.IO.File.Exists(_path))
                {
                    ViewBag.Message = "File uploaded successfully, but there is an error: ";
                }
                else
                {
                    ViewBag.Message = "File upload failed!! ";
                }

                ViewBag.Message += ex.Message;
                return View();
            }
        }
    }
}