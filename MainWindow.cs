using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ABLoopPlayer
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }

    public class MainWindow : Window
    {
        private readonly MediaElement media;
        private readonly Slider seek;
        private readonly Canvas markerCanvas;
        private readonly Rectangle abRegion;
        private readonly Rectangle aMark;
        private readonly Rectangle bMark;
        private readonly TextBlock timeLabel;
        private readonly TextBlock aLabel;
        private readonly TextBlock bLabel;
        private readonly TextBlock loopCountLabel;
        private readonly TextBlock fileLabel;
        private readonly Button playBtn;
        private readonly Button loopBtn;
        private readonly ComboBox speedBox;
        private readonly DispatcherTimer timer;

        private double? pointA;
        private double? pointB;
        private bool loopOn;
        private bool seeking;
        private bool playing;
        private int loopCount;
        private bool isFullscreen;
        private WindowState prevState;
        private WindowStyle prevStyle;

        private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(0xF5, 0x82, 0x0B));
        private static readonly Brush PanelBg = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        private static readonly Brush BtnBg = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E));
        private static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));

        private Button MakeButton(string text)
        {
            var b = new Button
            {
                Content = text,
                Background = BtnBg,
                Foreground = Fg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 0),
                Focusable = false,
                FontSize = 13
            };
            return b;
        }

        private TextBlock MakeLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Fg,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0)
            };
        }

        public MainWindow()
        {
            Title = "ABループプレイヤー";
            Width = 920;
            Height = 640;
            MinWidth = 640;
            MinHeight = 420;
            Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
            AllowDrop = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            // header
            var header = new DockPanel { Background = PanelBg, LastChildFill = true };
            var openBtn = MakeButton("ファイルを開く");
            openBtn.Click += (s, e) => OpenDialog();
            DockPanel.SetDock(openBtn, Dock.Left);
            header.Children.Add(openBtn);
            var titleLbl = new TextBlock
            {
                Text = "ABループプレイヤー",
                Foreground = Orange,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 10, 0)
            };
            DockPanel.SetDock(titleLbl, Dock.Left);
            header.Children.Add(titleLbl);
            fileLabel = MakeLabel("動画ファイルをドラッグ＆ドロップでも開けます");
            fileLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
            header.Children.Add(fileLabel);
            header.Margin = new Thickness(0);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // video
            var videoBorder = new Border { Background = Brushes.Black };
            media = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                ScrubbingEnabled = true,
                Stretch = Stretch.Uniform,
                Volume = 0.8
            };
            media.MediaOpened += MediaOpened;
            videoBorder.Child = media;
            Grid.SetRow(videoBorder, 1);
            root.Children.Add(videoBorder);

            // AB marker canvas
            markerCanvas = new Canvas
            {
                Height = 12,
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                Margin = new Thickness(10, 6, 10, 0)
            };
            abRegion = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(0x73, 0xF5, 0x82, 0x0B)), Height = 12, Visibility = Visibility.Collapsed };
            aMark = new Rectangle { Fill = Brushes.White, Width = 3, Height = 12, Visibility = Visibility.Collapsed };
            bMark = new Rectangle { Fill = Brushes.White, Width = 3, Height = 12, Visibility = Visibility.Collapsed };
            markerCanvas.Children.Add(abRegion);
            markerCanvas.Children.Add(aMark);
            markerCanvas.Children.Add(bMark);
            markerCanvas.SizeChanged += (s, e) => DrawMarkers();
            Grid.SetRow(markerCanvas, 2);
            root.Children.Add(markerCanvas);

            // seek slider
            seek = new Slider
            {
                Minimum = 0,
                Maximum = 1000,
                Value = 0,
                Margin = new Thickness(10, 2, 10, 2),
                Focusable = false
            };
            seek.PreviewMouseLeftButtonDown += (s, e) => { seeking = true; };
            seek.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (DurationSec() > 0) media.Position = TimeSpan.FromSeconds(seek.Value / 1000.0 * DurationSec());
                seeking = false;
            };
            seek.ValueChanged += (s, e) =>
            {
                if (seeking && DurationSec() > 0)
                    media.Position = TimeSpan.FromSeconds(seek.Value / 1000.0 * DurationSec());
            };
            Grid.SetRow(seek, 3);
            root.Children.Add(seek);

            // control rows
            var rows = new StackPanel { Background = PanelBg };
            Grid.SetRow(rows, 4);
            root.Children.Add(rows);

            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 6, 10, 0) };
            playBtn = MakeButton("▶ 再生");
            playBtn.Click += (s, e) => TogglePlay();
            row1.Children.Add(playBtn);
            timeLabel = MakeLabel("0:00.0 / 0:00.0");
            row1.Children.Add(timeLabel);
            loopCountLabel = MakeLabel("");
            loopCountLabel.Foreground = Orange;
            row1.Children.Add(loopCountLabel);
            rows.Children.Add(row1);

            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 6, 10, 0) };
            var setABtn = MakeButton("Aを設定");
            setABtn.BorderBrush = Orange;
            setABtn.Click += (s, e) => SetA();
            row2.Children.Add(setABtn);
            var aMinus = MakeButton("A-0.1");
            aMinus.Click += (s, e) => Nudge(true, -0.1);
            row2.Children.Add(aMinus);
            var aPlus = MakeButton("A+0.1");
            aPlus.Click += (s, e) => Nudge(true, 0.1);
            row2.Children.Add(aPlus);
            aLabel = MakeLabel("A: --");
            aLabel.Foreground = Orange;
            row2.Children.Add(aLabel);
            var setBBtn = MakeButton("Bを設定");
            setBBtn.BorderBrush = Orange;
            setBBtn.Click += (s, e) => SetB();
            row2.Children.Add(setBBtn);
            var bMinus = MakeButton("B-0.1");
            bMinus.Click += (s, e) => Nudge(false, -0.1);
            row2.Children.Add(bMinus);
            var bPlus = MakeButton("B+0.1");
            bPlus.Click += (s, e) => Nudge(false, 0.1);
            row2.Children.Add(bPlus);
            bLabel = MakeLabel("B: --");
            bLabel.Foreground = Orange;
            row2.Children.Add(bLabel);
            loopBtn = MakeButton("ループ: OFF");
            loopBtn.Click += (s, e) =>
            {
                if (!pointA.HasValue || !pointB.HasValue)
                {
                    MessageBox.Show("先にA点とB点を設定してください", "ABループプレイヤー");
                    return;
                }
                SetLoop(!loopOn);
            };
            row2.Children.Add(loopBtn);
            var clearBtn = MakeButton("ABクリア");
            clearBtn.Click += (s, e) => ClearAB();
            row2.Children.Add(clearBtn);
            rows.Children.Add(row2);

            var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 6, 10, 4) };
            row3.Children.Add(MakeLabel("速度"));
            speedBox = new ComboBox
            {
                Width = 76,
                Focusable = false,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13
            };
            foreach (var v in new[] { "0.25", "0.5", "0.75", "1", "1.25", "1.5", "2" })
                speedBox.Items.Add(v + "x");
            speedBox.SelectedIndex = 3;
            speedBox.SelectionChanged += (s, e) =>
            {
                if (speedBox.SelectedItem == null) return;
                var txt = speedBox.SelectedItem.ToString().Replace("x", "");
                double r;
                if (double.TryParse(txt, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out r))
                    media.SpeedRatio = r;
            };
            row3.Children.Add(speedBox);
            var back5 = MakeButton("-5秒");
            back5.Click += (s, e) => SeekBy(-5);
            row3.Children.Add(back5);
            var fwd5 = MakeButton("+5秒");
            fwd5.Click += (s, e) => SeekBy(5);
            row3.Children.Add(fwd5);
            row3.Children.Add(MakeLabel("音量"));
            var vol = new Slider { Minimum = 0, Maximum = 100, Value = 80, Width = 90, Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            vol.ValueChanged += (s, e) => { media.Volume = vol.Value / 100.0; };
            row3.Children.Add(vol);
            var fsBtn = MakeButton("全画面");
            fsBtn.Click += (s, e) => ToggleFullscreen();
            row3.Children.Add(fsBtn);
            rows.Children.Add(row3);

            var help = new TextBlock
            {
                Text = "ショートカット: Space=再生/停止 / ←→=5秒移動 / Shift+←→=1コマ / A, B=A点・B点 / L=ループ / ↑↓=速度 / F=全画面 / Esc=全画面終了",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 11,
                Margin = new Thickness(10, 2, 10, 8)
            };
            rows.Children.Add(help);

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += TimerTick;
            timer.Start();

            DragOver += (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            Drop += (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) OpenFile(files[0]);
            };
            PreviewKeyDown += WindowKeyDown;
        }

        private double DurationSec()
        {
            return media.NaturalDuration.HasTimeSpan ? media.NaturalDuration.TimeSpan.TotalSeconds : 0;
        }

        private string Fmt(double? t)
        {
            if (!t.HasValue) return "--";
            var m = Math.Floor(t.Value / 60);
            var s = t.Value - m * 60;
            return m.ToString("0") + ":" + s.ToString("00.0");
        }

        private void MediaOpened(object sender, RoutedEventArgs e)
        {
            UpdateTimeLabel();
            DrawMarkers();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            var d = DurationSec();
            if (d <= 0 || media.Source == null) return;
            var pos = media.Position.TotalSeconds;
            if (loopOn && pointA.HasValue && pointB.HasValue && playing && pos >= pointB.Value)
            {
                media.Position = TimeSpan.FromSeconds(pointA.Value);
                loopCount++;
                loopCountLabel.Text = "ループ回数: " + loopCount;
                pos = pointA.Value;
            }
            if (!seeking) seek.Value = pos / d * 1000.0;
            UpdateTimeLabel();
            DrawMarkers();
        }

        private void UpdateTimeLabel()
        {
            timeLabel.Text = Fmt(media.Source == null ? (double?)null : media.Position.TotalSeconds)
                             + " / " + Fmt(media.Source == null ? (double?)null : DurationSec());
        }

        private void DrawMarkers()
        {
            var d = DurationSec();
            var w = markerCanvas.ActualWidth;
            if (d <= 0 || w <= 0 || media.Source == null)
            {
                abRegion.Visibility = Visibility.Collapsed;
                aMark.Visibility = Visibility.Collapsed;
                bMark.Visibility = Visibility.Collapsed;
                return;
            }
            if (pointA.HasValue)
            {
                aMark.Visibility = Visibility.Visible;
                Canvas.SetLeft(aMark, pointA.Value / d * w - 1);
            }
            else aMark.Visibility = Visibility.Collapsed;
            if (pointB.HasValue)
            {
                bMark.Visibility = Visibility.Visible;
                Canvas.SetLeft(bMark, pointB.Value / d * w - 1);
            }
            else bMark.Visibility = Visibility.Collapsed;
            if (pointA.HasValue && pointB.HasValue)
            {
                abRegion.Visibility = Visibility.Visible;
                Canvas.SetLeft(abRegion, pointA.Value / d * w);
                abRegion.Width = (pointB.Value - pointA.Value) / d * w;
            }
            else abRegion.Visibility = Visibility.Collapsed;
        }

        private void OpenDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "動画・音声ファイル|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mp3;*.wav;*.m4a|すべてのファイル|*.*"
            };
            if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
        }

        private void OpenFile(string path)
        {
            media.Source = new Uri(path);
            fileLabel.Text = System.IO.Path.GetFileName(path);
            ClearAB();
            media.Play();
            playing = true;
            playBtn.Content = "⏸ 一時停止";
        }

        private void TogglePlay()
        {
            if (media.Source == null) return;
            if (playing) { media.Pause(); playing = false; playBtn.Content = "▶ 再生"; }
            else { media.Play(); playing = true; playBtn.Content = "⏸ 一時停止"; }
        }

        private void SeekBy(double delta)
        {
            var d = DurationSec();
            if (d <= 0) return;
            var t = media.Position.TotalSeconds + delta;
            if (t < 0) t = 0;
            if (t > d) t = d;
            media.Position = TimeSpan.FromSeconds(t);
        }

        private void SetA()
        {
            if (media.Source == null) return;
            pointA = media.Position.TotalSeconds;
            if (pointB.HasValue && pointA.Value >= pointB.Value) pointB = null;
            if (pointA.HasValue && pointB.HasValue) SetLoop(true);
            UpdateABLabels();
        }

        private void SetB()
        {
            if (media.Source == null) return;
            pointB = media.Position.TotalSeconds;
            if (pointA.HasValue && pointB.Value <= pointA.Value) pointA = null;
            if (pointA.HasValue && pointB.HasValue) SetLoop(true);
            UpdateABLabels();
        }

        private void Nudge(bool isA, double delta)
        {
            var d = DurationSec();
            if (d <= 0) return;
            if (isA && pointA.HasValue)
            {
                var v = Math.Min(d, Math.Max(0, pointA.Value + delta));
                if (pointB.HasValue && v >= pointB.Value) v = pointB.Value - 0.1;
                pointA = v;
            }
            if (!isA && pointB.HasValue)
            {
                var v = Math.Min(d, Math.Max(0, pointB.Value + delta));
                if (pointA.HasValue && v <= pointA.Value) v = pointA.Value + 0.1;
                pointB = v;
            }
            UpdateABLabels();
        }

        private void UpdateABLabels()
        {
            aLabel.Text = "A: " + Fmt(pointA);
            bLabel.Text = "B: " + Fmt(pointB);
            DrawMarkers();
        }

        private void SetLoop(bool on)
        {
            loopOn = on;
            loopCount = 0;
            loopCountLabel.Text = on ? "ループ回数: 0" : "";
            loopBtn.Content = "ループ: " + (on ? "ON" : "OFF");
            loopBtn.Background = on ? Orange : BtnBg;
            loopBtn.Foreground = on ? Brushes.Black : Fg;
        }

        private void ClearAB()
        {
            pointA = null;
            pointB = null;
            SetLoop(false);
            UpdateABLabels();
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevState = WindowState;
                prevStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                isFullscreen = true;
            }
            else
            {
                WindowStyle = prevStyle;
                WindowState = prevState;
                isFullscreen = false;
            }
        }

        private static readonly double[] Speeds = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };

        private void ChangeSpeed(int dir)
        {
            var idx = Array.IndexOf(Speeds, media.SpeedRatio);
            if (idx < 0) idx = 3;
            idx = Math.Max(0, Math.Min(Speeds.Length - 1, idx + dir));
            media.SpeedRatio = Speeds[idx];
            speedBox.SelectedIndex = idx;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    TogglePlay();
                    e.Handled = true;
                    break;
                case Key.Left:
                    SeekBy(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? -1.0 / 30 : -5);
                    e.Handled = true;
                    break;
                case Key.Right:
                    SeekBy(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? 1.0 / 30 : 5);
                    e.Handled = true;
                    break;
                case Key.Up:
                    ChangeSpeed(1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    ChangeSpeed(-1);
                    e.Handled = true;
                    break;
                case Key.A:
                    SetA();
                    e.Handled = true;
                    break;
                case Key.B:
                    SetB();
                    e.Handled = true;
                    break;
                case Key.L:
                    loopBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                    break;
                case Key.F:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (isFullscreen) ToggleFullscreen();
                    e.Handled = true;
                    break;
            }
        }
    }
}
