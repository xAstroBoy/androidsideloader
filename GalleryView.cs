using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// High-performance virtual gallery panel with smooth scrolling and hover animations.
// Optimized for thousands of items with LRU image caching.

namespace AndroidSideloader
{
    public class FastGalleryPanel : Control
    {
        // Data
        private List<ListViewItem> _items;
        private readonly int _tileWidth;
        private readonly int _tileHeight;
        private readonly int _spacing;

        // Layout
        private int _columns;
        private int _rows;
        private int _contentHeight;
        private int _leftPadding;

        // Smooth scrolling
        private float _scrollY;
        private float _targetScrollY;
        private bool _isScrolling;
        private readonly VScrollBar _scrollBar;

        // Animation
        private readonly System.Windows.Forms.Timer _animationTimer;
        private readonly Dictionary<int, TileAnimationState> _tileStates;

        // Image cache (LRU)
        private readonly Dictionary<string, Image> _imageCache;
        private readonly Queue<string> _cacheOrder;
        private const int MAX_CACHE_SIZE = 200;

        // Interaction
        private int _hoveredIndex = -1;
        private int _selectedIndex = -1;

        // Rendering
        private Bitmap _backBuffer;

        // Visual constants
        private const int CORNER_RADIUS = 10;
        private const int THUMB_CORNER_RADIUS = 6;
        private const float HOVER_SCALE = 1.07f;
        private const float ANIMATION_SPEED = 0.25f;
        private const float SCROLL_SMOOTHING = 0.3f;

        // Theme colors
        private static readonly Color TileBorderHover = Color.FromArgb(93, 203, 173);
        private static readonly Color TileBorderSelected = Color.FromArgb(200, 200, 200);
        private static readonly Color TextColor = Color.FromArgb(245, 255, 255, 255);
        private static readonly Color TextColorDim = Color.FromArgb(255, 255, 255);
        private static readonly Color BadgeUpdateBg = Color.FromArgb(180, 76, 175, 80);
        private static readonly Color BadgeInstalledBg = Color.FromArgb(180, 60, 145, 230);

        public event EventHandler<int> TileClicked;
        public event EventHandler<int> TileDoubleClicked;

        private class TileAnimationState
        {
            public float Scale = 1.0f;
            public float TargetScale = 1.0f;
            public float BorderOpacity = 0f;
            public float TargetBorderOpacity = 0f;
            public float BackgroundBrightness = 30f;
            public float TargetBackgroundBrightness = 30f;
            public float SelectionOpacity = 0f;
            public float TargetSelectionOpacity = 0f;
            public float TooltipOpacity = 0f;
            public float TargetTooltipOpacity = 0f;
        }

        public FastGalleryPanel(List<ListViewItem> items, int tileWidth, int tileHeight, int spacing, int initialWidth, int initialHeight)
        {
            _items = items ?? new List<ListViewItem>();
            _tileWidth = tileWidth;
            _tileHeight = tileHeight;
            _spacing = spacing;
            _imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
            _cacheOrder = new Queue<string>();
            _tileStates = new Dictionary<int, TileAnimationState>();

            Size = new Size(initialWidth, initialHeight);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.Selectable |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Color.FromArgb(24, 26, 30);

            // Initialize animation states
            for (int i = 0; i < _items.Count; i++)
                _tileStates[i] = new TileAnimationState();

            // Scrollbar - direct interaction jumps immediately (no smooth scroll)
            _scrollBar = new VScrollBar { Minimum = 0, SmallChange = _tileHeight / 2, LargeChange = _tileHeight * 2 };
            _scrollBar.Scroll += (s, e) =>
            {
                _scrollY = _scrollBar.Value;
                _targetScrollY = _scrollBar.Value;
                _isScrolling = false;
                Invalidate();
            };
            Controls.Add(_scrollBar);

            // Animation timer (~120fps)
            _animationTimer = new System.Windows.Forms.Timer { Interval = 8 };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();

            RecalculateLayout();
        }

