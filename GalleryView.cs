using AndroidSideloader;
using AndroidSideloader.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public enum SortField { Name, LastUpdated, Size, Popularity }
public enum SortDirection { Ascending, Descending }

public class FastGalleryPanel : Control
{
    // Data
    private List<ListViewItem> _items;
    private List<ListViewItem> _originalItems; // Keep original for re-sorting
    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private readonly int _spacing;

    // Sorting
    private SortField _currentSortField = SortField.Name;
    private SortDirection _currentSortDirection = SortDirection.Ascending;
    private readonly Panel _sortPanel;
    private readonly List<Button> _sortButtons;
    private Label _sortStatusLabel;
    private const int SORT_PANEL_HEIGHT = 36;

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
    private bool _isHoveringDeleteButton = false;

    // Context Menu & Favorites
    private ContextMenuStrip _contextMenu;
    private int _rightClickedIndex = -1;
    private HashSet<string> _favoritesCache;

    // Rendering
    private Bitmap _backBuffer;

    // Visual constants
    private const int CORNER_RADIUS = 10;
    private const int THUMB_CORNER_RADIUS = 6;
    private const float HOVER_SCALE = 1.07f;
    private const float ANIMATION_SPEED = 0.25f;
    private const float SCROLL_SMOOTHING = 0.3f;
    private const int DELETE_BUTTON_SIZE = 26;
    private const int DELETE_BUTTON_MARGIN = 6;

    // Theme colors
    private static readonly Color TileBorderHover = Color.FromArgb(93, 203, 173);
    private static readonly Color TileBorderSelected = Color.FromArgb(200, 200, 200);
    private static readonly Color TileBorderFavorite = Color.FromArgb(255, 215, 0);
    private static readonly Color BadgeFavoriteBg = Color.FromArgb(200, 255, 180, 0);
    private static readonly Color TextColor = Color.FromArgb(245, 255, 255, 255);
    private static readonly Color BadgeInstalledBg = Color.FromArgb(180, 60, 145, 230);
    private static readonly Color DeleteButtonBg = Color.FromArgb(200, 180, 50, 50);
    private static readonly Color DeleteButtonHoverBg = Color.FromArgb(255, 220, 70, 70);
    private static readonly Color SortButtonBg = Color.FromArgb(40, 42, 48);
    private static readonly Color SortButtonActiveBg = Color.FromArgb(93, 203, 173);
    private static readonly Color SortButtonHoverBg = Color.FromArgb(55, 58, 65);

