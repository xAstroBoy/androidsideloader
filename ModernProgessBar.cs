using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AndroidSideloader
{
    // A modern progress bar with rounded corners, left-to-right gradient fill,
    // animated indeterminate mode, and optional status text overlay
    [Description("Modern Themed Progress Bar")]
    public class ModernProgressBar : Control
    {
        #region Fields

        private int _value;
        private int _minimum;
        private int _maximum = 100;
        private int _radius = 8;
        private bool _isIndeterminate;
        private string _statusText = string.Empty;
        private string _operationType = string.Empty;

        // Indeterminate animation
        private readonly Timer _animationTimer;
        private float _animationOffset;
        private const float AnimationSpeed = 4f;
        private const int IndeterminateBlockWidth = 80;

        // Colors
        private Color _backgroundColor = Color.FromArgb(28, 32, 38);
        private Color _progressStartColor = Color.FromArgb(120, 220, 190); // lighter accent
        private Color _progressEndColor = Color.FromArgb(50, 160, 130);    // darker accent
        private Color _indeterminateColor = Color.FromArgb(93, 203, 173);  // accent
        private Color _textColor = Color.FromArgb(230, 230, 230);
        private Color _textShadowColor = Color.FromArgb(90, 0, 0, 0);

        #endregion

        #region Constructor

        public ModernProgressBar()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor,
                true);

            BackColor = Color.Transparent;

            // Size + Font
            Height = 28;
            Width = 220;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            _animationTimer = new Timer { Interval = 16 }; // ~60fps
            _animationTimer.Tick += AnimationTimer_Tick;
        }

        #endregion

        #region Properties

        [Category("Progress")]
        [Description("The current value of the progress bar.")]
        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(_minimum, Math.Min(_maximum, value));
                Invalidate();
            }
        }

        [Category("Progress")]
        [Description("The minimum value of the progress bar.")]
        public int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_value < _minimum) _value = _minimum;
                Invalidate();
            }
        }

        [Category("Progress")]
        [Description("The maximum value of the progress bar.")]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("The corner radius of the progress bar.")]
        public int Radius
        {
            get => _radius;
            set
            {
                _radius = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("Progress")]
        [Description("Whether the progress bar shows indeterminate (marquee) progress.")]
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                // If there is no change, do nothing
                if (_isIndeterminate == value) 
                    return;

                _isIndeterminate = value;
                if (_isIndeterminate)
                {
                    _animationOffset = -IndeterminateBlockWidth;
                    _animationTimer.Start();
                }
                else
                {
                    _animationTimer.Stop();
                }
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Optional status text to display on the progress bar.")]
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value ?? string.Empty;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Operation type label (e.g., 'Downloading', 'Installing').")]
        public string OperationType
        {
            get => _operationType;
            set
            {
                _operationType = value ?? string.Empty;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Background color of the progress bar track.")]
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Start color of the progress gradient (left side).")]
        public Color ProgressStartColor
        {
            get => _progressStartColor;
            set { _progressStartColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("End color of the progress gradient (right side).")]
        public Color ProgressEndColor
        {
            get => _progressEndColor;
            set { _progressEndColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Color used for indeterminate animation.")]
        public Color IndeterminateColor
        {
            get => _indeterminateColor;
            set { _indeterminateColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Text color for status overlay.")]
        public Color TextColor
        {
            get => _textColor;
            set { _textColor = value; Invalidate(); }
        }

        // Gets the progress as a percentage (0-100)
        public float ProgressPercent =>
            _maximum > _minimum ? (float)(_value - _minimum) / (_maximum - _minimum) * 100f : 0f;

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(BackColor);

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            var outerRect = new Rectangle(0, 0, w - 1, h - 1);

            // Draw background track
            using (var path = CreateRoundedPath(outerRect, _radius))
            using (var bgBrush = new SolidBrush(_backgroundColor))
            {
                g.FillPath(bgBrush, path);
            }

            // Draw progress or indeterminate animation
            if (_isIndeterminate)
            {
                DrawIndeterminate(g, outerRect);
            }
            else if (_value > _minimum)
            {
                DrawProgress(g, outerRect);
            }

            // Draw text overlay
            DrawTextOverlay(g, outerRect);

            base.OnPaint(e);
        }

        private void DrawProgress(Graphics g, Rectangle outerRect)
        {
            float percent = (_maximum > _minimum)
                ? (float)(_value - _minimum) / (_maximum - _minimum)
                : 0f;

            if (percent <= 0f) return;
            if (percent > 1f) percent = 1f;

            int progressWidth = (int)Math.Round(outerRect.Width * percent);
            if (progressWidth <= 0) return;
            if (progressWidth > outerRect.Width) progressWidth = outerRect.Width;

            using (var outerPath = CreateRoundedPath(outerRect, _radius))
            {
                // Clip to progress area inside rounded track
                Rectangle progressRect = new Rectangle(outerRect.X, outerRect.Y, progressWidth, outerRect.Height);
                using (var progressClip = new Region(progressRect))
                using (var trackRegion = new Region(outerPath))
                {
                    trackRegion.Intersect(progressClip);

                    Region prevClip = g.Clip;
                    try
                    {
                        g.SetClip(trackRegion, CombineMode.Replace);

                        // Left-to-right gradient, based on accent color
                        using (var gradientBrush = new LinearGradientBrush(
                                   progressRect,
                                   _progressStartColor,
                                   _progressEndColor,
                                   LinearGradientMode.Horizontal))
                        {
                            g.FillPath(gradientBrush, outerPath);
                        }
                    }
                    finally
                    {
                        g.Clip = prevClip;
                    }
                }
            }
        }

        private void DrawIndeterminate(Graphics g, Rectangle outerRect)
        {
            using (var outerPath = CreateRoundedPath(outerRect, _radius))
            {
                Region prevClip = g.Clip;
                try
                {
                    g.SetClip(outerPath, CombineMode.Replace);

                    int blockWidth = Math.Min(IndeterminateBlockWidth, outerRect.Width);
                    int blockX = (int)_animationOffset;
                    var blockRect = new Rectangle(blockX, outerRect.Y, blockWidth, outerRect.Height);

                    // Solid bar with slight left-to-right gradient
                    using (var brush = new LinearGradientBrush(
                               blockRect,
                               ControlPaint.Light(_indeterminateColor, 0.1f),
                               ControlPaint.Dark(_indeterminateColor, 0.1f),
                               LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(brush, blockRect);
                    }
                }
                finally
                {
                    g.Clip = prevClip;
                }
            }
        }

        private void DrawTextOverlay(Graphics g, Rectangle outerRect)
        {
            string displayText = BuildDisplayText();
            if (string.IsNullOrEmpty(displayText)) return;

            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                // Slight shadow for legibility on accent background
                var shadowRect = new Rectangle(outerRect.X + 1, outerRect.Y + 1, outerRect.Width, outerRect.Height);
                using (var shadowBrush = new SolidBrush(_textShadowColor))
                {
                    g.DrawString(displayText, Font, shadowBrush, shadowRect, sf);
                }

                using (var textBrush = new SolidBrush(_textColor))
                {
                    g.DrawString(displayText, Font, textBrush, outerRect, sf);
                }
            }
        }

        private string BuildDisplayText()
        {
            if (!string.IsNullOrEmpty(_statusText))
            {
                return _statusText;
            }

            if (_isIndeterminate && !string.IsNullOrEmpty(_operationType))
            {
                // E.g. "Downloading..."
                return _operationType + "...";
            }

            if (!_isIndeterminate && _value > _minimum)
            {
                string percentText = $"{(int)ProgressPercent}%";
                if (!string.IsNullOrEmpty(_operationType))
                {
                    // E.g. "Downloading · 73%"
                    return $"{_operationType} · {percentText}";
                }
                return percentText;
            }

            return string.Empty;
        }

        private GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            diameter = Math.Min(diameter, Math.Min(rect.Width, rect.Height));
            radius = diameter / 2;

            var arcRect = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arcRect, 180, 90);
            arcRect.X = rect.Right - diameter;
            path.AddArc(arcRect, 270, 90);
            arcRect.Y = rect.Bottom - diameter;
            path.AddArc(arcRect, 0, 90);
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);

            path.CloseFigure();
            return path;
        }

        #endregion

        #region Animation

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            _animationOffset += AnimationSpeed;

            if (_animationOffset > ClientSize.Width + IndeterminateBlockWidth)
            {
                _animationOffset = -IndeterminateBlockWidth;
            }

            Invalidate();
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
