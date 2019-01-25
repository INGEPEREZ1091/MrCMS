using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ImageMagick;
using MrCMS.Entities.Documents.Media;
using MrCMS.Settings;
using NHibernate;

namespace MrCMS.Services
{
    public class ImageProcessor : IImageProcessor
    {
        private readonly IFileSystem _fileSystem;
        private readonly ISession _session;

        public ImageProcessor(ISession session, IFileSystem fileSystem)
        {
            _session = session;
            _fileSystem = fileSystem;
        }

        public MediaFile GetImage(string imageUrl)
        {
            string originalImageUrl = GetOriginalImageUrl(imageUrl);
            MediaFile fileByLocation =
                _session.QueryOver<MediaFile>()
                    .Where(file => file.FileUrl == originalImageUrl)
                    .Cacheable()
                    .List().FirstOrDefault();

            if (fileByLocation != null)
                return fileByLocation;

            var crop = GetCrop(imageUrl);
            if (crop == null)
                return null;

            return crop.MediaFile;
        }

        public Crop GetCrop(string imageUrl)
        {
            string originalImageUrl = GetOriginalImageUrl(imageUrl);
            Crop crop =
                _session.QueryOver<Crop>()
                    .Where(file => file.Url == originalImageUrl)
                    .Cacheable()
                    .List().FirstOrDefault();

            return crop;
        }

        public void SaveResizedImage(MediaFile file, Size size, byte[] fileBytes, string fileUrl)
        {
            Size newSize = CalculateDimensions(file.Size, size);
            SaveFile(fileBytes, fileUrl, newSize, file.ContentType);
        }

        public void SaveCrop(MediaFile file, CropType cropType, Rectangle cropInfo, byte[] fileBytes, string fileUrl)
        {
            SaveFile(fileBytes, fileUrl, cropType.Size, file.ContentType, cropInfo);
        }

        public void EnforceMaxSize(ref Stream stream, MediaFile file, MediaSettings mediaSettings)
        {
            if (!mediaSettings.EnforceMaxImageSize)
            {
                return;
            }
            var imageInfo = new MagickImageInfo(stream);
            file.Width = imageInfo.Width;
            file.Height = imageInfo.Height;
            file.ContentLength = stream.Length;
            if (!RequiresResize(file.Size, mediaSettings.MaxSize))
                return;


            stream.Position = 0;
            var outputStream = new MemoryStream();
            using (var collection = new MagickImageCollection(stream))
            {
                collection.Coalesce();
                foreach (var image in collection)
                {
                    MagickGeometry geometry = new MagickGeometry
                    {
                        Width = mediaSettings.MaxImageSizeWidth,
                        Height = mediaSettings.MaxImageSizeHeight
                    };
                    image.Resize(geometry);
                    image.Strip();
                }
                collection.OptimizePlus();
                collection.Write(outputStream);
                outputStream.Position = 0;
            }

            Stream originalStream = stream;
            stream = outputStream;
            originalStream.Dispose();
        }


        private void SaveFile(byte[] fileBytes, string fileUrl, Size newSize, string contentType,
            Rectangle? cropRectangle = null)
        {
            using (var inputStream = new MemoryStream())
            using (var outputStream = new MemoryStream())
            {
                inputStream.Write(fileBytes, 0, fileBytes.Length);
                inputStream.Position = 0;
                using (var collection = new MagickImageCollection(inputStream))
                {
                    collection.Coalesce();
                    foreach (var image in collection)
                    {
                        MagickGeometry geometry = new MagickGeometry
                        {
                            Width = newSize.Width,
                            Height = newSize.Height
                        };
                        image.Resize(geometry);
                        if (cropRectangle.HasValue)
                        {
                            Rectangle rectangle = cropRectangle.Value;
                            image.Crop(
                                new MagickGeometry
                                {
                                    X = rectangle.X,
                                    Y = rectangle.Y,
                                    Width = rectangle.Width,
                                    Height = rectangle.Height
                                });
                        }
                        image.Strip();
                    }
                    collection.OptimizePlus();
                    collection.Write(outputStream);
                    outputStream.Position = 0;
                    _fileSystem.SaveFile(outputStream, fileUrl, contentType);
                }
            }
            //{
            //    inputStream.Write(fileBytes, 0, fileBytes.Length);
            //    inputStream.Position = 0;
            //    var instructions = new MagickGeometry()
            //    {
            //        Height = newSize.Height,
            //        Width = newSize.Width,
            //    };
            //    if (cropRectangle.HasValue)
            //    {
            //        Rectangle rectangle = cropRectangle.Value;
            //        instructions.CropRectangle = new double[] { rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom };
            //    }

            //    ImageBuilder.Current.Build(new ImageJob(inputStream, outputStream, instructions));
            //    _fileSystem.SaveFile(outputStream, fileUrl, contentType);
            //}
        }