    public event EventHandler<int> TileClicked;
    public event EventHandler<int> TileDoubleClicked;
    public event EventHandler<int> TileDeleteClicked;
    public event EventHandler<int> TileRightClicked;
    public event EventHandler<SortField> SortChanged;

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
        public float DeleteButtonOpacity = 0f;
        public float TargetDeleteButtonOpacity = 0f;
        public float FavoriteOpacity = 0f;
        public float TargetFavoriteOpacity = 0f;
    }

    public FastGalleryPanel(List<ListViewItem> items, int tileWidth, int tileHeight, int spacing, int initialWidth, int initialHeight)
    {
        _originalItems = items ?? new List<ListViewItem>();
        _items = new List<ListViewItem>(_originalItems);
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _spacing = spacing;
        _imageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        _cacheOrder = new Queue<string>();
        _tileStates = new Dictionary<int, TileAnimationState>();
        _sortButtons = new List<Button>();
        RefreshFavoritesCache();

        // Avoid any implicit padding from the control container
        Padding = Padding.Empty;
        Margin = Padding.Empty;

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

        // Create context menu
        CreateContextMenu();

        // Create sort panel
        _sortPanel = CreateSortPanel();
        Controls.Add(_sortPanel);

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

        // Apply initial sort
        ApplySort();
        RecalculateLayout();
    }

    private Panel CreateSortPanel()
    {
        var panel = new Panel
        {
            Height = SORT_PANEL_HEIGHT,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(28, 30, 34),
            Padding = new Padding(8, 4, 8, 4)
        };

        var label = new Label
        {
            Text = "Sort by:",
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(10, 9)
        };
        panel.Controls.Add(label);

        int buttonX = 70;

        SortField[] fields = { SortField.Name, SortField.LastUpdated, SortField.Size, SortField.Popularity };
        string[] texts = { "Name", "Updated", "Size", "Popularity" };

        for (int i = 0; i < fields.Length; i++)
        {
            var btn = CreateSortButton(texts[i], fields[i], buttonX);
            panel.Controls.Add(btn);
            _sortButtons.Add(btn);
            buttonX += btn.Width + 6;
        }

        // Add sort status label to the right of buttons
        _sortStatusLabel = new Label
        {
            Text = GetSortStatusText(),
            ForeColor = Color.FromArgb(140, 140, 140),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            AutoSize = true,
            Location = new Point(buttonX + 10, 9)
        };
        panel.Controls.Add(_sortStatusLabel);

        UpdateSortButtonStyles();
        return panel;
    }

    private string GetSortStatusText()
    {
        switch (_currentSortField)
        {
            case SortField.Name:
                return _currentSortDirection == SortDirection.Ascending ? "A → Z" : "Z → A";
            case SortField.LastUpdated:
                return _currentSortDirection == SortDirection.Ascending ? "Oldest → Newest" : "Newest → Oldest";
            case SortField.Size:
                return _currentSortDirection == SortDirection.Ascending ? "Smallest → Largest" : "Largest → Smallest";
            case SortField.Popularity:
                return _currentSortDirection == SortDirection.Ascending ? "Least → Most Popular" : "Most → Least Popular";
            default:
                return "";
        }
    }

    private Button CreateSortButton(string text, SortField field, int x)
    {
        var btn = new Button
        {
            Text = field == _currentSortField ? GetSortButtonText(text) : text,
            Tag = field,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.White,
            BackColor = SortButtonBg,
            Size = new Size(text == "Popularity" ? 90 : 75, 26),
            Location = new Point(x, 5),
            Cursor = Cursors.Hand
        };

        btn.FlatAppearance.BorderSize = 0;
        // Hover colors will be set dynamically in UpdateSortButtonStyles
        btn.FlatAppearance.MouseOverBackColor = SortButtonHoverBg;
        btn.FlatAppearance.MouseDownBackColor = SortButtonActiveBg;

        btn.Click += (s, e) => OnSortButtonClick(field);
        return btn;
    }

    private string GetSortButtonText(string baseText)
    {
        string arrow = _currentSortDirection == SortDirection.Ascending ? " ▲" : " ▼";
        return baseText + arrow;
    }

    private void OnSortButtonClick(SortField field)
    {
        if (_currentSortField == field)
        {
            // Toggle direction
            _currentSortDirection = _currentSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _currentSortField = field;
            // Popularity, LastUpdated, Size default to descending (most popular/newest/largest first)
            // Name defaults to ascending (A-Z)
            _currentSortDirection = (field == SortField.Name)
                ? SortDirection.Ascending
                : SortDirection.Descending;
        }

        UpdateSortButtonStyles();
        ApplySort();
        SortChanged?.Invoke(this, field);
    }

    private void UpdateSortButtonStyles()
    {
        foreach (var btn in _sortButtons)
        {
            var field = (SortField)btn.Tag;
            bool isActive = field == _currentSortField;

            string baseText = field == SortField.Name ? "Name" :
                              field == SortField.LastUpdated ? "Updated" :
                              field == SortField.Size ? "Size" : "Popularity";

            btn.Text = isActive ? GetSortButtonText(baseText) : baseText;
            btn.BackColor = isActive ? SortButtonActiveBg : SortButtonBg;
            btn.ForeColor = isActive ? Color.FromArgb(24, 26, 30) : Color.White;

            // Set appropriate hover color based on active state
            if (isActive)
            {
                // Active button: use a slightly lighter teal on hover
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(110, 215, 190);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 180, 155);
            }
            else
            {
                // Inactive button: use grey hover
                btn.FlatAppearance.MouseOverBackColor = SortButtonHoverBg;
                btn.FlatAppearance.MouseDownBackColor = SortButtonActiveBg;
            }
        }

        // Update the sort status label
        if (_sortStatusLabel != null)
        {
            _sortStatusLabel.Text = GetSortStatusText();
        }
    }

    private void ApplySort()
    {
        // Reset to original order first
        _items = new List<ListViewItem>(_originalItems);

        // Apply sorting
        switch (_currentSortField)
        {
            case SortField.Name:
                // Custom sort to match list sort behaviour: '_' before digits, digits before letters (case-insensitive)
                if (_currentSortDirection == SortDirection.Ascending)
                    _items = _items.OrderBy(i => i.Text, new GameNameComparer()).ToList();
                else
                    _items = _items.OrderByDescending(i => i.Text, new GameNameComparer()).ToList();
                break;

            case SortField.LastUpdated:
                _items = _currentSortDirection == SortDirection.Ascending
                    ? _items.OrderBy(i => ParseDate(i.SubItems.Count > 4 ? i.SubItems[4].Text : "")).ToList()
                    : _items.OrderByDescending(i => ParseDate(i.SubItems.Count > 4 ? i.SubItems[4].Text : "")).ToList();
                break;

            case SortField.Size:
                _items = _currentSortDirection == SortDirection.Ascending
                    ? _items.OrderBy(i => ParseSize(i.SubItems.Count > 5 ? i.SubItems[5].Text : "0")).ToList()
                    : _items.OrderByDescending(i => ParseSize(i.SubItems.Count > 5 ? i.SubItems[5].Text : "0")).ToList();
                break;

            case SortField.Popularity:
                if (_currentSortDirection == SortDirection.Ascending)
                    _items = _items.OrderBy(i => ParsePopularity(i.SubItems.Count > 6 ? i.SubItems[6].Text : "0"))
                                   .ThenBy(i => i.Text, new GameNameComparer()).ToList();
                else
                    _items = _items.OrderByDescending(i => ParsePopularity(i.SubItems.Count > 6 ? i.SubItems[6].Text : "0"))
                                   .ThenBy(i => i.Text, new GameNameComparer()).ToList();
                break;
        }

        // Reset selection and hover
        _hoveredIndex = -1;
        _selectedIndex = -1;

        // Rebuild animation states
        _tileStates.Clear();
        for (int i = 0; i < _items.Count; i++)
            _tileStates[i] = new TileAnimationState();

        // Reset scroll position
        _scrollY = 0;
        _targetScrollY = 0;

        RecalculateLayout();
        Invalidate();
    }

    private double ParsePopularity(string popStr)
    {
        if (double.TryParse(popStr?.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double pop))
            return pop;
        return 0;
    }

    // Custom sort to match list sort behaviour: '_' before digits, digits before letters (case-insensitive)
    private class GameNameComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int minLen = Math.Min(x.Length, y.Length);
            for (int i = 0; i < minLen; i++)
            {
                char cx = x[i];
                char cy = y[i];

                int orderX = GetCharOrder(cx);
                int orderY = GetCharOrder(cy);

                if (orderX != orderY)
                    return orderX.CompareTo(orderY);

                // Same category, compare case-insensitively
                int cmp = char.ToLowerInvariant(cx).CompareTo(char.ToLowerInvariant(cy));
                if (cmp != 0)
                    return cmp;
            }

            // Shorter string comes first
            return x.Length.CompareTo(y.Length);
        }

        private static int GetCharOrder(char c)
        {
            // Order: underscore (0), digits (1), letters (2), everything else (3)
            if (c == '_') return 0;
            if (char.IsDigit(c)) return 1;
            if (char.IsLetter(c)) return 2;
            return 3;
        }
    }

    private DateTime ParseDate(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
        string datePart = dateStr.Split(' ')[0];
        return DateTime.TryParse(datePart, out DateTime date) ? date : DateTime.MinValue;
    }

    private double ParseSize(string sizeStr)
    {
        if (double.TryParse(sizeStr?.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double mb))
            return mb;
        return 0;
    }

    public void UpdateItems(List<ListViewItem> newItems)
    {
        if (newItems == null) newItems = new List<ListViewItem>();

        _originalItems = new List<ListViewItem>(newItems);
        _items = new List<ListViewItem>(newItems);

        // Reset selection and hover states
        _hoveredIndex = -1;
        _selectedIndex = -1;
        _isHoveringDeleteButton = false;

        // Rebuild animation states for new item count
        _tileStates.Clear();
        for (int i = 0; i < _items.Count; i++)
            _tileStates[i] = new TileAnimationState();

        // Reset scroll position for new results
        _scrollY = 0;
        _targetScrollY = 0;
        _isScrolling = false;

        // Refresh favorites cache and re-apply sort
        RefreshFavoritesCache();
        ApplySort();
    }

    public ListViewItem GetItemAtIndex(int index)
    {
        if (index >= 0 && index < _items.Count)
            return _items[index];
        return null;
    }

    private bool IsItemInstalled(ListViewItem item)
    {
        if (item == null) return false;

        return item.ForeColor.ToArgb() == MainForm.ColorInstalled.ToArgb() ||
               item.ForeColor.ToArgb() == MainForm.ColorUpdateAvailable.ToArgb() ||
               item.ForeColor.ToArgb() == MainForm.ColorDonateGame.ToArgb();
    }

    private Rectangle GetDeleteButtonRect(int index, int row, int col, int scrollY)
    {
        if (!_tileStates.TryGetValue(index, out var state))
            state = new TileAnimationState();

        int baseX = _leftPadding + col * (_tileWidth + _spacing);
        int baseY = _spacing + SORT_PANEL_HEIGHT + row * (_tileHeight + _spacing) - scrollY;

        float scale = state.Scale;
        int scaledW = (int)(_tileWidth * scale);
        int scaledH = (int)(_tileHeight * scale);
        int x = baseX - (scaledW - _tileWidth) / 2;
        int y = baseY - (scaledH - _tileHeight) / 2;

        // Calculate thumbnail area
        int thumbPadding = 4;
        int thumbHeight = scaledH - 26; // Same as in DrawTile

        // Position delete button in bottom-right corner of thumbnail
        int btnX = x + scaledW - DELETE_BUTTON_SIZE - thumbPadding - DELETE_BUTTON_MARGIN;
        int btnY = y + thumbPadding + thumbHeight - DELETE_BUTTON_SIZE - DELETE_BUTTON_MARGIN;

        return new Rectangle(btnX, btnY, DELETE_BUTTON_SIZE, DELETE_BUTTON_SIZE);
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
                _scrollY = Math.Max(0, Math.Min(_scrollY, Math.Max(0, _contentHeight - (Height - SORT_PANEL_HEIGHT))));
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
                bool isInstalled = IsItemInstalled(_items[index]);
                string pkgName = _items[index].SubItems.Count > 2 ? _items[index].SubItems[1].Text : "";
                bool isFavorite = _favoritesCache.Contains(pkgName);
                state.TargetFavoriteOpacity = isFavorite ? 1.0f : 0f;

                state.TargetScale = isHovered ? HOVER_SCALE : 1.0f;
                state.TargetBorderOpacity = isHovered ? 1.0f : 0f;
                state.TargetBackgroundBrightness = isHovered ? 45f : (isSelected ? 38f : 30f);
                state.TargetSelectionOpacity = isSelected ? 1.0f : 0f;
                state.TargetTooltipOpacity = isHovered ? 1.0f : 0f;
                state.TargetDeleteButtonOpacity = (isHovered && isInstalled) ? 1.0f : 0f;

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

                if (Math.Abs(state.DeleteButtonOpacity - state.TargetDeleteButtonOpacity) > 0.01f)
                {
                    state.DeleteButtonOpacity += (state.TargetDeleteButtonOpacity - state.DeleteButtonOpacity) * 0.35f;
                    needsRedraw = true;
                }
                else state.DeleteButtonOpacity = state.TargetDeleteButtonOpacity;

                if (Math.Abs(state.FavoriteOpacity - state.TargetFavoriteOpacity) > 0.01f)
                { state.FavoriteOpacity += (state.TargetFavoriteOpacity - state.FavoriteOpacity) * 0.35f; needsRedraw = true; }
                else state.FavoriteOpacity = state.TargetFavoriteOpacity;
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

        int availableHeight = Height - SORT_PANEL_HEIGHT;
        _scrollBar.SetBounds(Width - _scrollBar.Width, SORT_PANEL_HEIGHT, _scrollBar.Width, availableHeight);

        int availableWidth = Width - _scrollBar.Width - _spacing * 2;
        _columns = Math.Max(1, (availableWidth + _spacing) / (_tileWidth + _spacing));
        _rows = (int)Math.Ceiling((double)_items.Count / _columns);
        _contentHeight = _rows * (_tileHeight + _spacing) + _spacing + 20;

        int usedWidth = _columns * (_tileWidth + _spacing) - _spacing;
        _leftPadding = Math.Max(_spacing, (availableWidth - usedWidth) / 2 + _spacing);

        _scrollBar.Maximum = Math.Max(0, _contentHeight);
        _scrollBar.LargeChange = Math.Max(1, availableHeight);
        _scrollBar.Visible = _contentHeight > availableHeight;

        _scrollY = Math.Max(0, Math.Min(_scrollY, Math.Max(0, _contentHeight - availableHeight)));
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

            // Fill sort panel area
            g.FillRectangle(new SolidBrush(Color.FromArgb(28, 30, 34)), 0, 0, Width, SORT_PANEL_HEIGHT);

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

            // Clip to tile area (below sort panel)
            g.SetClip(new Rectangle(0, SORT_PANEL_HEIGHT, Width, Height - SORT_PANEL_HEIGHT));

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

            g.ResetClip();
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
        int baseY = _spacing + SORT_PANEL_HEIGHT + row * (_tileHeight + _spacing) - scrollY;

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

            // Favorite border (golden)
            if (state.FavoriteOpacity > 0.5f)
            {
                using (var favPen = new Pen(Color.FromArgb((int)(180 * state.FavoriteOpacity), TileBorderFavorite), 1.0f))
                    g.DrawPath(favPen, tilePath);
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

        // Favorite badge
        if (state.FavoriteOpacity > 0.5f)
        {
            DrawBadge(g, "★", x + thumbPadding + 4, badgeY, BadgeFavoriteBg);
            badgeY += 18;
        }

        bool hasUpdate = item.ForeColor.ToArgb() == MainForm.ColorUpdateAvailable.ToArgb();
        bool installed = item.ForeColor.ToArgb() == MainForm.ColorInstalled.ToArgb();
        bool canDonate = item.ForeColor.ToArgb() == MainForm.ColorDonateGame.ToArgb();

        if (hasUpdate)
        {
            DrawBadge(g, "UPDATE AVAILABLE", x + thumbPadding + 4, badgeY, Color.FromArgb(180, MainForm.ColorUpdateAvailable.R, MainForm.ColorUpdateAvailable.G, MainForm.ColorUpdateAvailable.B));
            badgeY += 18;
        }

        if (canDonate)
        {
            DrawBadge(g, "NEWER THAN LIST", x + thumbPadding + 4, badgeY, Color.FromArgb(180, MainForm.ColorDonateGame.R, MainForm.ColorDonateGame.G, MainForm.ColorDonateGame.B));
            badgeY += 18;
        }

        if (installed || hasUpdate || canDonate)
            DrawBadge(g, "INSTALLED", x + thumbPadding + 4, badgeY, BadgeInstalledBg);

        // Right-side badges (top-right of thumbnail)
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

        // Delete button (bottom-right of thumbnail) - for installed apps on hover
        if (state.DeleteButtonOpacity > 0.01f)
        {
            DrawDeleteButton(g, x, y, scaledW, thumbHeight, thumbPadding, state.DeleteButtonOpacity, _isHoveringDeleteButton && index == _hoveredIndex);
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

    private void DrawDeleteButton(Graphics g, int tileX, int tileY, int tileWidth, int thumbHeight, int thumbPadding, float opacity, bool isHovering)
    {
        // Position in bottom-right corner of thumbnail
        int btnX = tileX + tileWidth - DELETE_BUTTON_SIZE - thumbPadding - DELETE_BUTTON_MARGIN;
        int btnY = tileY + thumbPadding + thumbHeight - DELETE_BUTTON_SIZE - DELETE_BUTTON_MARGIN;
        var btnRect = new Rectangle(btnX, btnY, DELETE_BUTTON_SIZE, DELETE_BUTTON_SIZE);

        int bgAlpha = (int)(opacity * 255);
        Color bgColor = isHovering ? DeleteButtonHoverBg : DeleteButtonBg;

        using (var path = CreateRoundedRectangle(btnRect, 6))
        using (var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, bgColor.R, bgColor.G, bgColor.B)))
        {
            g.FillPath(bgBrush, path);
        }

        // Draw trash icon
        int iconPadding = 5;
        int iconX = btnX + iconPadding;
        int iconY = btnY + iconPadding;
        int iconSize = DELETE_BUTTON_SIZE - iconPadding * 2;

        using (var pen = new Pen(Color.FromArgb((int)(opacity * 255), Color.White), 1.5f))
        {
            // Trash can body
            int bodyTop = iconY + 4;
            int bodyBottom = iconY + iconSize;
            int bodyLeft = iconX + 2;
            int bodyRight = iconX + iconSize - 2;

            // Draw body outline (trapezoid-ish shape)
            g.DrawLine(pen, bodyLeft, bodyTop, bodyLeft + 1, bodyBottom);
            g.DrawLine(pen, bodyLeft + 1, bodyBottom, bodyRight - 1, bodyBottom);
            g.DrawLine(pen, bodyRight - 1, bodyBottom, bodyRight, bodyTop);

            // Draw lid
            g.DrawLine(pen, iconX, bodyTop, iconX + iconSize, bodyTop);

            // Draw handle on lid
            int handleLeft = iconX + iconSize / 2 - 3;
            int handleRight = iconX + iconSize / 2 + 3;
            int handleTop = iconY + 1;
            g.DrawLine(pen, handleLeft, bodyTop, handleLeft, handleTop);
            g.DrawLine(pen, handleLeft, handleTop, handleRight, handleTop);
            g.DrawLine(pen, handleRight, handleTop, handleRight, bodyTop);

            // Draw vertical lines inside trash
            int lineY1 = bodyTop + 3;
            int lineY2 = bodyBottom - 3;
            g.DrawLine(pen, iconX + iconSize / 2, lineY1, iconX + iconSize / 2, lineY2);
            if (iconSize > 10)
            {
                g.DrawLine(pen, iconX + iconSize / 2 - 4, lineY1, iconX + iconSize / 2 - 4, lineY2);
                g.DrawLine(pen, iconX + iconSize / 2 + 4, lineY1, iconX + iconSize / 2 + 4, lineY2);
            }
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
        // Account for sort panel offset
        if (y < SORT_PANEL_HEIGHT) return -1;

        int adjustedY = (y - SORT_PANEL_HEIGHT) + (int)_scrollY;
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

    private bool IsPointOnDeleteButton(int x, int y, int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        if (!IsItemInstalled(_items[index])) return false;

        int row = index / _columns;
        int col = index % _columns;
        var btnRect = GetDeleteButtonRect(index, row, col, (int)_scrollY);

        return btnRect.Contains(x, y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int newHover = GetIndexAtPoint(e.X, e.Y);
        bool wasHoveringDelete = _isHoveringDeleteButton;

        if (newHover != _hoveredIndex)
        {
            _hoveredIndex = newHover;
            _isHoveringDeleteButton = false;
        }

        // Check if hovering over delete button
        if (_hoveredIndex >= 0)
        {
            _isHoveringDeleteButton = IsPointOnDeleteButton(e.X, e.Y, _hoveredIndex);
        }
        else
        {
            _isHoveringDeleteButton = false;
        }

        // Update cursor
        if (_isHoveringDeleteButton)
        {
            Cursor = Cursors.Hand;
        }
        else if (_hoveredIndex >= 0)
        {
            Cursor = Cursors.Hand;
        }
        else
        {
            Cursor = Cursors.Default;
        }

        // Redraw if delete button hover state changed
        if (wasHoveringDelete != _isHoveringDeleteButton)
        {
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredIndex >= 0) _hoveredIndex = -1;
        _isHoveringDeleteButton = false;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        // Take focus to unfocus any other control (like search text box)
        if (!Focused)
        {
            Focus();
        }

        if (e.Button == MouseButtons.Left)
        {
            int i = GetIndexAtPoint(e.X, e.Y);
            if (i >= 0)
            {
                // Check if clicking on delete button
                if (IsPointOnDeleteButton(e.X, e.Y, i))
                {
                    // Select this item so the uninstall knows which app to remove
                    _selectedIndex = i;
                    TileClicked?.Invoke(this, i);
                    Invalidate();

                    // Then trigger delete
                    TileDeleteClicked?.Invoke(this, i);
                    return;
                }

                _selectedIndex = i;
                Invalidate();
                TileClicked?.Invoke(this, i);
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            int i = GetIndexAtPoint(e.X, e.Y);
            if (i >= 0)
            {
                _rightClickedIndex = i;
                _selectedIndex = i;
                Invalidate();
                TileClicked?.Invoke(this, i);
                TileRightClicked?.Invoke(this, i);
                _contextMenu.Show(this, e.Location);
            }
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left)
        {
            int i = GetIndexAtPoint(e.X, e.Y);
            if (i >= 0)
            {
                // Don't trigger double-click if on delete button
                if (IsPointOnDeleteButton(e.X, e.Y, i))
                    return;

                TileDoubleClicked?.Invoke(this, i);
            }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        float scrollAmount = e.Delta * 1.2f;
        int maxScroll = Math.Max(0, _contentHeight - (Height - SORT_PANEL_HEIGHT));
        _targetScrollY = Math.Max(0, Math.Min(maxScroll, _targetScrollY - scrollAmount));
        _isScrolling = true;
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.BackColor = Color.FromArgb(40, 42, 48);
        _contextMenu.ForeColor = Color.White;
        _contextMenu.ShowImageMargin = false;
        _contextMenu.Renderer = new MainForm.CenteredMenuRenderer();

        var favoriteItem = new ToolStripMenuItem("★ Add to Favorites");
        favoriteItem.Click += ContextMenu_FavoriteClick;
        _contextMenu.Items.Add(favoriteItem);
        _contextMenu.Opening += ContextMenu_Opening;
    }

    private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_rightClickedIndex < 0 || _rightClickedIndex >= _items.Count) { e.Cancel = true; return; }
        var item = _items[_rightClickedIndex];
        string packageName = item.SubItems.Count > 2 ? item.SubItems[1].Text : "";
        if (string.IsNullOrEmpty(packageName)) { e.Cancel = true; return; }

        bool isFavorite = _favoritesCache.Contains(packageName);
        ((ToolStripMenuItem)_contextMenu.Items[0]).Text = isFavorite ? "Remove from Favorites" : "★ Add to Favorites";
    }

    private void ContextMenu_FavoriteClick(object sender, EventArgs e)
    {
        if (_rightClickedIndex < 0 || _rightClickedIndex >= _items.Count) return;
        var item = _items[_rightClickedIndex];
        string packageName = item.SubItems.Count > 1 ? item.SubItems[1].Text : "";
        if (string.IsNullOrEmpty(packageName)) return;

        var settings = SettingsManager.Instance;
        if (_favoritesCache.Contains(packageName))
        {
            settings.RemoveFavoriteGame(packageName);
            _favoritesCache.Remove(packageName);
        }
        else
        {
            settings.AddFavoriteGame(packageName);
            _favoritesCache.Add(packageName);
        }
        Invalidate();
    }

    public void RefreshFavoritesCache()
    {
        _favoritesCache = new HashSet<string>(SettingsManager.Instance.FavoritedGames, StringComparer.OrdinalIgnoreCase);
    }

    public void ScrollToPackage(string packageName)
    {
        if (string.IsNullOrEmpty(packageName) || _items == null || _items.Count == 0)
            return;

        // Find the index of the item with the matching package name
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item.SubItems.Count > 2 &&
                item.SubItems[2].Text.Equals(packageName, StringComparison.OrdinalIgnoreCase))
            {
                // Calculate the row this item is in
                int row = i / _columns;

                // Calculate the Y position to scroll to (center the row in view if possible)
                int targetY = _spacing + SORT_PANEL_HEIGHT + row * (_tileHeight + _spacing);
                int viewportHeight = this.Height - SORT_PANEL_HEIGHT;
                int centeredY = targetY - (viewportHeight / 2) + (_tileHeight / 2);

                // Clamp to valid scroll range
                int maxScroll = Math.Max(0, _contentHeight - viewportHeight);
                _scrollY = Math.Max(0, Math.Min(centeredY, maxScroll));
                _targetScrollY = _scrollY;

                // Update scrollbar and redraw
                if (_scrollBar.Visible)
                {
                    _scrollBar.Value = Math.Max(_scrollBar.Minimum, Math.Min(_scrollBar.Maximum - _scrollBar.LargeChange + 1, (int)_scrollY));
                }

                // Also select this item visually
                _selectedIndex = i;

                Invalidate();
                break;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _contextMenu?.Dispose();

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