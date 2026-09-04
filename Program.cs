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
                System.Threading.Thread.Sleep(750);

                var mainForm = new MainForm();
                splash.Close();
                Application.Run(mainForm);
            }
        }
    }

    // 딤 오버레이 (Dim Overlay)
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
            this.Opacity = 0.40;
            this.Owner = parent;
        }
    }

    // 가이드 규격 칩 버튼 (Height: 32px, Padding: Left 12 / Right 12, Flexible Width)
    public class TossChipButton : Button
    {
        public bool HasChevron { get; set; } = false;

        public TossChipButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            this.Height = 32; // 가이드 규격 32px 고정
            this.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            this.DoubleBuffered = true;
        }

        public void AdjustFlexibleWidth()
        {
            using (var g = this.CreateGraphics())
            {
                var size = g.MeasureString(this.Text, this.Font);
                if (HasChevron)
                {
                    // Filter / Input 규격: 좌 12px + 텍스트 + 간격 1px + 쉐브론(약 8px) + 우 10px
                    this.Width = (int)Math.Ceiling(size.Width) + 12 + 1 + 8 + 10;
                }
                else
                {
                    // Select 규격: 좌 12px + 텍스트 + 우 12px
                    this.Width = (int)Math.Ceiling(size.Width) + 24;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, this.Width, this.Height);
            int radius = 16; // 32px 높이 기준 완전한 캡슐 라운딩 (r = 16)

            using (var path = GetCapsulePath(rect, radius))
            {
                this.Region = new Region(path);

                using (var brush = new SolidBrush(this.BackColor))
                {
                    pevent.Graphics.FillPath(brush, path);
                }

                if (HasChevron)
                {
                    // 좌측 12px 기준 텍스트 출력
                    var textRect = new Rectangle(12, 0, this.Width - 31, this.Height);
                    TextRenderer.DrawText(
                        pevent.Graphics,
                        this.Text,
                        this.Font,
                        textRect,
                        this.ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    );

                    // 우측 10px 마진 쉐브론 화살표(∨) 렌더링
                    int chevronRight = this.Width - 10;
                    int chevronY = this.Height / 2;
                    using (var pen = new Pen(this.ForeColor, 1.5F))
                    {
                        pevent.Graphics.DrawLines(pen, new Point[] {
                            new Point(chevronRight - 8, chevronY - 2),
                            new Point(chevronRight - 4, chevronY + 2),
                            new Point(chevronRight, chevronY - 2)
                        });
                    }
                }
                else
                {
                    TextRenderer.DrawText(
                        pevent.Graphics,
                        this.Text,
                        this.Font,
                        rect,
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

    // 모달 알림창
    public class TossModalDialog : Form
    {
        public TossModalDialog(string title, string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(380, 180);
            this.BackColor = Color.FromArgb(235, 238, 242);
            this.Padding = new Padding(1);

            var innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("맑은 고딕", 12F, FontStyle.Bold),
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
                BackColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 32
            };
            btnConfirm.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            innerPanel.Controls.Add(lblMsg);
            innerPanel.Controls.Add(lblTitle);
            innerPanel.Controls.Add(btnConfirm);
            this.Controls.Add(innerPanel);
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

    // 로딩 스플래시 창
    public class SplashForm : Form
    {
        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(380, 100);
            this.BackColor = Color.FromArgb(230, 233, 237);
            this.Padding = new Padding(1);
            this.TopMost = true;

            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var lbl = new Label
            {
                Text = "프로그램이 실행중이니 잠시만 기다려 주세요",
                Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(lbl);
            this.Controls.Add(panel);
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

            // 상단 타이틀 및 설명 헤더
            var topArea = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(0, 0, 0, 8)
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

            // 가이드 규격 칩 그룹 바 (가로 간격: 6px, 높이: 32px)
            var controlRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32
            };

            // 숨김 상태로 동작하는 실제 날짜 픽커
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

            // Filter/Input 규격 칩 (좌 12px, 쉐브론 ∨, 우 10px, 높이 32px)
            btnDateFilter = new TossChipButton
            {
                Text = selectedDate.ToString("yyyy년 MM월 dd일"),
                HasChevron = true,
                BackColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(51, 61, 75),
                Location = new Point(0, 0)
            };
            btnDateFilter.AdjustFlexibleWidth();
            btnDateFilter.Click += (s, e) =>
            {
                // 칩 클릭 시 달력 팝업 드롭다운 트리거
                dtPicker.Location = new Point(btnDateFilter.Left, btnDateFilter.Bottom);
                dtPicker.Visible = true;
                dtPicker.Focus();
                SendKeys.Send("%{DOWN}");
            };

            btnConfirm = new TossChipButton
            {
                Text = "확인",
                BackColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White
            };
            btnConfirm.AdjustFlexibleWidth();
            btnConfirm.Click += (s, e) => FilterBySelectedDate(isInitialLoad: false);

            btnRefresh = new TossChipButton
            {
                Text = "새로고침",
                BackColor = Color.FromArgb(242, 244, 246),
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

            // 하단 액션 버튼 및 면책 문구 (칩 가로 간격: 6px, 높이: 32px)
            var bottomArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 90,
                Padding = new Padding(0, 4, 0, 0)
            };

            var actionRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };

            btnSaveAll = new TossChipButton
            {
                Text = "전체 명령어 저장",
                BackColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Dock = DockStyle.Right
            };
            btnSaveAll.AdjustFlexibleWidth();
            btnSaveAll.Click += (s, e) => SaveAllToFile();

            var spacer = new Panel { Dock = DockStyle.Right, Width = 6 }; // 가이드 규격 Gap: 6px

            btnCopyAll = new TossChipButton
            {
                Text = "전체 복사",
                BackColor = Color.FromArgb(242, 244, 246),
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

            // 중앙 리스트 테이블 (높이 48px 카드 로우)
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 10)
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
            gridCommands.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold);
            gridCommands.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            gridCommands.ColumnHeadersHeight = 38;

            gridCommands.DefaultCellStyle.SelectionBackColor = Color.FromArgb(242, 244, 246);
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
                HeaderText = "명령어 첫부분",
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

            // 목록 내 버튼도 규격(Height: 32px, 캡슐형) 페인팅
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

        // Chip Group 가로 간격(6px) 배치
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

            // 1. 이벤트 로그(4104) 수집
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

            // 2. 콘솔 히스토리 수집
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

    // 상세 보기 모달
    public class CommandDetailModal : Form
    {
        public CommandDetailModal(DateTime time, string command)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(720, 500);
            this.BackColor = Color.FromArgb(235, 238, 242);
            this.Padding = new Padding(1);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24)
            };

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44
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
                BackColor = Color.FromArgb(49, 130, 246),
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

            var spacer = new Panel { Dock = DockStyle.Right, Width = 6 }; // Gap: 6px

            var btnCopy = new TossChipButton
            {
                Text = "복사하기",
                BackColor = Color.FromArgb(242, 244, 246),
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
                Padding = new Padding(0, 10, 0, 0)
            };

            var btnClose = new TossChipButton
            {
                Text = "닫기",
                BackColor = Color.FromArgb(242, 244, 246),
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Fill,
                Height = 32
            };
            btnClose.Click += (s, e) => this.Close();
            bottomPanel.Controls.Add(btnClose);

            panel.Controls.Add(txtContent);
            panel.Controls.Add(topPanel);
            panel.Controls.Add(bottomPanel);
            this.Controls.Add(panel);
        }
    }

    public class HistoryEntry
    {
        public DateTime Time { get; set; }
        public string FullCommand { get; set; } = string.Empty;
    }
}
