using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
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

            var splash = new SplashForm();
            splash.Show();
            splash.Update();
            Application.DoEvents();

            var mainForm = new MainForm();

            splash.Close();
            splash.Dispose();

            Application.Run(mainForm);
        }
    }

    internal static class WinApiHelper
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        public static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public static void ApplyRoundRegion(Form form, int radius = 20)
        {
            form.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, form.Width + 1, form.Height + 1, radius, radius));
        }

        public static GraphicsPath GetRoundPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            float r = radius;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static bool CheckIsProcessElevated(IntPtr handle)
        {
            if (OpenProcessToken(handle, 0x0008, out IntPtr tokenHandle))
            {
                try
                {
                    int elevationSize = Marshal.SizeOf<int>();
                    IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);
                    try
                    {
                        if (GetTokenInformation(tokenHandle, 20, elevationPtr, (uint)elevationSize, out _))
                        {
                            return Marshal.ReadInt32(elevationPtr) != 0;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(elevationPtr);
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            return false;
        }
    }

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
            this.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
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
                    this.Width = (int)Math.Ceiling(size.Width) + 52;
                }
                else
                {
                    this.Width = (int)Math.Ceiling(size.Width) + 26;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color parentBg = this.Parent?.BackColor ?? Color.White;
            g.Clear(parentBg);

            Color drawColor = _chipColor;
            if (_isPressed) drawColor = ControlPaint.Dark(_chipColor, 0.06f);
            else if (_isHovered) drawColor = ControlPaint.Light(_chipColor, 0.06f);

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            int radius = rect.Height / 2;

            using (var path = WinApiHelper.GetRoundPath(rect, radius))
            {
                using (var brush = new SolidBrush(drawColor))
                {
                    g.FillPath(brush, path);
                }

                if (HasChevron)
                {
                    var textRect = new Rectangle(16, 0, this.Width - 42, this.Height);
                    TextRenderer.DrawText(
                        g,
                        this.Text,
                        this.Font,
                        textRect,
                        this.ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    );

                    int chevronRight = this.Width - 16;
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
    }

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

    public class TossModalDialog : Form
    {
        public TossModalDialog(string title, string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(380, 195);
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
                Height = 32
            };
            btnConfirm.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            this.Controls.Add(lblMsg);
            this.Controls.Add(lblTitle);
            this.Controls.Add(btnConfirm);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            WinApiHelper.ApplyRoundRegion(this, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var path = WinApiHelper.GetRoundPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 20))
            {
                using (var pen = new Pen(Color.FromArgb(215, 221, 230), 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            WinApiHelper.ApplyRoundRegion(this, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var path = WinApiHelper.GetRoundPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 20))
            {
                using (var pen = new Pen(Color.FromArgb(215, 221, 230), 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }

    public class MainForm : Form
    {
        private TossChipButton btnDateFilter = null!;
        private DateTimePicker dtPicker = null!;
        private TossChipButton btnConfirm = null!;
        private TossChipButton btnRefresh = null!;
        private TextBox txtSearch = null!;
        private Label lblItemCount = null!;
        private TossChipButton btnAbout = null!;
        private TossChipButton btnCopyAll = null!;
        private TossChipButton btnSaveAll = null!;
        private DataGridView gridCommands = null!;

        private List<HistoryEntry> allEvents = new();
        private List<HistoryEntry> filteredEvents = new();
        private DateTime selectedDate = DateTime.Today;

        private bool sortTimeDesc = true;
        private bool sortCmdDesc = false;
        private int lastSortColumn = 0; // 0: 시간, 2: 명령어

        public MainForm()
        {
            this.Text = "Powershell 입력 명령어 체크리스트";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(940, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(242, 244, 246);
            this.Font = new Font("맑은 고딕", 9.5F);

            InitializeLayout();
            LoadHistoryData();
            FilterAndDisplay(isInitialLoad: true);
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

            // 상단 타이틀 영역 (타이틀 + 우측 제품 정보 버튼)
            var topArea = new Panel
            {
                Dock = DockStyle.Top,
                Height = 155,
                Padding = new Padding(0, 0, 0, 8),
                BackColor = Color.White
            };

            var titleRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.White
            };

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Left,
                AutoSize = true
            };

            btnAbout = new TossChipButton
            {
                Text = "제품 정보",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Right,
                Height = 32
            };
            btnAbout.AdjustFlexibleWidth();
            btnAbout.Click += (s, e) => ShowAboutDialog();

            titleRow.Controls.Add(lblTitle);
            titleRow.Controls.Add(btnAbout);

            var lblDesc = new Label
            {
                Text = "- 본 프로그램은 입력하신 MDP 진단 관련하여 Powershell 명령어 취합 하는 프로그램입니다. 날짜를 선택후 확인 버튼을 눌러주세요.\r\n" +
                       "- 명령어 입력후 나오는 결과값은 수집되지 않고 단순 명령어만 수집하니 참고 부탁드립니다.",
                Font = new Font("맑은 고딕", 9.5F),
                ForeColor = Color.FromArgb(107, 118, 132),
                Dock = DockStyle.Top,
                Height = 52
            };

            // 필터 및 실시간 검색 컨트롤 바
            var controlRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
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
                RearrangeControls();
            };

            btnDateFilter = new TossChipButton
            {
                Text = selectedDate.ToString("yyyy년 MM월 dd일"),
                HasChevron = true,
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(51, 61, 75),
                Location = new Point(0, 1)
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
            btnConfirm.Click += (s, e) => FilterAndDisplay(isInitialLoad: false);

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
                FilterAndDisplay(isInitialLoad: false);
                TossModalDialog.ShowWithOverlay(this, "새로고침 완료", "명령어 이력을 최신 상태로 갱신했습니다.");
            };

            // 검색창
            var searchPanel = new Panel
            {
                Height = 32,
                Width = 200,
                BackColor = Color.FromArgb(242, 244, 246),
                Padding = new Padding(10, 6, 10, 6)
            };

            txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(25, 31, 40),
                Font = new Font("맑은 고딕", 9.5F),
                Dock = DockStyle.Fill
            };
            txtSearch.TextChanged += (s, e) => FilterAndDisplay(isInitialLoad: true);
            searchPanel.Controls.Add(txtSearch);

            // 실시간 건수 표시 라벨
            lblItemCount = new Label
            {
                Text = "총 0개",
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 120
            };

            controlRow.Controls.Add(dtPicker);
            controlRow.Controls.Add(btnDateFilter);
            controlRow.Controls.Add(btnConfirm);
            controlRow.Controls.Add(btnRefresh);
            controlRow.Controls.Add(searchPanel);
            controlRow.Controls.Add(lblItemCount);

            RearrangeControls();

            topArea.Controls.Add(controlRow);
            topArea.Controls.Add(lblDesc);
            topArea.Controls.Add(titleRow);

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
                HeaderText = "날짜 시간 ▼",
                Width = 175,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };

            var colAuth = new DataGridViewTextBoxColumn
            {
                HeaderText = "구분",
                Width = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            var colPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "명령어",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };

            var colAction = new DataGridViewButtonColumn
            {
                HeaderText = "명령어 보기",
                Width = 120,
                FlatStyle = FlatStyle.Flat
            };

            gridCommands.Columns.AddRange(colTime, colAuth, colPreview, colAction);

            // 컬럼 헤더 클릭 시 오름차순/내림차순 정렬
            gridCommands.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex == 0) // 날짜 시간
                {
                    sortTimeDesc = !sortTimeDesc;
                    lastSortColumn = 0;
                    gridCommands.Columns[0].HeaderText = sortTimeDesc ? "날짜 시간 ▼" : "날짜 시간 ▲";
                    gridCommands.Columns[2].HeaderText = "명령어";
                    SortAndBindGrid();
                }
                else if (e.ColumnIndex == 2) // 명령어
                {
                    sortCmdDesc = !sortCmdDesc;
                    lastSortColumn = 2;
                    gridCommands.Columns[2].HeaderText = sortCmdDesc ? "명령어 ▼" : "명령어 ▲";
                    gridCommands.Columns[0].HeaderText = "날짜 시간";
                    SortAndBindGrid();
                }
            };

            gridCommands.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 1) // 구분 컬럼
                {
                    // 행 선택 시 연하늘색(#E8F3FF), 평상 시 흰색으로 배경 처리
                    bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                    Color cellBg = isSelected ? Color.FromArgb(232, 243, 255) : Color.White;

                    using (var b = new SolidBrush(cellBg))
                    {
                        e.Graphics.FillRectangle(b, e.CellBounds);
                    }

                    // 하단 분할선
                    using (var pen = new Pen(Color.FromArgb(242, 244, 246)))
                    {
                        e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    }

                    bool isAdmin = filteredEvents.Count > e.RowIndex && filteredEvents[e.RowIndex].IsAdmin;
                    string tagText = isAdmin ? "관리자" : "일반";

                    // 관리자: 티파니 앤 코 민트색 (#81D8D0) / 일반: 연한 회색 (#F2F4F6)
                    Color chipBg = isAdmin ? Color.FromArgb(129, 216, 208) : Color.FromArgb(242, 244, 246);
                    Color textClr = isAdmin ? Color.White : Color.FromArgb(25, 31, 40);

                    var tagRect = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + 11, 56, 26);
                    using (var path = WinApiHelper.GetRoundPath(tagRect, 13))
                    {
                        using (var brush = new SolidBrush(chipBg))
                        {
                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            e.Graphics.FillPath(brush, path);
                        }

                        TextRenderer.DrawText(
                            e.Graphics,
                            tagText,
                            new Font("맑은 고딕", 8.5F, FontStyle.Bold),
                            tagRect,
                            textClr,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        );
                    }
                    e.Handled = true;
                }
                else if (e.RowIndex >= 0 && e.ColumnIndex == 3) // 명령어 보기 버튼
                {
                    bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                    Color cellBg = isSelected ? Color.FromArgb(232, 243, 255) : Color.White;

                    using (var b = new SolidBrush(cellBg))
                    {
                        e.Graphics.FillRectangle(b, e.CellBounds);
                    }

                    using (var pen = new Pen(Color.FromArgb(242, 244, 246)))
                    {
                        e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    }

                    var btnRect = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + 8, e.CellBounds.Width - 20, 32);
                    using (var path = WinApiHelper.GetRoundPath(btnRect, 16))
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
                if (e.RowIndex >= 0 && e.ColumnIndex == 3) OpenDetail(e.RowIndex);
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

        private void RearrangeControls()
        {
            int spacing = 6;
            btnConfirm.Location = new Point(btnDateFilter.Right + spacing, 1);
            btnRefresh.Location = new Point(btnConfirm.Right + spacing, 1);

            var searchPanel = txtSearch.Parent;
            if (searchPanel != null)
            {
                searchPanel.Location = new Point(btnRefresh.Right + 12, 1);
            }
        }

        private void ShowAboutDialog()
        {
            string info = "프로그램 : Powershell 입력 명령어 체크리스트\r\n" +
                          "최종 버전 : 1.0\r\n" +
                          "최종 수정 날짜 : 26.09.04\r\n" +
                          "제작자 : kkh";
            TossModalDialog.ShowWithOverlay(this, "제품 정보", info);
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

        // 실시간 활성 파워쉘 프로세스의 관리자 여부 매핑
        private Dictionary<int, bool> GetActivePowerShellElevations()
        {
            var dict = new Dictionary<int, bool>();
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        if (proc.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                            proc.ProcessName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
                        {
                            bool isAdmin = false;
                            // 1. 창 타이틀 검사 (가장 확실한 직관적 기준)
                            if (proc.MainWindowTitle.Contains("관리자") || proc.MainWindowTitle.Contains("Administrator"))
                            {
                                isAdmin = true;
                            }
                            else
                            {
                                // 2. 프로세스 토큰 Elevation 검사
                                isAdmin = WinApiHelper.CheckIsProcessElevated(proc.Handle);
                            }
                            dict[proc.Id] = isAdmin;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return dict;
        }

        private void LoadHistoryData()
        {
            allEvents.Clear();
            var activePsMap = GetActivePowerShellElevations();
            bool hasAnyActiveAdmin = activePsMap.ContainsValue(true);

            // 1. 이벤트 로그(4104) 분석
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
                            bool isAdmin = false;
                            if (record.ProcessId.HasValue && activePsMap.TryGetValue(record.ProcessId.Value, out bool adminStatus))
                            {
                                isAdmin = adminStatus;
                            }
                            else if (record.UserId != null && record.UserId.Value.StartsWith("S-1-5-18"))
                            {
                                isAdmin = true;
                            }

                            allEvents.Add(new HistoryEntry { Time = time, FullCommand = clean, IsAdmin = isAdmin });
                        }
                    }
                }
            }
            catch { }

            // 2. 콘솔 히스토리 로드
            LoadConsoleHistoryFile(hasAnyActiveAdmin);

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

        private void LoadConsoleHistoryFile(bool hasAnyActiveAdmin)
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
                            // 이벤트 로그에 이미 관리자 플래그가 매핑되어 있는지 확인
                            var match = allEvents.Find(e => e.FullCommand.Equals(cmd, StringComparison.OrdinalIgnoreCase));
                            bool isAdmin = match != null ? match.IsAdmin : false;

                            allEvents.Add(new HistoryEntry
                            {
                                Time = modTime.AddSeconds(-offsetSeconds),
                                FullCommand = cmd,
                                IsAdmin = isAdmin
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

        private void FilterAndDisplay(bool isInitialLoad = false)
        {
            string keyword = txtSearch?.Text.Trim() ?? "";

            filteredEvents = allEvents.FindAll(x =>
            {
                if (x.Time.Date != selectedDate) return false;
                if (!string.IsNullOrEmpty(keyword) && !x.FullCommand.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            });

            SortAndBindGrid();

            if (lblItemCount != null)
            {
                lblItemCount.Text = string.IsNullOrEmpty(keyword) ? $"총 {filteredEvents.Count}개" : $"검색 {filteredEvents.Count}개";
            }

            if (filteredEvents.Count == 0 && !isInitialLoad && string.IsNullOrEmpty(keyword))
            {
                TossModalDialog.ShowWithOverlay(this, "조회 결과", "선택하신 날짜에 기록된 명령어가 없습니다.");
            }
        }

        private void SortAndBindGrid()
        {
            if (lastSortColumn == 0)
            {
                filteredEvents.Sort((a, b) => sortTimeDesc ? b.Time.CompareTo(a.Time) : a.Time.CompareTo(b.Time));
            }
            else if (lastSortColumn == 2)
            {
                filteredEvents.Sort((a, b) => sortCmdDesc ? string.Compare(b.FullCommand, a.FullCommand, StringComparison.OrdinalIgnoreCase)
                                                           : string.Compare(a.FullCommand, b.FullCommand, StringComparison.OrdinalIgnoreCase));
            }

            gridCommands.Rows.Clear();
            foreach (var item in filteredEvents)
            {
                string firstLine = item.FullCommand.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (firstLine.EndsWith("`")) firstLine = firstLine.TrimEnd('`').Trim();

                string preview = firstLine.Length > 55 ? firstLine.Substring(0, 55) + "..." : firstLine;
                gridCommands.Rows.Add(item.Time.ToString("yyyy-MM-dd HH:mm:ss"), item.IsAdmin ? "관리자" : "일반", preview, "명령어 보기");
            }
        }

        private void OpenDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= filteredEvents.Count) return;
            var entry = filteredEvents[rowIndex];

            using (var overlay = new DimOverlayForm(this))
            {
                overlay.Show();
                using (var dlg = new CommandDetailModal(entry.Time, entry.FullCommand, entry.IsAdmin))
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
                string authStr = item.IsAdmin ? "[관리자]" : "[일반]";
                sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {authStr} | {item.FullCommand}");
            }

            Clipboard.SetText(sb.ToString());
            TossModalDialog.ShowWithOverlay(this, "클립보드 복사", "현재 표시된 전체 명령어가 복사되었습니다.");
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
                        string authStr = item.IsAdmin ? "[관리자]" : "[일반]";
                        sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {authStr} | {item.FullCommand}");
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    TossModalDialog.ShowWithOverlay(this, "저장 완료", "명령어 목록이 텍스트 파일로 저장되었습니다.");
                }
            }
        }
    }

    public class CommandDetailModal : Form
    {
        public CommandDetailModal(DateTime time, string command, bool isAdmin)
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

            string authLabel = isAdmin ? "[관리자]" : "[일반]";
            var lblTitle = new Label
            {
                Text = $"명령어 상세 보기 {authLabel} ({time:yyyy-MM-dd HH:mm:ss})",
                Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Location = new Point(0, 8),
                AutoSize = true
            };

            int btnY = (topPanel.Height - 32) / 2;

            var btnSave = new TossChipButton
            {
                Text = "저장하기",
                ChipColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Height = 32
            };
            btnSave.AdjustFlexibleWidth();
            btnSave.Location = new Point(topPanel.Width - btnSave.Width, btnY);
            btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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

            var btnCopy = new TossChipButton
            {
                Text = "복사하기",
                ChipColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Height = 32
            };
            btnCopy.AdjustFlexibleWidth();
            btnCopy.Location = new Point(btnSave.Left - btnCopy.Width - 8, btnY);
            btnCopy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(command);
                TossModalDialog.ShowWithOverlay(this, "복사 완료", "명령어가 클립보드에 복사되었습니다.");
            };

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(btnCopy);
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
                Height = 44,
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            WinApiHelper.ApplyRoundRegion(this, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var path = WinApiHelper.GetRoundPath(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 20))
            {
                using (var pen = new Pen(Color.FromArgb(215, 221, 230), 1.5f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }

    public class HistoryEntry
    {
        public DateTime Time { get; set; }
        public string FullCommand { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
    }
}
