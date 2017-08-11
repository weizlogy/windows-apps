using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorCheckCracker {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        /// <summary>
        /// <para>color check crack mode.</para>
        /// </summary>
        public enum CrackMode {
            /// <summary>
            /// choose one that it has same target color.
            /// </summary>
            Same,
            /// <summary>
            /// choose one that it has different target color.
            /// </summary>
            Different
        }

        /// <summary>
        /// true, if it's capturing display. false, otherwise.
        /// </summary>
        private bool capturing = false;
        /// <summary>
        /// current color check crack mode.
        /// </summary>
        private CrackMode cmode = CrackMode.Different;

        /// <summary>
        /// initialize UI and register event handers.
        /// </summary>
        public MainWindow() {
            // auto generate code.
            InitializeComponent();
            // mouse left to drag window.
            this.MouseLeftButtonDown += (sender, e) => { this.DragMove(); };
            // deactivate to activate.
            // ignore activate when capturing or closing.
            this.Deactivated += (sender, e) => {
                if (this.capturing || this.Tag != null) {
                    return;
                }
                // if no delay, often do not activated.
                Task.Delay(200).ContinueWith(_ => {
                    this.Dispatcher.Invoke(() => {
                        this.Activate();
                    });
                });
            };
            // closing to ignore deactivate event.
            this.Closing += (sender, e) => {
                this.Tag = new object();
            };
            // key control.
            // escape to clear image or close window.
            // enter to start crack.
            // S to Same mode.
            // D to Different mode.
            // T to Terminate temporary transparent window.
            this.KeyUp += (sender, e) => {
                switch (e.Key) {
                    case Key.Escape:
                        if (this.pic.Source != null) {
                            ClearImage();
                            break;
                        }
                        Application.Current.Shutdown();
                        break;
                    case Key.Enter:
                        DoCrack();
                        break;
                    case Key.S:
                        cmode = CrackMode.Same;
                        this.BorderBrush = Brushes.Red;
                        ClearImage();
                        break;
                    case Key.D:
                        cmode = CrackMode.Different;
                        this.BorderBrush = Brushes.Black;
                        ClearImage();
                        break;
                    case Key.T:
                        this.Background.Opacity = this.Background.Opacity == 0 ? 1 : 0;
                        break;
                    default:
                        break;
                }
            };
            // key control.
            // enter to clear image.
            // T to Temporary transparent window.
            this.KeyDown += (sender, e) => {
                switch (e.Key) {
                    case Key.Enter:
                        ClearImage();
                        break;
                    default:
                        break;
                }
            };
        }

        /// <summary>
        /// clear capture image.
        /// </summary>
        public void ClearImage() {
            this.pic.Source = null;
            this.Background.Opacity = 1;
        }

        /// <summary>
        /// <para>execute crack.</para>
        /// </summary>
        public void DoCrack() {
            this.capturing = true;
            // keep location and size.
            var screenLocation = this.PointToScreen(new Point());
            var windowSize = this.RenderSize;
            Console.WriteLine("capture axis x={0}, y={1} size w={2}, h={3}",
                screenLocation.X, screenLocation.Y, windowSize.Width, windowSize.Height);
            // crack and set result to window.
            using (Cracker cracker = AbstractCracker.Generate(this.cmode)) {
                cracker.Capture(screenLocation, windowSize).Analyze();
                this.pic.Source = cracker.GetImageSource();
            }
            Console.WriteLine("Capture end.");
            this.capturing = false;
            // transparent windos for click under the window.
            this.Background.Opacity = 0;
        }
    }
}
