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

    public class RoundedButton : Button
    {
        public int BorderRadius { get; set; } = 8;

        public RoundedButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, this.Width, this.Height);
            using (var path = GetRoundPath(rect, BorderRadius))
            {
                this.Region = new Region(path);

                using (var brush = new SolidBrush(this.BackColor))
                {
                    pevent.Graphics.FillPath(brush, path);
                }

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
    }

    public class TossModalDialog : Form
    {
        public TossModalDialog(string title, string message)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(360, 180);
            this.BackColor = Color.FromArgb(230, 233, 237);
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
                ForeColor = Color.FromArgb(78, 89, 104),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var btnConfirm = new RoundedButton
            {
                Text = "확인",
                BorderRadius = 10,
                BackColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Bottom,
                Height = 40
            };
            btnConfirm.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            innerPanel.Controls.Add(lblMsg);
            innerPanel.Controls.Add(lblTitle);
            innerPanel.Controls.Add(btnConfirm);
            this.Controls.Add(innerPanel);
        }

        public static void Show(IWin32Window owner, string title, string message)
        {
            using (var dlg = new TossModalDialog(title, message))
            {
                dlg.ShowDialog(owner);
            }
        }
    }

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

    public class MainForm : Form
    {
        private DateTimePicker dtPicker = null!;
        private RoundedButton btnConfirm = null!;
        private RoundedButton btnRefresh = null!;
        private RoundedButton btnCopyAll = null!;
        private RoundedButton btnSaveAll = null!;
        private DataGridView gridCommands = null!;
        private List<HistoryEntry> allEvents = new();
        private List<HistoryEntry> filteredEvents = new();

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
                Padding = new Padding(20),
                BackColor = Color.FromArgb(242, 244, 246)
            };

            var cardPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24)
            };

            // 상단 영역
            var topArea = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(0, 0, 0, 8)
            };

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Top,
                Height = 34
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
                Height = 34
            };

            dtPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy년 MM월 dd일",
                Font = new Font("맑은 고딕", 10F),
                Location = new Point(0, 1),
                Width = 160,
                Height = 32
            };

            int btnHeight = dtPicker.Height;

            btnConfirm = new RoundedButton
            {
                Text = "확인",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Location = new Point(168, 1),
                Size = new Size(72, btnHeight)
            };
            btnConfirm.Click += (s, e) => FilterBySelectedDate(isInitialLoad: false);

            btnRefresh = new RoundedButton
            {
                Text = "새로고침",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                Location = new Point(246, 1),
                Size = new Size(82, btnHeight)
            };
            btnRefresh.Click += (s, e) =>
            {
                LoadHistoryData();
                FilterBySelectedDate(isInitialLoad: false);
                TossModalDialog.Show(this, "새로고침 완료", "명령어 이력을 최신 상태로 갱신했습니다.");
            };

            controlRow.Controls.Add(dtPicker);
            controlRow.Controls.Add(btnConfirm);
            controlRow.Controls.Add(btnRefresh);

            topArea.Controls.Add(controlRow);
            topArea.Controls.Add(lblDesc);
            topArea.Controls.Add(lblTitle);

            // 하단 영역
            var bottomArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                Padding = new Padding(0, 8, 0, 0)
            };

            var actionRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38
            };

            btnSaveAll = new RoundedButton
            {
                Text = "전체 명령어 저장",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Dock = DockStyle.Right,
                Width = 135
            };
            btnSaveAll.Click += (s, e) => SaveAllToFile();

            var spacer = new Panel { Dock = DockStyle.Right, Width = 8 };

            btnCopyAll = new RoundedButton
            {
                Text = "전체 복사",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                Dock = DockStyle.Right,
                Width = 95
            };
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
                Height = 38,
                TextAlign = ContentAlignment.MiddleCenter
            };

            bottomArea.Controls.Add(actionRow);
            bottomArea.Controls.Add(lblFooter);

            // 중앙 테이블
            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };

            gridCommands = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 40 },
                EnableHeadersVisualStyles = false
            };

            gridCommands.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            gridCommands.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(78, 89, 104);
            gridCommands.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold);
            gridCommands.ColumnHeadersHeight = 38;
            gridCommands.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 243, 254);
            gridCommands.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 31, 40);

            var colTime = new DataGridViewTextBoxColumn
            {
                HeaderText = "날짜 시간",
                Width = 165,
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
                Text = "명령어 보기",
                UseColumnTextForButtonValue = true,
                Width = 120,
                FlatStyle = FlatStyle.Flat
            };

            gridCommands.Columns.AddRange(colTime, colPreview, colAction);

            // 토스 블루 둥근 버튼 커스텀 페인팅
            gridCommands.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 2)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

                    var btnRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + 4, e.CellBounds.Width - 16, e.CellBounds.Height - 8);
                    using (var path = RoundedButton.GetRoundPath(btnRect, 6))
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

        private bool IsSystemInternalNoise(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return true;
            string s = cmd.Trim();

            if (s.StartsWith("<#") || s.StartsWith("#requires")) return true;
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

            // 2. 콘솔 히스토리 수집 (여러 줄 묶음 파싱 및 최신순 역순 배치)
            LoadConsoleHistoryFile();

            // 중복 제거 및 시간 정렬
            DeduplicateAndSortEvents();
        }

        // 여러 줄로 이루어진 스크립트/명령어를 하나로 병합하는 파서
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

                // 백틱, 파이프, 미완성 괄호인 경우 다음 줄과 연결
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

                    // 여러 줄 명령어 병합 파싱
                    var parsedCommands = ParseCommandsFromLines(rawLines);

                    // 파일의 맨 끝(가장 최근 입력한 명령어)부터 최신순으로 시간 배정
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
                // 앞뒤 공백 및 줄바꿈 정리 기준 중복 체크
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
            DateTime target = dtPicker.Value.Date;
            filteredEvents = allEvents.FindAll(x => x.Time.Date == target);

            gridCommands.Rows.Clear();
            foreach (var item in filteredEvents)
            {
                // 명령어의 첫 번째 줄만 추출
                string firstLine = item.FullCommand.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (firstLine.EndsWith("`")) firstLine = firstLine.TrimEnd('`').Trim();

                string preview = firstLine.Length > 55 ? firstLine.Substring(0, 55) + "..." : firstLine;
                gridCommands.Rows.Add(item.Time.ToString("yyyy-MM-dd HH:mm:ss"), preview, "명령어 보기");
            }

            if (filteredEvents.Count == 0 && !isInitialLoad)
            {
                TossModalDialog.Show(this, "조회 결과", "선택하신 날짜에 기록된 명령어가 없습니다.");
            }
        }

        private void OpenDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= filteredEvents.Count) return;
            var entry = filteredEvents[rowIndex];
            using (var dlg = new CommandDetailModal(entry.Time, entry.FullCommand))
            {
                dlg.ShowDialog(this);
            }
        }

        private void CopyAll()
        {
            if (filteredEvents.Count == 0)
            {
                TossModalDialog.Show(this, "복사 실패", "복사할 명령어 항목이 없습니다.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in filteredEvents)
            {
                sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {item.FullCommand}");
            }

            Clipboard.SetText(sb.ToString());
            TossModalDialog.Show(this, "클립보드 복사", "선택된 날짜의 전체 명령어가 복사되었습니다.");
        }

        private void SaveAllToFile()
        {
            if (filteredEvents.Count == 0)
            {
                TossModalDialog.Show(this, "저장 실패", "저장할 내역이 없습니다.");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                sfd.FileName = $"PS_History_{dtPicker.Value:yyyyMMdd}.txt";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    foreach (var item in filteredEvents)
                    {
                        sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {item.FullCommand}");
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    TossModalDialog.Show(this, "저장 완료", "명령어 목록이 텍스트 파일로 저장되었습니다.");
                }
            }
        }
    }

    public class CommandDetailModal : Form
    {
        public CommandDetailModal(DateTime time, string command)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(700, 480);
            this.BackColor = Color.FromArgb(230, 233, 237);
            this.Padding = new Padding(1);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44
            };

            var lblTitle = new Label
            {
                Text = $"명령어 상세 보기 ({time:yyyy-MM-dd HH:mm:ss})",
                Font = new Font("맑은 고딕", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Location = new Point(0, 8),
                AutoSize = true
            };

            var btnCopy = new RoundedButton
            {
                Text = "복사하기",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                Size = new Size(82, 32),
                Location = new Point(490, 4)
            };
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(command);
                TossModalDialog.Show(this, "복사 완료", "명령어가 클립보드에 복사되었습니다.");
            };

            var btnSave = new RoundedButton
            {
                Text = "저장하기",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Size = new Size(82, 32),
                Location = new Point(578, 4)
            };
            btnSave.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                    sfd.FileName = $"PS_Cmd_{time:yyyyMMdd_HHmmss}.txt";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, command, Encoding.UTF8);
                        TossModalDialog.Show(this, "저장 완료", "명령어가 파일로 저장되었습니다.");
                    }
                }
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
                Dock = DockStyle.Fill
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(0, 8, 0, 0)
            };

            var btnClose = new RoundedButton
            {
                Text = "닫기",
                BorderRadius = 10,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                Dock = DockStyle.Fill
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