        public void UpdateItems(List<ListViewItem> newItems)
        {
            if (newItems == null) newItems = new List<ListViewItem>();

            _items = newItems;

            // Reset selection and hover states
            _hoveredIndex = -1;
            _selectedIndex = -1;

            // Rebuild animation states for new item count
            _tileStates.Clear();
            for (int i = 0; i < _items.Count; i++)
                _tileStates[i] = new TileAnimationState();

            // Reset scroll position for new results
            _scrollY = 0;
            _targetScrollY = 0;
            _isScrolling = false;

            RecalculateLayout();
            Invalidate();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            bool needsRedraw = false;

            // Smooth scrolling
            if (_isScrolling)
            {
                float diff = _targetScrollY - _scrollY;
                if (Math.Abs(diff) > 0.5f)
                {
                    _scrollY += diff * SCROLL_SMOOTHING;
                    _scrollY = Math.Max(0, Math.Min(_scrollY, Math.Max(0, _contentHeight - Height)));
                    if (_scrollBar.Visible && _scrollBar.Value != (int)_scrollY)
                        _scrollBar.Value = Math.Max(_scrollBar.Minimum, Math.Min(_scrollBar.Maximum - _scrollBar.LargeChange + 1, (int)_scrollY));
                    needsRedraw = true;
                }
                else
                {
                    _scrollY = _targetScrollY;
                    _isScrolling = false;
                }
            }

            // Tile animations - only process visible tiles for performance
            int scrollYInt = (int)_scrollY;
            int startRow = Math.Max(0, (scrollYInt - _spacing - _tileHeight) / (_tileHeight + _spacing));
            int endRow = Math.Min(_rows - 1, (scrollYInt + Height + _tileHeight) / (_tileHeight + _spacing));

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = 0; col < _columns; col++)
                {
                    int index = row * _columns + col;
                    if (index >= _items.Count) break;

                    if (!_tileStates.TryGetValue(index, out var state))
                    {
                        state = new TileAnimationState();
                        _tileStates[index] = state;
                    }

                    bool isHovered = index == _hoveredIndex;
                    bool isSelected = index == _selectedIndex;

                    state.TargetScale = isHovered ? HOVER_SCALE : 1.0f;
                    state.TargetBorderOpacity = isHovered ? 1.0f : 0f;
                    state.TargetBackgroundBrightness = isHovered ? 45f : (isSelected ? 38f : 30f);
                    state.TargetSelectionOpacity = isSelected ? 1.0f : 0f;
                    state.TargetTooltipOpacity = isHovered ? 1.0f : 0f;

                    if (Math.Abs(state.Scale - state.TargetScale) > 0.001f)
                    {
                        state.Scale += (state.TargetScale - state.Scale) * ANIMATION_SPEED;
                        needsRedraw = true;
                    }
                    else state.Scale = state.TargetScale;

                    if (Math.Abs(state.BorderOpacity - state.TargetBorderOpacity) > 0.01f)
                    {
                        state.BorderOpacity += (state.TargetBorderOpacity - state.BorderOpacity) * ANIMATION_SPEED;
                        needsRedraw = true;
                    }
                    else state.BorderOpacity = state.TargetBorderOpacity;

                    if (Math.Abs(state.BackgroundBrightness - state.TargetBackgroundBrightness) > 0.5f)
                    {
                        state.BackgroundBrightness += (state.TargetBackgroundBrightness - state.BackgroundBrightness) * ANIMATION_SPEED;
                        needsRedraw = true;
                    }
                    else state.BackgroundBrightness = state.TargetBackgroundBrightness;

                    if (Math.Abs(state.SelectionOpacity - state.TargetSelectionOpacity) > 0.01f)
                    {
                        state.SelectionOpacity += (state.TargetSelectionOpacity - state.SelectionOpacity) * ANIMATION_SPEED;
                        needsRedraw = true;
                    }
                    else state.SelectionOpacity = state.TargetSelectionOpacity;

                    if (Math.Abs(state.TooltipOpacity - state.TargetTooltipOpacity) > 0.01f)
                    {
                        state.TooltipOpacity += (state.TargetTooltipOpacity - state.TooltipOpacity) * 0.35f;
                        needsRedraw = true;
                    }
                    else state.TooltipOpacity = state.TargetTooltipOpacity;
                }
            }

