﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace IconMaker
{
    /// <summary>
    /// Represents a Windows icon file.
    /// </summary>
    public sealed class IconFile
    {
        #region Private Fields
        /// <summary>
        /// Stores the images in the icon.
        /// </summary>
        private readonly IconImageCollection images = new IconImageCollection();
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the IconFile class.
        /// </summary>
        public IconFile()
        {
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets the images contained in the icon.
        /// </summary>
        public IconImageCollection Images
        {
            get { return this.images; }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Saves the icon to a file.
        /// </summary>
        /// <param name="fileName">Name of file.</param>
        public void Save(string fileName)
        {
            if(fileName == null)
                throw new ArgumentNullException("fileName");

            using(var stream = File.OpenWrite(fileName))
            {
                this.Save(stream);
            }
        }

        /// <summary>
        /// Saves the icon to a stream.
        /// </summary>
        /// <param name="stream">Stream into which icon is saved.</param>
        public void Save(Stream stream)
        {
            if(stream == null)
                throw new ArgumentNullException("stream");

            BinaryWriter writer = new BinaryWriter(stream);

            var sortedImages = this.Images;
            var imageData = new Dictionary<int, byte[]>();

            int offset = (this.images.Count * 16) + 6;

            // Write the icon file header.
            writer.Write((ushort)0);    // must be 0
            writer.Write((ushort)1);    // 1 = ico file
            writer.Write((ushort)this.images.Count); // number of sizes
            
            foreach(var image in sortedImages)
            {
                var data = GetImageData(image);
                imageData.Add(image.PixelWidth, data);

                writer.Write((byte)image.Width);  // width
                writer.Write((byte)image.Height);  // height
                writer.Write((byte)0);  // colors, 0 = more than 256
                writer.Write((byte)0);  // must be 0
                writer.Write((ushort)1);    // color planes, should be 0 or 1
                writer.Write((ushort)32);   // bits per pixel
                writer.Write(data.Length);  // size of bitmap data in bytes
                writer.Write(offset);   // bitmap data offset in file

                offset += data.Length;
            }

            var sortedData = from i in imageData
                             orderby i.Key
                             select i.Value;

            foreach(var data in sortedData)
                writer.Write(data);
        }
        #endregion

        #region Private Static Methods
        /// <summary>
        /// Returns a byte array containing the serialized icon image.
        /// </summary>
        /// <param name="image">Icon image to serialize.</param>
        /// <returns>Serialized icon image.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands", Justification = "Assembly already requires full trust.")]
        private static byte[] GetImageData(BitmapSource image)
        {
            using(MemoryStream memoryStream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(memoryStream);

                if(image.Width < 256)
                {
                    int width = image.PixelWidth;
                    int pixelCount = width * width;
                    int maskWidth = width / 8;
                    if ((maskWidth % 4) != 0)
                        maskWidth += 3 - (maskWidth % 4);

                    writer.Write(40);   // size of BITMAPINFOHEADER
                    writer.Write(width);  // icon width/height
                    writer.Write(width * 2);  // icon height * 2 (AND plane)
                    writer.Write((short)1); // must be 1
                    writer.Write((short)32);    // bits per pixel
                    writer.Write(0);    // must be 0
                    writer.Write(pixelCount * 4 + maskWidth * width);   // size of bitmap data
                    writer.Write(new byte[4 * 4]);  // must be 0

                    uint[] pixelData = new uint[pixelCount];
                    image.CopyPixels(pixelData, width * 4, 0);

                    for(int y = width - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            uint srcPixel = pixelData[(y * width) + x];
                            if ((srcPixel >> 24) != 0)
                                writer.Write(srcPixel);
                            else
                                writer.Write((uint)0);
                        }
                    }

                    for (int y = width - 1; y >= 0; y--)
                    {
                        for (int x = 0; x < width / 8; x++)
                        {
                            byte maskValue = 0;

                            for (int bit = 0; bit < 8; bit++)
                            {
                                uint srcPixel = pixelData[(y * width) + (x * 8) + bit];
                                if ((srcPixel >> 24) < 128)
                                    maskValue |= (byte)(1 << (7 - bit));
                            }

                            writer.Write(maskValue);
                        }

                        for (int padding = 0; padding < ((width / 8) % 4); padding++)
                            writer.Write((byte)0);
                    }
                }
                else
                {
                    var pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(image));
                    pngEncoder.Save(memoryStream);
                }

                return memoryStream.ToArray();
            }
        }
        #endregion
    }
}