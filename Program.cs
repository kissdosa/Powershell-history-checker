using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PSHistoryChecker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using (var splash = new SplashForm())
            {
                splash.Show();
                splash.Refresh();
                System.Threading.Thread.Sleep(800);

                var mainForm = new MainForm();
                splash.Close();
                Application.Run(mainForm);
            }
        }
    }

    // "명령어 보기"와 동일한 원리로 렌더링되는 초고화질 안티에일리어싱 칩 버튼
    public class TossChipButton : Control
    {
        public bool HasChevron { get; set; } = false;
        private Color _chipColor = Color.FromArgb(49, 130, 246);
        private bool _isHovered = false;
        private bool _isPressed = false;

        public Color ChipColor
        {
            get => _chipColor;
            set { _chipColor = value; Invalidate(); }
        }

        public TossChipButton()
        {
            this.SetStyle(ControlStyles.UserPaint |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.ResizeRedraw, true);

            this.Cursor = Cursors.Hand;
            this.Height = 32;
            this.Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; _isPressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _isPressed = false; Invalidate(); }

        public void AdjustFlexibleWidth()
        {
            using (var g = this.CreateGraphics())
            {
                var size = g.MeasureString(this.Text, this.Font);
                if (HasChevron)
                {
                    this.Width = (int)Math.Ceiling(size.Width) + 12 + 1 + 8 + 12;
                }
                else
                {
                    this.Width = (int)Math.Ceiling(size.Width) + 24;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 부모 컨트롤의 배경색으로 먼저 깨끗하게 채워 모서리 검은 잔상 100% 방지
            Color parentBg = this.Parent?.BackColor ?? Color.White;
            g.Clear(parentBg);

            // 마우스 인터랙션 피드백
            Color drawColor = _chipColor;
            if (_isPressed)
            {
                drawColor = ControlPaint.Dark(_chipColor, 0.06f);
            }
            else if (_isHovered)
            {
                drawColor = ControlPaint.Light(_chipColor, 0.06f);
            }

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int radius = rect.Height / 2;

            using (var path = GetCapsulePath(rect, radius))
            {
                using (var brush = new SolidBrush(drawColor))
                {
                    g.FillPath(brush, path);
                }

                if (HasChevron)
                {
                    var textRect = new Rectangle(12, 0, this.Width - 32, this.Height);
                    TextRenderer.DrawText(
                        g,
                        this.Text,
                        this.Font,
                        textRect,
                        this.ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    );

                    int chevronRight = this.Width - 12;
                    int chevronY = this.Height / 2;
                    using (var pen = new Pen(this.ForeColor, 1.8F))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, new Point[] {
                            new Point(chevronRight - 8, chevronY - 2),
                            new Point(chevronRight - 4, chevronY + 2),
                            new Point(chevronRight, chevronY - 2)
                        });
                    }
                }
                else
                {
                    TextRenderer.DrawText(
                        g,
                        this.Text,
                        this.Font,
                        new Rectangle(0, 0, this.Width, this.Height),
                        this.ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    );
                }
            }
        }

        public static GraphicsPath GetCapsulePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2F;
            var arcRect = new RectangleF(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arcRect, 90, 180);
            arcRect.X = rect.Right - diameter;
            path.AddArc(arcRect, 270, 180);
            path.CloseFigure();
            return path;
        }
    }

    // 딤 오버레이
    public class DimOverlayForm : Form
    {
        public DimOverlayForm(Form parent)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = parent.Location;
            this.Size = parent.Size;
            this.BackColor = Color.Black;
            this.Opacity = 0.35;
            this.Owner = parent;
        }
    }

    // 깔끔한 1픽셀 테두리가 적용된 모달 알림창
    public class TossModalDialog : Form
    {
        public TossModalDialog(string title, string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(380, 185);
            this.BackColor = Color.White;
            this.Padding = new Padding(24);

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("맑은 고딕", 12.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblMsg = new Label
            {
                Text = message,
                Font = new Font("맑은 고딕", 9.5F),
                ForeColor = Color.FromArgb(107, 118, 132),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnConfirm = new TossChipButton
            {
                Text = "확인",
                ChipColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 36
            };
            btnConfirm.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            this.Controls.Add(lblMsg);
            this.Controls.Add(lblTitle);
            this.Controls.Add(btnConfirm);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 깔끔한 1픽셀 외곽선 렌더링
            using (var pen = new Pen(Color.FromArgb(220, 225, 232), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        public static void ShowWithOverlay(Form owner, string title, string message)
        {
            using (var overlay = new DimOverlayForm(owner))
            {
                overlay.Show();
                using (var dlg = new TossModalDialog(title, message))
                {
                    dlg.ShowDialog(overlay);
                }
                overlay.Close();
            }
        }
    }

    // 엑셀 취합 프로그램 스타일의 시작 대기 팝업
    public class SplashForm : Form
    {
        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(380, 130);
            this.BackColor = Color.White;
            this.Padding = new Padding(20);
            this.TopMost = true;

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblSub = new Label
            {
                Text = "프로그램을 실행 중입니다...",
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 130, 246),
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblWait = new Label
            {
                Text = "잠시만 기다려 주세요.",
                Font = new Font("맑은 고딕", 8.5F),
                ForeColor = Color.FromArgb(142, 151, 163),
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.Add(lblWait);
            this.Controls.Add(lblSub);
            this.Controls.Add(lblTitle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(220, 225, 232), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }

    // 메인 창
    public class MainForm : Form
    {
        private TossChipButton btnDateFilter = null!;
        private DateTimePicker dtPicker = null!;
        private TossChipButton btnConfirm = null!;
        private TossChipButton btnRefresh = null!;
        private TossChipButton btnCopyAll = null!;
        private TossChipButton btnSaveAll = null!;
        private DataGridView gridCommands = null!;
        private List<HistoryEntry> allEvents = new();
        private List<HistoryEntry> filteredEvents = new();
        private DateTime selectedDate = DateTime.Today;

        public MainForm()
        {
            this.Text = "Powershell 입력 명령어 체크리스트";
            this.Size = new Size(920, 680);
            this.MinimumSize = new Size(880, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(242, 244, 246);
            this.Font = new Font("맑은 고딕", 9.5F);

            InitializeLayout();
            LoadHistoryData();
            FilterBySelectedDate(isInitialLoad: true);
        }

        private void InitializeLayout()
        {
            var rootPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = Color.FromArgb(242, 244, 246)
            };

            var cardPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(28)
            };

            var topArea = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Top,
                Height = 36
            };

            var lblDesc = new Label
            {
                Text = "- 본 프로그램은 입력하신 MDP 진단 관련하여 Powershell 명령어 취합 하는 프로그램입니다. 날짜를 선택후 확인 버튼을 눌러주세요.\r\n" +
                       "- 명령어 입력후 나오는 결과값은 수집되지 않고 단순 명령어만 수집하니 참고 부탁드립니다.",
                Font = new Font("맑은 고딕", 9.5F),
                ForeColor = Color.FromArgb(107, 118, 132),
                Dock = DockStyle.Top,
                Height = 52
            };

            var controlRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Color.White
            };

            dtPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy년 MM월 dd일",
                Visible = false,
                Location = new Point(0, 0)
            };
            dtPicker.ValueChanged += (s, e) =>
            {
                selectedDate = dtPicker.Value.Date;
                btnDateFilter.Text = selectedDate.ToString("yyyy년 MM월 dd일");
                btnDateFilter.AdjustFlexibleWidth();
                RearrangeFilterChips();
            };

            btnDateFilter = new TossChipButton
            {
                Text = selectedDate.ToString("yyyy년 MM월 dd일"),
                HasChevron = true,
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(51, 61, 75),
                Location = new Point(0, 0)
            };
            btnDateFilter.AdjustFlexibleWidth();
            btnDateFilter.Click += (s, e) =>
            {
                dtPicker.Location = new Point(btnDateFilter.Left, btnDateFilter.Bottom);
                dtPicker.Visible = true;
                dtPicker.Focus();
                SendKeys.Send("%{DOWN}");
            };

            btnConfirm = new TossChipButton
            {
                Text = "확인",
                ChipColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White
            };
            btnConfirm.AdjustFlexibleWidth();
            btnConfirm.Click += (s, e) => FilterBySelectedDate(isInitialLoad: false);

            btnRefresh = new TossChipButton
            {
                Text = "새로고침",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104)
            };
            btnRefresh.AdjustFlexibleWidth();
            btnRefresh.Click += (s, e) =>
            {
                LoadHistoryData();
                FilterBySelectedDate(isInitialLoad: false);
                TossModalDialog.ShowWithOverlay(this, "새로고침 완료", "명령어 이력을 최신 상태로 갱신했습니다.");
            };

            controlRow.Controls.Add(dtPicker);
            controlRow.Controls.Add(btnDateFilter);
            controlRow.Controls.Add(btnConfirm);
            controlRow.Controls.Add(btnRefresh);

            RearrangeFilterChips();

            topArea.Controls.Add(controlRow);
            topArea.Controls.Add(lblDesc);
            topArea.Controls.Add(lblTitle);

            var bottomArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                Padding = new Padding(0, 4, 0, 0),
                BackColor = Color.White
            };

            var actionRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.White
            };

            btnSaveAll = new TossChipButton
            {
                Text = "전체 명령어 저장",
                ChipColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Dock = DockStyle.Right
            };
            btnSaveAll.AdjustFlexibleWidth();
            btnSaveAll.Click += (s, e) => SaveAllToFile();

            var spacer = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.White };

            btnCopyAll = new TossChipButton
            {
                Text = "전체 복사",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Right
            };
            btnCopyAll.AdjustFlexibleWidth();
            btnCopyAll.Click += (s, e) => CopyAll();

            actionRow.Controls.Add(btnCopyAll);
            actionRow.Controls.Add(spacer);
            actionRow.Controls.Add(btnSaveAll);

            var lblFooter = new Label
            {
                Text = "본 프로그램은 악성코드 분석을 위해 제공되는 도구입니다. 사용자는 본 프로그램을 자유롭게 수정, 사용, 배포할 수 있습니다. 단, 임의 수정 및 배포시 발생하는 문제는 수정자 본인에게 있음을 알려드립니다.",
                Font = new Font("맑은 고딕", 8F),
                ForeColor = Color.FromArgb(142, 151, 163),
                Dock = DockStyle.Bottom,
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter
            };

            bottomArea.Controls.Add(actionRow);
            bottomArea.Controls.Add(lblFooter);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10),
                BackColor = Color.White
            };

            gridCommands = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(242, 244, 246),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 48 },
                EnableHeadersVisualStyles = false
            };

            gridCommands.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            gridCommands.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(107, 118, 132);
            gridCommands.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 250, 251);
            gridCommands.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(107, 118, 132);
            gridCommands.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold);
            gridCommands.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            gridCommands.ColumnHeadersHeight = 38;

            gridCommands.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 243, 255);
            gridCommands.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 31, 40);
            gridCommands.DefaultCellStyle.Font = new Font("맑은 고딕", 9.5F);

            var colTime = new DataGridViewTextBoxColumn
            {
                HeaderText = "날짜 시간",
                Width = 175,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            var colPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "명령어",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            var colAction = new DataGridViewButtonColumn
            {
                HeaderText = "명령어 보기",
                Width = 125,
                FlatStyle = FlatStyle.Flat
            };

            gridCommands.Columns.AddRange(colTime, colPreview, colAction);

            gridCommands.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 2)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

                    var btnRect = new Rectangle(e.CellBounds.X + 12, e.CellBounds.Y + 8, e.CellBounds.Width - 24, 32);
                    using (var path = TossChipButton.GetCapsulePath(btnRect, 16))
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(49, 130, 246)))
                        {
                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            e.Graphics.FillPath(brush, path);
                        }

                        TextRenderer.DrawText(
                            e.Graphics,
                            "명령어 보기",
                            new Font("맑은 고딕", 9F, FontStyle.Bold),
                            btnRect,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        );
                    }
                    e.Handled = true;
                }
            };

            gridCommands.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 2) OpenDetail(e.RowIndex);
            };
            gridCommands.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) OpenDetail(e.RowIndex);
            };

            gridContainer.Controls.Add(gridCommands);

            cardPanel.Controls.Add(gridContainer);
            cardPanel.Controls.Add(bottomArea);
            cardPanel.Controls.Add(topArea);

            rootPanel.Controls.Add(cardPanel);
            this.Controls.Add(rootPanel);
        }

        private void RearrangeFilterChips()
        {
            int spacing = 6;
            btnConfirm.Location = new Point(btnDateFilter.Right + spacing, 0);
            btnRefresh.Location = new Point(btnConfirm.Right + spacing, 0);
        }

        private bool IsSystemInternalNoise(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return true;
            string s = cmd.Trim();

            if (s.StartsWith("<#") || s.StartsWith("#requires")) return true;
            if (s.StartsWith("#") && !s.StartsWith("# ")) return true;
            if (s.Contains("$__cmdletization", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("TabExpansion2", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("PSReadLine", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("Get-ItemProperty HKLM:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("prompt", StringComparison.OrdinalIgnoreCase) || s.StartsWith("function prompt", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("prompt\r\n") || s.StartsWith("prompt\n")) return true;
            if (s.Contains("param(") && s.Contains("$ExecutionContext")) return true;
            if (s.Contains("Set-StrictMode") && s.Contains("$global:")) return true;
            if (s.Contains("Microsoft.PowerShell.") && s.Contains("Export-ModuleMember")) return true;
            if (s.Contains("CommandLine=prompt", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("Out-Default", StringComparison.OrdinalIgnoreCase) && s.Length < 30) return true;

            return false;
        }

        private void LoadHistoryData()
        {
            allEvents.Clear();

            try
            {
                string query = "*[System[(EventID=4104)]]";
                var logQuery = new EventLogQuery("Microsoft-Windows-PowerShell/Operational", PathType.LogName, query) { ReverseDirection = true };
                using var reader = new EventLogReader(logQuery);
                EventRecord record;
                while ((record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        var time = record.TimeCreated ?? DateTime.Now;
                        string msg = record.FormatDescription() ?? "";

                        int idx = msg.IndexOf(":\r\n", StringComparison.Ordinal);
                        if (idx < 0) idx = msg.IndexOf(":\n", StringComparison.Ordinal);
                        if (idx >= 0 && (msg.StartsWith("Scriptblock") || msg.StartsWith("스크립트 블록") || msg.StartsWith("Creating Scriptblock")))
                        {
                            msg = msg.Substring(idx + 2);
                        }

                        string clean = msg.Trim();
                        if (!string.IsNullOrWhiteSpace(clean) && !IsSystemInternalNoise(clean))
                        {
                            allEvents.Add(new HistoryEntry { Time = time, FullCommand = clean });
                        }
                    }
                }
            }
            catch { }

            LoadConsoleHistoryFile();
            DeduplicateAndSortEvents();
        }

        private List<string> ParseCommandsFromLines(IEnumerable<string> rawLines)
        {
            var commands = new List<string>();
            var currentCmd = new StringBuilder();
            int braceDepth = 0;
            int parenDepth = 0;

            foreach (var rawLine in rawLines)
            {
                string line = rawLine;
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if (currentCmd.Length > 0)
                {
                    currentCmd.AppendLine(line);
                }
                else
                {
                    currentCmd.Append(line);
                }

                foreach (char c in trimmed)
                {
                    if (c == '{') braceDepth++;
                    else if (c == '}') braceDepth = Math.Max(0, braceDepth - 1);
                    else if (c == '(') parenDepth++;
                    else if (c == ')') parenDepth = Math.Max(0, parenDepth - 1);
                }

                bool continues = trimmed.EndsWith("`") || trimmed.EndsWith("|") || braceDepth > 0 || parenDepth > 0;

                if (!continues)
                {
                    commands.Add(currentCmd.ToString().Trim());
                    currentCmd.Clear();
                    braceDepth = 0;
                    parenDepth = 0;
                }
            }

            if (currentCmd.Length > 0)
            {
                commands.Add(currentCmd.ToString().Trim());
            }

            return commands;
        }

        private void LoadConsoleHistoryFile()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string historyPath = Path.Combine(appData, @"Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt");

                if (File.Exists(historyPath))
                {
                    var fileInfo = new FileInfo(historyPath);
                    DateTime modTime = fileInfo.LastWriteTime;

                    var rawLines = new List<string>();
                    using (var fs = new FileStream(historyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            rawLines.Add(line);
                        }
                    }

                    var parsedCommands = ParseCommandsFromLines(rawLines);
                    parsedCommands.Reverse();
                    int offsetSeconds = 0;

                    foreach (var cmd in parsedCommands)
                    {
                        if (!string.IsNullOrWhiteSpace(cmd) && !IsSystemInternalNoise(cmd))
                        {
                            allEvents.Add(new HistoryEntry
                            {
                                Time = modTime.AddSeconds(-offsetSeconds),
                                FullCommand = cmd
                            });
                            offsetSeconds++;
                        }
                    }
                }
            }
            catch { }
        }

        private void DeduplicateAndSortEvents()
        {
            var unique = new List<HistoryEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in allEvents)
            {
                string key = $"{item.Time:yyyy-MM-dd}|{item.FullCommand.Trim()}";
                if (seen.Add(key))
                {
                    unique.Add(item);
                }
            }

            unique.Sort((a, b) => b.Time.CompareTo(a.Time));
            allEvents = unique;
        }

        private void FilterBySelectedDate(bool isInitialLoad = false)
        {
            filteredEvents = allEvents.FindAll(x => x.Time.Date == selectedDate);

            gridCommands.Rows.Clear();
            foreach (var item in filteredEvents)
            {
                string firstLine = item.FullCommand.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (firstLine.EndsWith("`")) firstLine = firstLine.TrimEnd('`').Trim();

                string preview = firstLine.Length > 55 ? firstLine.Substring(0, 55) + "..." : firstLine;
                gridCommands.Rows.Add(item.Time.ToString("yyyy-MM-dd HH:mm:ss"), preview, "명령어 보기");
            }

            if (filteredEvents.Count == 0 && !isInitialLoad)
            {
                TossModalDialog.ShowWithOverlay(this, "조회 결과", "선택하신 날짜에 기록된 명령어가 없습니다.");
            }
        }

        private void OpenDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= filteredEvents.Count) return;
            var entry = filteredEvents[rowIndex];

            using (var overlay = new DimOverlayForm(this))
            {
                overlay.Show();
                using (var dlg = new CommandDetailModal(entry.Time, entry.FullCommand))
                {
                    dlg.ShowDialog(overlay);
                }
                overlay.Close();
            }
        }

        private void CopyAll()
        {
            if (filteredEvents.Count == 0)
            {
                TossModalDialog.ShowWithOverlay(this, "복사 실패", "복사할 명령어 항목이 없습니다.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in filteredEvents)
            {
                sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {item.FullCommand}");
            }

            Clipboard.SetText(sb.ToString());
            TossModalDialog.ShowWithOverlay(this, "클립보드 복사", "선택된 날짜의 전체 명령어가 복사되었습니다.");
        }

        private void SaveAllToFile()
        {
            if (filteredEvents.Count == 0)
            {
                TossModalDialog.ShowWithOverlay(this, "저장 실패", "저장할 내역이 없습니다.");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                sfd.FileName = $"PS_History_{selectedDate:yyyyMMdd}.txt";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    foreach (var item in filteredEvents)
                    {
                        sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {item.FullCommand}");
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    TossModalDialog.ShowWithOverlay(this, "저장 완료", "명령어 목록이 텍스트 파일로 저장되었습니다.");
                }
            }
        }
    }

    // 명령어 보기 상세 모달
    public class CommandDetailModal : Form
    {
        public CommandDetailModal(DateTime time, string command)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(720, 500);
            this.BackColor = Color.White;
            this.Padding = new Padding(24);

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White
            };

            var lblTitle = new Label
            {
                Text = $"명령어 상세 보기 ({time:yyyy-MM-dd HH:mm:ss})",
                Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Location = new Point(0, 6),
                AutoSize = true
            };

            var btnSave = new TossChipButton
            {
                Text = "저장하기",
                ChipColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Dock = DockStyle.Right
            };
            btnSave.AdjustFlexibleWidth();
            btnSave.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                    sfd.FileName = $"PS_Cmd_{time:yyyyMMdd_HHmmss}.txt";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, command, Encoding.UTF8);
                        TossModalDialog.ShowWithOverlay(this, "저장 완료", "명령어가 파일로 저장되었습니다.");
                    }
                }
            };

            var spacer = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Color.White };

            var btnCopy = new TossChipButton
            {
                Text = "복사하기",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Right
            };
            btnCopy.AdjustFlexibleWidth();
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(command);
                TossModalDialog.ShowWithOverlay(this, "복사 완료", "명령어가 클립보드에 복사되었습니다.");
            };

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(btnCopy);
            topPanel.Controls.Add(spacer);
            topPanel.Controls.Add(btnSave);

            var txtContent = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = command,
                Font = new Font("Consolas", 10F),
                BackColor = Color.FromArgb(249, 250, 251),
                ForeColor = Color.FromArgb(51, 61, 75),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.White
            };

            var btnClose = new TossChipButton
            {
                Text = "닫기",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Fill,
                Height = 32
            };
            btnClose.Click += (s, e) => this.Close();
            bottomPanel.Controls.Add(btnClose);

            this.Controls.Add(txtContent);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(220, 225, 232), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }

    public class HistoryEntry
    {
        public DateTime Time { get; set; }
        public string FullCommand { get; set; } = string.Empty;
    }
}