        public void SaveResizedCrop(Crop crop, Size size, byte[] fileBytes, string fileUrl)
        {
            Size newSize = CalculateDimensions(crop.Size, size);
            SaveFile(fileBytes, fileUrl, newSize, crop.ContentType);
        }

        private string GetOriginalImageUrl(string imageUrl)
        {
            if (IsResized(imageUrl))
            {
                string resizePart = GetResizePart(imageUrl);
                int lastIndexOf = imageUrl.LastIndexOf(resizePart, StringComparison.OrdinalIgnoreCase);
                imageUrl = imageUrl.Remove(lastIndexOf - 1, resizePart.Length + 1);
            }
            return imageUrl;
        }

        private static bool IsResized(string imageUrl)
        {
            return GetRequestedSize(imageUrl) != null;
        }

        public static Size? GetRequestedSize(string imageUrl)
        {
            string resizePart = GetResizePart(imageUrl);
            if (resizePart == null) return null;

            int width = 0;
            int height = 0;
            string[] parts = resizePart.Split('_');
            var valid = parts.Count() == 2 && parts[0].StartsWith("w") && parts[1].StartsWith("h") &&
                   int.TryParse(parts[0].Substring(1), out width) && int.TryParse(parts[1].Substring(1), out height);
            if (!valid)
                return null;

            return new Size(width, height);
        }

        private static string GetResizePart(string imageUrl)
        {
            if (imageUrl.LastIndexOf("_w", StringComparison.Ordinal) == -1 || imageUrl.LastIndexOf('.') == -1)
                return null;

            int startIndex = imageUrl.LastIndexOf("_w", StringComparison.Ordinal) + 1;
            int length = imageUrl.LastIndexOf('.') - startIndex;
            if (length < 2) return null;
            string resizePart = imageUrl.Substring(startIndex, length);
            return resizePart;
        }


        public static Size CalculateDimensions(Size originalSize, Size targetSize)
        {
            // If the target image is bigger than the source
            if (!RequiresResize(originalSize, targetSize) || targetSize == Size.Empty)
            {
                return originalSize;
            }

            double ratio = 0;

            // What ratio should we resize it by
            double? widthRatio = targetSize.Width == 0 ? (double?)null : originalSize.Width / (double)targetSize.Width;
            double? heightRatio = targetSize.Height == 0
                ? (double?)null
                : originalSize.Height / (double)targetSize.Height;
            ratio = widthRatio.GetValueOrDefault() > heightRatio.GetValueOrDefault()
                ? originalSize.Width / (double)targetSize.Width
                : originalSize.Height / (double)targetSize.Height;

            double width = Math.Ceiling(originalSize.Width / ratio);
            width = targetSize.Width != 0 && width > targetSize.Width ? targetSize.Width : width;
            double resizeWidth = width;

            double height = Math.Ceiling(originalSize.Height / ratio);
            height = targetSize.Height != 0 && height > targetSize.Height ? targetSize.Height : height;
            double resizeHeight = height;

            return new Size((int)resizeWidth, (int)resizeHeight);
        }

        public static bool RequiresResize(Size originalSize, Size targetSize)
        {
            return (targetSize.Width != 0 && targetSize.Width < originalSize.Width) ||
                   (targetSize.Height != 0 && targetSize.Height < originalSize.Height);
        }

        /// <summary>
        ///     Returns the name and full path of the requested file
        /// </summary>
        public static string RequestedImageFileUrl(MediaFile file, Size size)
        {
            string fileLocation = file.FileUrl;

            if (file.Size == size || !RequiresResize(file.Size, size))
                return fileLocation;

            string temp = fileLocation.Replace(file.FileExtension, "");
            if (size.Width != 0)
                temp += "_w" + size.Width;
            if (size.Height != 0)
                temp += "_h" + size.Height;

            return temp + file.FileExtension;
        }

        /// <summary>
        ///     Returns the name and full path of the requested crop
        /// </summary>
        public static string RequestedResizedCropFileUrl(Crop crop, Size size)
        {
            string fileLocation = crop.Url;

            if (crop.Size == size || !RequiresResize(crop.Size, size))
                return fileLocation;

            string temp = fileLocation.Replace(crop.FileExtension, "");
            if (size.Width != 0)
                temp += "_w" + size.Width;
            if (size.Height != 0)
                temp += "_h" + size.Height;

            return temp + crop.FileExtension;
        }

        /// <summary>
        ///     Returns the name and full path of the requested crop
        /// </summary>
        public static string RequestedCropUrl(MediaFile file, CropType cropType)
        {
            string fileLocation = file.FileUrl;

            string temp = fileLocation.Replace(file.FileExtension, "");
            temp += "_c" + cropType.Id;

            return temp + file.FileExtension;
        }
    }
}