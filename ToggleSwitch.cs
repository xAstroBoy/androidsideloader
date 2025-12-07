using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AndroidSideloader
{
    /// <summary>
    /// An iOS-style toggle switch control with smooth animation.
    /// </summary>
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _isHovered;
        private float _animationProgress; // 0 = off, 1 = on
        private Timer _animationTimer;
        private const int AnimationDuration = 80; // ms
        private const int AnimationInterval = 8; // ~120fps
        private float _animationStep;

        // Colors
        private Color _onColor = Color.FromArgb(93, 203, 173);
        private Color _offColor = Color.FromArgb(60, 65, 75);
        private Color _thumbColor = Color.White;
        private Color _onHoverColor = Color.FromArgb(110, 215, 190);
        private Color _offHoverColor = Color.FromArgb(75, 80, 90);

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.StandardClick |
                     ControlStyles.StandardDoubleClick, true);

            // Disable double-click so rapid clicks are treated as separate clicks
            SetStyle(ControlStyles.StandardDoubleClick, false);

            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            _animationTimer = new Timer { Interval = AnimationInterval };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationStep = (float)AnimationInterval / AnimationDuration;
        }

        [Category("Appearance")]
        [Description("Gets or sets whether the toggle is in the 'on' state.")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    StartAnimation();
                    OnCheckedChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Sets the checked state without triggering animation or events.
        /// Using this for initial state setup.
        /// </summary>
        public void SetCheckedSilent(bool value)
        {
            _checked = value;
            _animationProgress = value ? 1f : 0f;
            _animationTimer.Stop();
            Invalidate();
        }

        [Category("Appearance")]
        [Description("The color of the toggle when it is on.")]
        public Color OnColor
        {
            get => _onColor;
            set { _onColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("The color of the toggle when it is off.")]
        public Color OffColor
        {
            get => _offColor;
            set { _offColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("The color of the thumb (circle).")]
        public Color ThumbColor
        {
            get => _thumbColor;
            set { _thumbColor = value; Invalidate(); }
        }

        protected virtual void OnCheckedChanged(EventArgs e)
        {
            CheckedChanged?.Invoke(this, e);
        }

        private void StartAnimation()
        {
            if (!_animationTimer.Enabled)
            {
                _animationTimer.Start();
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            float target = _checked ? 1f : 0f;

            if (_animationProgress < target)
            {
                _animationProgress += _animationStep;
                if (_animationProgress >= target)
                {
                    _animationProgress = target;
                    _animationTimer.Stop();
                }
            }
            else if (_animationProgress > target)
            {
                _animationProgress -= _animationStep;
                if (_animationProgress <= target)
                {
                    _animationProgress = target;
                    _animationTimer.Stop();
                }
            }
            else
            {
                _animationTimer.Stop();
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int width = Width;
            int height = Height;
            int padding = 2;
            int thumbDiameter = height - (padding * 2);
            int trackRadius = height / 2;

            Color trackColor;
            if (_isHovered)
            {
                trackColor = InterpolateColor(_offHoverColor, _onHoverColor, _animationProgress);
            }
            else
            {
                trackColor = InterpolateColor(_offColor, _onColor, _animationProgress);
            }

            Rectangle trackRect = new Rectangle(0, 0, width, height);
            using (GraphicsPath trackPath = CreateRoundedRectPath(trackRect, trackRadius))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            int thumbMinX = padding;
            int thumbMaxX = width - thumbDiameter - padding;
            float easedProgress = EaseOutQuad(_animationProgress);
            int thumbX = (int)(thumbMinX + (thumbMaxX - thumbMinX) * easedProgress);
            int thumbY = padding;

            Rectangle shadowRect = new Rectangle(thumbX + 1, thumbY + 1, thumbDiameter, thumbDiameter);
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, shadowRect);
            }

            Rectangle thumbRect = new Rectangle(thumbX, thumbY, thumbDiameter, thumbDiameter);
            using (SolidBrush thumbBrush = new SolidBrush(_thumbColor))
            {
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }

        private float EaseOutQuad(float t)
        {
            return t * (2 - t);
        }

        private Color InterpolateColor(Color from, Color to, float progress)
        {
            int r = (int)(from.R + (to.R - from.R) * progress);
            int g = (int)(from.G + (to.G - from.G) * progress);
            int b = (int)(from.B + (to.B - from.B) * progress);
            int a = (int)(from.A + (to.A - from.A) * progress);
            return Color.FromArgb(a, r, g, b);
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                // Toggle immediately on mouse down for responsive feel
                _checked = !_checked;
                StartAnimation();
                OnCheckedChanged(EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}