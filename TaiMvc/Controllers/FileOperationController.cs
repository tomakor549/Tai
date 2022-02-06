﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TaiMvc.Models;
using TaiMvc.SpecialOperation;
using System.Web;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Runtime.Remoting;
using System.IO;


namespace TaiMvc.Controllers
{
    [Authorize]
    public class FileOperationController : Controller
    {
        private const string _encryptionPassword = "Haslo";

        private readonly UserManager<ApplicationUser> _userManager;

        private Stopwatch stopWatch = new Stopwatch();

        private readonly ILogger<FileOperationController> _logger;

        private readonly IWebHostEnvironment webHostEnvironment;

        public int count=0;

        public int count2;
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string? actionName = filterContext.ActionDescriptor.DisplayName;
            if (actionName != null)
            {
                if (actionName.Contains("DownloadFile") || actionName.Contains("OperationUpload") || actionName.Contains("StreamDownloadFile2"))
                {
                    stopWatch.Reset();
                    stopWatch.Start();
                }
            }
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            string? actionName = filterContext.ActionDescriptor.DisplayName;

            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            string filemane = "Time operations.txt";
            var path = Path.Join(user.Localization, filemane);


            if (actionName != null)
            {
                if (actionName.Contains("DownloadFile") || actionName.Contains("OperationUpload") || actionName.Contains("StreamDownloadFile2") || actionName.Contains("FileUpload"))
                {

                    if (System.IO.File.Exists(path))
                    {
                        using StreamWriter file = new(path, append: true);

                        stopWatch.Stop();
                        var time = stopWatch.ElapsedMilliseconds;
                        _logger.LogInformation("Time: " + time.ToString() + "ms");
                        Debug.WriteLine("Time: " + time.ToString() + "ms");
                        //fsnotex.WriteLine("Time" + actionName + ": " + time.ToString() + "ms");
                        file.WriteLine("Time " + actionName.ToString() + " : " + time.ToString() + "ms");
                        //AddText(fsnotex, "Time" + actionName + ": " + time.ToString() + "ms");
                        file.Close();

                    }
                    else
                    {
                        using (FileStream fsex = System.IO.File.Create(path))
                        {
                            stopWatch.Stop();
                            var time = stopWatch.ElapsedMilliseconds;
                            _logger.LogInformation("Time: " + time.ToString() + "ms");
                            Debug.WriteLine("Time: " + time.ToString() + "ms");
                            AddText(fsex, "Time: " + time.ToString() + "ms");
                            fsex.Close();
                        }
                    }
                }
            }
        }
        private static void AddText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Write(info, 0, info.Length);
        }

        public FileOperationController(ILogger<FileOperationController> logger, UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public FileResult? DownloadFile(string fileName)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            var path = Path.Join(user.Localization, fileName);
            var len = new FileInfo(path).Length;
            if (len > 2000000000)
                return null;
            byte[] bytes;
            if (fileName.EndsWith(".aes"))
            {
                bytes = FileEncryptionOperations.FileDecrypt(path, _encryptionPassword);
                fileName = fileName.Remove(fileName.Length - 4);
            }
            else
                bytes = System.IO.File.ReadAllBytes(path);

            //Send the File to Download.
            return File(bytes, "application/octet-stream", fileName);
        }

        //normal short version stream download file
        public FileStreamResult StreamDownloadFile(string fileName)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            var path = Path.Join(user.Localization, fileName);

            Response.Headers.Add("content-disposition", "attachment; filename=" + fileName);
            //bufferSize 4096
            return File(new MyFileStream(path, FileMode.Open), "application/octet-stream");

            //other
            //return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096),
            //"application/octet-stream");

        }

        //abnormal long version stream download file - greater user control 
        public async Task StreamDownloadFile2(string fileName)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            var path = Path.Join(user.Localization, fileName);
            fileName = fileName.Remove(fileName.Length - 4);

            this.Response.StatusCode = 200;
            this.Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{fileName}\"");
            this.Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            var inputStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var outputStream = this.Response.Body;
            const int bufferSize = 1024;
            var buffer = new byte[bufferSize];
            while (true)
            {
                var bytesRead = await inputStream.ReadAsync(buffer, 0, bufferSize);
                if (bytesRead == 0) break;
                await outputStream.WriteAsync(buffer, 0, bytesRead);
            }
            await outputStream.FlushAsync();
        }

        //source: https://dogschasingsquirrels.com/2020/06/02/streaming-a-response-in-net-core-webapi/
        public async Task StreamEncodingDownloadFile(string fileName)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            var path = Path.Join(user.Localization, fileName);
            fileName = fileName.Remove(fileName.Length - 4);

            this.Response.StatusCode = 200;
            this.Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{fileName}\"");
            this.Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            var inputStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var outputStream = this.Response.Body;
            const int bufferSize = 1024;

            FileEncryptionOperations.FileDecrypt(ref inputStream, ref outputStream, _encryptionPassword, bufferSize);
            await outputStream.FlushAsync();
        }


        /*    ALL UPLOADS      */


        //upload traditional
        public IActionResult Operations() => View();

        public IActionResult OperationUpload(IFormFile file)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            
            string pathToCheck = Path.Join(user.Localization, file.FileName);

            if (file != null)
            {
                
                if (System.IO.File.Exists(pathToCheck))
                {
                    count+=1;
                    string fileNameOnly = Path.GetFileNameWithoutExtension(pathToCheck);
                    string extension = Path.GetExtension(pathToCheck);
                    string path = Path.GetDirectoryName(pathToCheck);
                    string newFullPath = pathToCheck;
                    
                    string tempFileName = string.Format("{0}({1})", fileNameOnly, count);
                        newFullPath = Path.Combine(path, tempFileName + extension);
                        using (var fileStream = new FileStream(newFullPath, FileMode.Create, FileAccess.Write))
                        {
                            file.CopyTo(fileStream);
                        }
                    
                }
               else
                {
                    using (var fileStream = new FileStream(pathToCheck, FileMode.Create, FileAccess.Write))
                    {
                        file.CopyTo(fileStream);
                    }
                }
            }
            else
            {
                ViewData["Message"] = "Wybierz jakiś plik do uploadu";
            }
            return RedirectToAction("Operations");
        }
        //Encryption Upload
        public IActionResult OperationUploadEncryption(IFormFile file)
        {
            var user = _userManager.GetUserAsync(HttpContext.User).Result;


            string pathToCheck = Path.Join(user.Localization, file.FileName);


            if (file != null)
            {
                if (System.IO.File.Exists(pathToCheck))
                {
                    count2+=1;
                    string fileNameOnly = Path.GetFileNameWithoutExtension(pathToCheck);
                    string extension = Path.GetExtension(pathToCheck);
                    string path = Path.GetDirectoryName(pathToCheck);
                    string newFullPath = pathToCheck;
                    
                    string tempFileName = string.Format("{0}({1})", fileNameOnly, count2);
                    newFullPath = Path.Combine(path, tempFileName + extension);
                    using (var fileStream = new FileStream(newFullPath, FileMode.Create, FileAccess.Write))
                    {
                        file.CopyToAsync(fileStream);
                    }
                }
                else
                {
                    using (var fileStream = new FileStream(pathToCheck, FileMode.Create, FileAccess.Write))
                    {
                        file.CopyTo(fileStream);
                    }
                }
            }
            else
            {
                ViewData["Message"] = "Wybierz jakiś plik do uploadu";
            }

            return RedirectToAction("Operations");
        }
        //stream upload



        [HttpPost]
        [DisableFormValueModelBinding]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
          
            var user = _userManager.GetUserAsync(HttpContext.User).Result;
            FormValueProvider formModel;
            //byte[] writeArray = new byte[4092];
            string targetFilePath = Path.Join(user.Localization, file.FileName);
            string tar = Path.Combine(user.Localization, file.FileName);
            const int bufferSize = 1024;
            //new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, bufferSize, false))
            try 
            {
                using (var stream = System.IO.File.Create(tar))
                {
                    formModel = await Request.StreamFile(stream);
                    //file.CopyToAsync(stream);
                    //formModel = await _context.HttpContext.Request.StreamFile(stream);
                }
                var viewModel = new ApplicationUser();

                var bindingSuccessful = await TryUpdateModelAsync(viewModel, prefix: "",
                   valueProvider: formModel);

                if (!bindingSuccessful)
                {
                    if (!ModelState.IsValid)
                    {
                        return BadRequest("Operations");
                    }
                }

                return Ok(viewModel);
            }
            catch (IOException)
            {
                ViewData["Message"] = "Wybierz jakiś plik do uploadu";
            }
            return RedirectToAction("Operations");

        }
    }
    
}