            if (needsRedraw) Invalidate();
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (height <= 0 && Height > 0) height = Height;
            if (width <= 0 && Width > 0) width = Width;
            base.SetBoundsCore(x, y, width, height, specified);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 0 && Height > 0 && _scrollBar != null)
            {
                RecalculateLayout();
                Refresh();
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && !IsDisposed && !Disposing)
                RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            if (IsDisposed || Disposing || _scrollBar == null || Width <= 0 || Height <= 0)
                return;

            _scrollBar.SetBounds(Width - _scrollBar.Width, 0, _scrollBar.Width, Height);

            int availableWidth = Width - _scrollBar.Width - _spacing * 2;
            _columns = Math.Max(1, (availableWidth + _spacing) / (_tileWidth + _spacing));
            _rows = (int)Math.Ceiling((double)_items.Count / _columns);
            _contentHeight = _rows * (_tileHeight + _spacing) + _spacing + 20;

            int usedWidth = _columns * (_tileWidth + _spacing) - _spacing;
            _leftPadding = Math.Max(_spacing, (availableWidth - usedWidth) / 2 + _spacing);

            _scrollBar.Maximum = Math.Max(0, _contentHeight);
            _scrollBar.LargeChange = Math.Max(1, Height);
            _scrollBar.Visible = _contentHeight > Height;

            _scrollY = Math.Max(0, Math.Min(_scrollY, Math.Max(0, _contentHeight - Height)));
            _targetScrollY = _scrollY;
            if (_scrollBar.Visible) _scrollBar.Value = (int)_scrollY;

            if (_backBuffer == null || _backBuffer.Width != Width || _backBuffer.Height != Height)
            {
                _backBuffer?.Dispose();
                _backBuffer = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_backBuffer == null) return;

            using (var g = Graphics.FromImage(_backBuffer))
            {
                g.Clear(BackColor);

                if (_isScrolling)
                {
                    g.SmoothingMode = SmoothingMode.HighSpeed;
                    g.InterpolationMode = InterpolationMode.Low;
                }
                else
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                }
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                int scrollYInt = (int)_scrollY;
                int startRow = Math.Max(0, (scrollYInt - _spacing - _tileHeight) / (_tileHeight + _spacing));
                int endRow = Math.Min(_rows - 1, (scrollYInt + Height + _tileHeight) / (_tileHeight + _spacing));

                // Draw non-hovered, non-selected tiles first
                for (int row = startRow; row <= endRow; row++)
                {
                    for (int col = 0; col < _columns; col++)
                    {
                        int index = row * _columns + col;
                        if (index >= _items.Count) break;
                        if (index != _hoveredIndex && index != _selectedIndex)
                            DrawTile(g, index, row, col, scrollYInt);
                    }
                }

                // Draw selected tile
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count && _selectedIndex != _hoveredIndex)
                {
                    int selectedRow = _selectedIndex / _columns;
                    int selectedCol = _selectedIndex % _columns;
                    if (selectedRow >= startRow && selectedRow <= endRow)
                        DrawTile(g, _selectedIndex, selectedRow, selectedCol, scrollYInt);
                }

                // Draw hovered tile last (on top)
                if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
                {
                    int hoveredRow = _hoveredIndex / _columns;
                    int hoveredCol = _hoveredIndex % _columns;
                    if (hoveredRow >= startRow && hoveredRow <= endRow)
                        DrawTile(g, _hoveredIndex, hoveredRow, hoveredCol, scrollYInt);
                }
            }
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawTile(Graphics g, int index, int row, int col, int scrollY)
        {
            var item = _items[index];
            var state = _tileStates.ContainsKey(index) ? _tileStates[index] : new TileAnimationState();

            int baseX = _leftPadding + col * (_tileWidth + _spacing);
            int baseY = _spacing + row * (_tileHeight + _spacing) - scrollY;

            float scale = state.Scale;
            int scaledW = (int)(_tileWidth * scale);
            int scaledH = (int)(_tileHeight * scale);
            int x = baseX - (scaledW - _tileWidth) / 2;
            int y = baseY - (scaledH - _tileHeight) / 2;

            var tileRect = new Rectangle(x, y, scaledW, scaledH);

            // Tile background
            using (var tilePath = CreateRoundedRectangle(tileRect, CORNER_RADIUS))
            {
                int brightness = (int)state.BackgroundBrightness;
                using (var bgBrush = new SolidBrush(Color.FromArgb(255, brightness, brightness, brightness + 2)))
                    g.FillPath(bgBrush, tilePath);

                if (state.SelectionOpacity > 0.01f)
                {
                    using (var selectionPen = new Pen(Color.FromArgb((int)(255 * state.SelectionOpacity), TileBorderSelected), 3f))
                        g.DrawPath(selectionPen, tilePath);
                }

                if (state.BorderOpacity > 0.01f)
                {
                    using (var borderPen = new Pen(Color.FromArgb((int)(200 * state.BorderOpacity), TileBorderHover), 2f))
                        g.DrawPath(borderPen, tilePath);
                }
            }

            // Thumbnail
            int thumbPadding = 4;
            int thumbHeight = scaledH - 26;
            var thumbRect = new Rectangle(x + thumbPadding, y + thumbPadding, scaledW - thumbPadding * 2, thumbHeight);

            string packageName = item.SubItems.Count > 2 ? item.SubItems[2].Text : "";
            var thumbnail = GetCachedImage(packageName);

            using (var thumbPath = CreateRoundedRectangle(thumbRect, THUMB_CORNER_RADIUS))
            {
                var oldClip = g.Clip;
                g.SetClip(thumbPath, CombineMode.Replace);

                if (thumbnail != null)
                {
                    float imgRatio = (float)thumbnail.Width / thumbnail.Height;
                    float rectRatio = (float)thumbRect.Width / thumbRect.Height;
                    Rectangle drawRect = imgRatio > rectRatio
                        ? new Rectangle(thumbRect.X - ((int)(thumbRect.Height * imgRatio) - thumbRect.Width) / 2, thumbRect.Y, (int)(thumbRect.Height * imgRatio), thumbRect.Height)
                        : new Rectangle(thumbRect.X, thumbRect.Y - ((int)(thumbRect.Width / imgRatio) - thumbRect.Height) / 2, thumbRect.Width, (int)(thumbRect.Width / imgRatio));
                    g.DrawImage(thumbnail, drawRect);
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(35, 35, 40)))
                        g.FillPath(brush, thumbPath);
                    using (var textBrush = new SolidBrush(Color.FromArgb(70, 70, 80)))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("🎮", new Font("Segoe UI Emoji", 18f), textBrush, thumbRect, sf);
                    }
                }
                g.Clip = oldClip;
            }

            // Status badges (left side)
            int badgeY = y + thumbPadding + 4;
            bool hasUpdate = item.ForeColor.ToArgb() == ColorTranslator.FromHtml("#4daa57").ToArgb();
            bool installed = item.ForeColor.ToArgb() == ColorTranslator.FromHtml("#3c91e6").ToArgb();

            if (hasUpdate)
            {
                DrawBadge(g, "UPDATE AVAILABLE", x + thumbPadding + 4, badgeY, BadgeUpdateBg);
                badgeY += 18;
            }
            if (installed || hasUpdate)
                DrawBadge(g, "INSTALLED", x + thumbPadding + 4, badgeY, BadgeInstalledBg);

            // Right-side badges
            int rightBadgeY = y + thumbPadding + 4;

            // Size badge (top right) - always visible
            if (item.SubItems.Count > 5)
            {
                string sizeText = FormatSize(item.SubItems[5].Text);
                if (!string.IsNullOrEmpty(sizeText))
                {
                    DrawRightAlignedBadge(g, sizeText, x + scaledW - thumbPadding - 4, rightBadgeY, 1.0f);
                    rightBadgeY += 18;
                }
            }

            // Last updated badge (below size, right aligned) - only on hover with fade
            if (state.TooltipOpacity > 0.01f && item.SubItems.Count > 4)
            {
                string lastUpdated = item.SubItems[4].Text;
                string formattedDate = FormatLastUpdated(lastUpdated);
                if (!string.IsNullOrEmpty(formattedDate))
                {
                    DrawRightAlignedBadge(g, formattedDate, x + scaledW - thumbPadding - 4, rightBadgeY, state.TooltipOpacity);
                }
            }

            // Game name
            var nameRect = new Rectangle(x + 6, y + thumbHeight + thumbPadding, scaledW - 12, 20);
            using (var font = new Font("Segoe UI Semibold", 8f))
            using (var brush = new SolidBrush(TextColor))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(item.Text, font, brush, nameRect, sf);
            }
        }

        private void DrawRightAlignedBadge(Graphics g, string text, int rightX, int y, float opacity = 1.0f)
        {
            using (var font = new Font("Segoe UI", 7f, FontStyle.Bold))
            {
                var sz = g.MeasureString(text, font);
                int badgeWidth = (int)sz.Width + 8;
                var rect = new Rectangle(rightX - badgeWidth, y, badgeWidth, 14);

                int bgAlpha = (int)(180 * opacity);
                int textAlpha = (int)(255 * opacity);

                using (var path = CreateRoundedRectangle(rect, 4))
                using (var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, 0, 0, 0)))
                {
                    g.FillPath(bgBrush, path);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using (var textBrush = new SolidBrush(Color.FromArgb(textAlpha, 255, 255, 255)))
                    {
                        g.DrawString(text, font, textBrush, rect, sf);
                    }
                }
            }
        }

        private string FormatLastUpdated(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return "";

            // Extract just the date part before space
            string datePart = dateStr.Split(' ')[0];

            if (DateTime.TryParse(datePart, out DateTime date))
            {
                // Format as "29 JUL 2025"
                return date.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
            }

            // Fallback: return original if parsing fails
            return dateStr;
        }

        private void DrawBadge(Graphics g, string text, int x, int y, Color bgColor)
        {
            using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            {
                var sz = g.MeasureString(text, font);
                var rect = new Rectangle(x, y, (int)sz.Width + 8, 14);
                using (var path = CreateRoundedRectangle(rect, 4))
                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, font, Brushes.White, rect, sf);
                }
            }
        }

        private string FormatSize(string sizeStr)
        {
            if (double.TryParse(sizeStr?.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mb))
            {
                double gb = mb / 1024.0;
                return gb >= 0.1 ? $"{gb:F2} GB" : $"{mb:F0} MB";
            }
            return "";
        }

        private Image GetCachedImage(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return null;
            if (_imageCache.TryGetValue(packageName, out var cached)) return cached;

            string basePath = SideloaderRCLONE.ThumbnailsFolder;
            string path = new[] { ".jpg", ".png" }.Select(ext => Path.Combine(basePath, packageName + ext)).FirstOrDefault(File.Exists);
            if (path == null) return null;

            try
            {
                while (_imageCache.Count >= MAX_CACHE_SIZE && _cacheOrder.Count > 0)
                {
                    string oldKey = _cacheOrder.Dequeue();
                    if (_imageCache.TryGetValue(oldKey, out var oldImg)) { oldImg.Dispose(); _imageCache.Remove(oldKey); }
                }

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var img = Image.FromStream(stream);
                    _imageCache[packageName] = img;
                    _cacheOrder.Enqueue(packageName);
                    return img;
                }
            }
            catch { return null; }
        }

        private int GetIndexAtPoint(int x, int y)
        {
            int adjustedY = y + (int)_scrollY;
            int col = (x - _leftPadding) / (_tileWidth + _spacing);
            int row = (adjustedY - _spacing) / (_tileHeight + _spacing);

            if (col < 0 || col >= _columns || row < 0) return -1;

            int tileX = _leftPadding + col * (_tileWidth + _spacing);
            int tileY = _spacing + row * (_tileHeight + _spacing);

            if (x >= tileX && x < tileX + _tileWidth && adjustedY >= tileY && adjustedY < tileY + _tileHeight)
            {
                int index = row * _columns + col;
                return index < _items.Count ? index : -1;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = GetIndexAtPoint(e.X, e.Y);
            if (newHover != _hoveredIndex)
            {
                _hoveredIndex = newHover;
                Cursor = newHover >= 0 ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex >= 0) _hoveredIndex = -1;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                int i = GetIndexAtPoint(e.X, e.Y);
                if (i >= 0)
                {
                    _selectedIndex = i;
                    Invalidate();
                    TileClicked?.Invoke(this, i);
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                int i = GetIndexAtPoint(e.X, e.Y);
                if (i >= 0) TileDoubleClicked?.Invoke(this, i);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float scrollAmount = e.Delta * 1.2f;
            int maxScroll = Math.Max(0, _contentHeight - Height);
            _targetScrollY = Math.Max(0, Math.Min(maxScroll, _targetScrollY - scrollAmount));
            _isScrolling = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();

                foreach (var img in _imageCache.Values) { try { img?.Dispose(); } catch { } }
                _imageCache.Clear();
                _cacheOrder.Clear();
                _tileStates.Clear();
                _backBuffer?.Dispose();
                _backBuffer = null;
            }
            base.Dispose(disposing);
        }
    }
}