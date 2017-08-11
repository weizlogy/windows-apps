using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static ColorCheckCracker.MainWindow;

namespace ColorCheckCracker {
    /// <summary>
    /// color check crack interface.
    /// </summary>
    interface Cracker : IDisposable {
        /// <summary>
        /// capture the target.
        /// </summary>
        /// <param name="pLocation">capture left top axis.</param>
        /// <param name="pSize">capture width and height.</param>
        /// <returns>self</returns>
        Cracker Capture(Point pLocation, Size pSize);

        /// <summary>
        /// analyze and covert capture image.
        /// </summary>
        void Analyze();

        /// <summary>
        /// get imagesource for image control.
        /// </summary>
        /// <returns></returns>
        BitmapSource GetImageSource();
    }

    /// <summary>
    /// <para>color check crack base implementation.</para>
    /// </summary>
    abstract class AbstractCracker : Cracker {
        /// <summary>
        /// capture image pixcel size.
        /// </summary>
        protected const int PIXCEL_SIZE = 4;

        /// <summary>
        /// capture image.
        /// </summary>
        protected System.Drawing.Bitmap CaptureImage { get; private set; }

        /// <summary>
        /// convert capture image for not max appear colorcode.
        /// </summary>
        /// <param name="pixcel">target pixcel</param>
        /// <param name="index">target index</param>
        /// <param name="maxApColorCode">max appear colorcode</param>
        protected abstract void OverlayRemainColors(byte[] pixcel, System.Drawing.Color maxApColorCode);

        /// <summary>
        /// convert capture image for max appear colorcode.
        /// </summary>
        /// <param name="pixcel">target pixcel</param>
        /// <param name="index">target index</param>
        /// <param name="maxApColorCode">max appear colorcode</param>
        protected abstract void OverlayMaxApColor(byte[] pixcel, System.Drawing.Color maxApColorCode);

        /// <summary>
        /// static factory method.
        /// </summary>
        /// <param name="cmode">color check mode</param>
        /// <returns>cracker interface</returns>
        public static Cracker Generate(CrackMode cmode) {
            Cracker cracker = null;
            switch (cmode) {
                case CrackMode.Different:
                    cracker = new DifferentColorCracker();
                    break;
                case CrackMode.Same:
                    cracker = new SameColorCracker();
                    break;
            }
            return cracker;
        }

        /// <summary>
        /// <see cref="Cracker.Analyze()"/>
        /// </summary>
        public void Analyze() {
            var bmp = this.CaptureImage;
            if (bmp == null) {
                return;
            }
            // prepare.
            var bmpd = bmp.LockBits(
                new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var buf = new byte[bmp.Width * bmp.Height * 4];
            Marshal.Copy(bmpd.Scan0, buf, 0, buf.Length);
            // find max appear color.
            var colors = new List<System.Drawing.Color>();
            for (int i = 0; i < buf.Length; i += PIXCEL_SIZE) {
                colors.Add(System.Drawing.Color.FromArgb(buf[i + 3], buf[i + 2], buf[i + 1], buf[i]));
            }
            var maxApColorInfo =
                colors.GroupBy(c => c)
                    .Select(c => new { ColorCode = c.Key, Count = c.Count() })
                    .OrderByDescending(c => c.Count).First();
            Console.WriteLine("maxColorAndCount = {0}", maxApColorInfo);
            // convert.
            for (int i = 0; i < buf.Length; i += PIXCEL_SIZE) {
                var pixcel = new byte[PIXCEL_SIZE];
                Array.Copy(buf, i, pixcel, 0, pixcel.Length);
                if (colors[i / 4] != maxApColorInfo.ColorCode) {
                    OverlayRemainColors(pixcel, maxApColorInfo.ColorCode);
                    Array.Copy(pixcel, 0, buf, i, pixcel.Length);
                    continue;
                }
                OverlayMaxApColor(pixcel, maxApColorInfo.ColorCode);
                Array.Copy(pixcel, 0, buf, i, pixcel.Length);
            }
            // end.
            Marshal.Copy(buf, 0, bmpd.Scan0, buf.Length);
            bmp.UnlockBits(bmpd);
            //bmp.Save(@"E:\test.png", ImageFormat.Png);
        }

        /// <summary>
        /// <see cref="Cracker.Capture()"/>
        /// </summary>
        public Cracker Capture(Point pLocation, Size pSize) {
            var bmp = new System.Drawing.Bitmap((int)pSize.Width, (int)pSize.Height, PixelFormat.Format32bppArgb);
            using (var grph = System.Drawing.Graphics.FromImage(bmp)) {
                grph.CopyFromScreen((int)pLocation.X, (int)pLocation.Y, 0, 0, bmp.Size);
            }
            this.CaptureImage = bmp;
            return this;
        }

        /// <summary>
        /// dispose capture image.
        /// </summary>
        public void Dispose() {
            if (this.CaptureImage == null) {
                return;
            }
            this.CaptureImage.Dispose();
        }

        /// <summary>
        /// <see cref="Cracker.GetImageSource()"/>
        /// </summary>
        public BitmapSource GetImageSource() {
            var bmp = this.CaptureImage;
            return Imaging.CreateBitmapSourceFromHBitmap(
                bmp.GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
    }

    /// <summary>
    /// <para>different color check crack implementation.</para>
    /// <para>
    /// convert to reverse color for max appear color. 
    /// convert to clear color for otherwise.
    /// </para>
    /// </summary>
    class DifferentColorCracker : AbstractCracker {
        protected override void OverlayRemainColors(byte[] pixcel, System.Drawing.Color maxApColorCode) {
            // clear alpha channel.
            pixcel[3] = 0x00;
        }
        protected override void OverlayMaxApColor(byte[] pixcel, System.Drawing.Color maxApColorCode) {
            // reverse color.
            var index = 0;
            pixcel[index++] = (byte)(0xFF - maxApColorCode.B);
            pixcel[index++] = (byte)(0xFF - maxApColorCode.G);
            pixcel[index++] = (byte)(0xFF - maxApColorCode.R);
        }
    }

    /// <summary>
    /// <para>same color check crack implementation.</para>
    /// <para>
    /// convert to clear color for max appear color. 
    /// convert to reverse color for otherwise.
    /// </para>
    /// </summary>
    class SameColorCracker : AbstractCracker {
        protected override void OverlayRemainColors(byte[] pixcel, System.Drawing.Color maxApColorCode) {
            // reverse color.
            var index = 0;
            pixcel[index++] = (byte)(0xFF - maxApColorCode.B);
            pixcel[index++] = (byte)(0xFF - maxApColorCode.G);
            pixcel[index++] = (byte)(0xFF - maxApColorCode.R);
        }
        protected override void OverlayMaxApColor(byte[] pixcel, System.Drawing.Color maxApColorCode) {
            // clear alpha channel.
            pixcel[3] = 0x00;
        }
    }
}
