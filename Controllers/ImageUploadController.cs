using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace LABB2.Controllers
{
    public class ImageUploadController : Controller
    {
        
        private readonly ComputerVisionClient _cvClient;

        //Instans av ComputerVisionClient i denna kontroller som möjligör användandet av azures tjänst.
        public ImageUploadController(IConfiguration configuration)
        {
            // Autentisering med nycklar från azure.
            var endpoint = configuration["AzureCognitiveServices:Endpoint"];
            var key = configuration["AzureCognitiveServices:Key"];

            _cvClient = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
            {
                Endpoint = endpoint
            };
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult UploadImage()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> AnalyzeImage(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                ViewBag.Message = "You forgot to upload an image.";
                return View("UploadImage");
            }

            // Define paths for saving files
            var tempImagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "TempImages");
            var thumbnailsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Thumbnails");
            var tempImagePath = Path.Combine(tempImagesFolder, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_analyzed.jpg");
            var thumbnailPath = Path.Combine(thumbnailsFolder, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_thumbnail.jpg");

            

            // Konverterar bilden till en stream och analyserar
            using (var stream = imageFile.OpenReadStream())
            {
                // Specifierar det som ska hämtas av bilden.
                var features = new List<VisualFeatureTypes?>()
                {
                    VisualFeatureTypes.Description,
                    VisualFeatureTypes.Tags,
                    VisualFeatureTypes.Categories,
                    VisualFeatureTypes.Brands,
                    VisualFeatureTypes.Objects,
                    VisualFeatureTypes.Adult
                };
                var analysis = await _cvClient.AnalyzeImageInStreamAsync(stream, features);
                // ritar upp "anteckningar" på bilden. 
                using (var image = System.Drawing.Image.FromStream(imageFile.OpenReadStream()))
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    foreach (var obj in analysis.Objects)
                    {
                        var box = obj.Rectangle;
                        var label = obj.ObjectProperty;

                        var pen = new Pen(Color.Red, 3);
                        var font = new System.Drawing.Font("Arial", 16);
                        var brush = new SolidBrush(Color.Yellow);

                        // Draw rectangle
                        graphics.DrawRectangle(pen, box.X, box.Y, box.W, box.H);

                        // Draw label
                        graphics.DrawString(label, font, brush, box.X, box.Y - 20);
                    }

                    // Sparar den nya bilden i en temp mapp med nytt namn och lägger den i en viewbag.
                    var analyzedImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "TempImages", $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_analyzed.jpg");
                    image.Save(analyzedImagePath, ImageFormat.Jpeg);

                }
                // Generate the thumbnail
                using (var imageStream = imageFile.OpenReadStream())
                {
                    // Generate thumbnail
                    using (var thumbnailStream = await _cvClient.GenerateThumbnailInStreamAsync(100, 100, imageStream, true))
                    {
                        // Save the thumbnail
                        using (var fileStream = System.IO.File.Create(thumbnailPath))
                        {
                            await thumbnailStream.CopyToAsync(fileStream);
                        }
                    }
                }

                // Set paths in ViewBag
                ViewBag.AnnotatedImagePath = "/TempImages/" + Path.GetFileName(tempImagePath);
                ViewBag.ThumbnailPath = "/Thumbnails/" + Path.GetFileName(thumbnailPath);

                // Analysen av azures tjänst.
                ViewBag.ImageDescription = analysis.Description.Captions.FirstOrDefault()?.Text ?? "No description available.";
                // Tar emot taggar,kategorier,märken,objekt och om bilden innehåller vuxet innehåll.
                // Joinar sedan ihop dom med en ", ".
                ViewBag.ImageTags = analysis.Tags;
                ViewBag.ImageCategories = string.Join(", ", analysis.Categories.Select(category => category.Name));
                ViewBag.ImageBrands = string.Join(", ", analysis.Brands.Select(brand => brand.Name));
                ViewBag.ImageObjects = string.Join(", ", analysis.Objects.Select(obj => obj.ObjectProperty));
                ViewBag.AdultContent = analysis.Adult.IsAdultContent ? "Yes" : "No";
            }

            return View("UploadImage");
        }
    }
}
