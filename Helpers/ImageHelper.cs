using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipboardPro.Helpers
{
    public static class ImageHelper
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource CreateNativeBitmapSource(string imagePath)
        {
            using (var img = System.Drawing.Image.FromFile(imagePath))
            {
                using (var bmp = new System.Drawing.Bitmap(img))
                {
                    var handle = bmp.GetHbitmap();
                    try
                    {
                        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            handle, IntPtr.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        source.Freeze();
                        return source;
                    }
                    finally
                    {
                        DeleteObject(handle);
                    }
                }
            }
        }
    }
}
