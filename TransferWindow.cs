using System;
using System.Drawing;
using System.Windows.Forms;

namespace AndroidSideloader
{
    // FileZilla-style popup showing every in-flight file transfer (SFTP / push / pull)
    // with a live per-file progress bar, speed and status. Non-modal so it never blocks
    // the app; hides on close and re-shows itself when new transfers start.
    public class TransferWindow : Form
    {
        private static TransferWindow _instance;
        private static readonly object _instLock = new object();

        private readonly ListView _list;
        private readonly Timer _timer;

        private static readonly Color Bg = Color.FromArgb(25, 25, 25);
        private static readonly Color BgAlt = Color.FromArgb(32, 32, 32);
        private static readonly Color Fg = Color.White;
        private static readonly Color HeaderBg = Color.FromArgb(35, 35, 35);
        private static readonly Color BarBack = Color.FromArgb(52, 52, 52);
        private static readonly Color Accent = Color.FromArgb(0, 120, 215);
        private static readonly Color DoneColor = Color.FromArgb(60, 160, 90);
        private static readonly Color FailColor = Color.FromArgb(200, 70, 70);

        private sealed class BufferedListView : ListView
        {
            public BufferedListView() { DoubleBuffered = true; }
        }

        private TransferWindow()
        {
            Text = "Transfers";
            ClientSize = new Size(680, 340);
            MinimumSize = new Size(420, 200);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Fg;
            ShowInTaskbar = true;
            try { if (Program.form != null && !Program.form.IsDisposed) Icon = Program.form.Icon; } catch { }

            _list = new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                OwnerDraw = true,
                BackColor = Bg,
                ForeColor = Fg,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _list.Columns.Add("File", 300);
            _list.Columns.Add("Size", 90, HorizontalAlignment.Right);
            _list.Columns.Add("Progress", 140);
            _list.Columns.Add("Speed", 80, HorizontalAlignment.Right);
            _list.Columns.Add("Status", 70, HorizontalAlignment.Left);
            _list.DrawColumnHeader += OnDrawHeader;
            _list.DrawItem += (s, e) => { /* per-subitem drawing below */ };
            _list.DrawSubItem += OnDrawSubItem;

            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = BgAlt };
            Button clearBtn = new Button
            {
                Text = "Clear finished",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Fg,
                BackColor = Color.FromArgb(45, 45, 45),
                Width = 130,
                Height = 26,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            clearBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            clearBtn.Location = new Point(bottom.ClientSize.Width - clearBtn.Width - 8, 7);
            clearBtn.Click += (s, e) => TransferQueue.ClearFinished();
            bottom.Controls.Add(clearBtn);

            Controls.Add(_list);
            Controls.Add(bottom);

            _timer = new Timer { Interval = 250 };
            _timer.Tick += (s, e) => RefreshList();
            _timer.Start();

            FormClosing += (s, e) =>
            {
                // Hide (keep the singleton alive) rather than dispose on the X button.
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        // Called from any thread when a transfer is added/updated. Creates and shows the
        // window on the UI thread if needed.
        public static void NotifyActivity()
        {
            Form owner = Program.form;
            if (owner == null || owner.IsDisposed) return;
            try
            {
                owner.BeginInvoke((Action)(() =>
                {
                    lock (_instLock)
                    {
                        if (_instance == null || _instance.IsDisposed)
                        {
                            _instance = new TransferWindow();
                        }
                    }
                    if (!_instance.Visible)
                    {
                        _instance.Show(owner);
                    }
                    _instance.BringToFront();
                }));
            }
            catch { }
        }

        private void RefreshList()
        {
            var items = TransferQueue.Snapshot();

            _list.BeginUpdate();
            while (_list.Items.Count > items.Count)
            {
                _list.Items.RemoveAt(_list.Items.Count - 1);
            }
            for (int i = 0; i < items.Count; i++)
            {
                TransferItem t = items[i];
                ListViewItem row;
                if (i < _list.Items.Count)
                {
                    row = _list.Items[i];
                }
                else
                {
                    row = new ListViewItem();
                    row.SubItems.Add("");
                    row.SubItems.Add("");
                    row.SubItems.Add("");
                    row.SubItems.Add("");
                    _list.Items.Add(row);
                }

                row.Tag = t;
                row.SubItems[0].Text = t.Name;
                row.SubItems[1].Text = FormatSize(t.TotalBytes);
                double pct = t.TotalBytes > 0 ? (t.TransferredBytes * 100.0 / t.TotalBytes) : 0;
                row.SubItems[2].Text = pct.ToString("0.0") + "%";
                row.SubItems[3].Text = t.State == TransferState.Active ? t.SpeedMBps.ToString("0.0") + " MB/s" : "";
                row.SubItems[4].Text = StatusText(t.State);
            }
            _list.EndUpdate();
        }

        private static string StatusText(TransferState s)
        {
            switch (s)
            {
                case TransferState.Queued: return "Queued";
                case TransferState.Active: return "Active";
                case TransferState.Done: return "Done";
                case TransferState.Failed: return "Failed";
                default: return "";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            double mb = bytes / 1048576.0;
            return mb >= 1024 ? (mb / 1024.0).ToString("0.00") + " GB" : mb.ToString("0.0") + " MB";
        }

        private void OnDrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(HeaderBg))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Font, e.Bounds, Color.Gainsboro,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
            using (Pen p = new Pen(Color.FromArgb(55, 55, 55)))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            TransferItem t = e.Item.Tag as TransferItem;
            Color rowBg = (e.ItemIndex % 2 == 0) ? Bg : BgAlt;
            using (SolidBrush b = new SolidBrush(rowBg))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            if (e.ColumnIndex == 2 && t != null)
            {
                Rectangle r = e.Bounds;
                r.Inflate(-4, -5);
                using (SolidBrush bb = new SolidBrush(BarBack))
                {
                    e.Graphics.FillRectangle(bb, r);
                }
                double frac = t.TotalBytes > 0 ? Math.Max(0, Math.Min(1, (double)t.TransferredBytes / t.TotalBytes)) : 0;
                Rectangle fill = r;
                fill.Width = (int)(r.Width * frac);
                Color barColor = t.State == TransferState.Failed ? FailColor
                               : t.State == TransferState.Done ? DoneColor
                               : Accent;
                using (SolidBrush fb = new SolidBrush(barColor))
                {
                    e.Graphics.FillRectangle(fb, fill);
                }
                TextRenderer.DrawText(e.Graphics, (frac * 100).ToString("0.0") + "%", Font, e.Bounds, Fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding | TextFormatFlags.EndEllipsis;
                if (e.Header != null && e.Header.TextAlign == HorizontalAlignment.Right)
                {
                    flags |= TextFormatFlags.Right;
                }
                Color txt = Fg;
                if (e.ColumnIndex == 4 && t != null)
                {
                    txt = t.State == TransferState.Done ? Color.FromArgb(120, 200, 140)
                        : t.State == TransferState.Failed ? Color.FromArgb(230, 120, 120)
                        : t.State == TransferState.Active ? Color.FromArgb(120, 180, 240)
                        : Color.Gainsboro;
                }
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font, e.Bounds, txt, flags);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _timer?.Stop(); _timer?.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
