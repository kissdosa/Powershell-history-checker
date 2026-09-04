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
                System.Threading.Thread.Sleep(1000);

                var mainForm = new MainForm();
                splash.Close();
                Application.Run(mainForm);
            }
        }
    }

    // 애플/토스 스타일 둥근 모서리 버튼 컴포넌트
    public class RoundedButton : Button
    {
        public int BorderRadius { get; set; } = 10;

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

    // 요청하신 디자인의 커스텀 알림/확인 모달 팝업
    public class TossModalDialog : Form
    {
        public TossModalDialog(string title, string message, bool showCancel = false)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(360, 190);
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
                Font = new Font("맑은 고딕", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Top,
                Height = 35,
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

            var btnPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44
            };

            var btnConfirm = new RoundedButton
            {
                Text = "확인",
                BorderRadius = 12,
                BackColor = Color.FromArgb(49, 130, 246),
                ForeColor = Color.White,
                Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                Size = showCancel ? new Size(145, 42) : new Size(300, 42),
                Location = showCancel ? new Point(165, 0) : new Point(10, 0)
            };
            btnConfirm.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            if (showCancel)
            {
                var btnCancel = new RoundedButton
                {
                    Text = "취소",
                    BorderRadius = 12,
                    BackColor = Color.FromArgb(242, 244, 246),
                    ForeColor = Color.FromArgb(78, 89, 104),
                    Font = new Font("맑은 고딕", 10F, FontStyle.Bold),
                    Size = new Size(145, 42),
                    Location = new Point(10, 0)
                };
                btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
                btnPanel.Controls.Add(btnCancel);
            }

            btnPanel.Controls.Add(btnConfirm);
            innerPanel.Controls.Add(lblMsg);
            innerPanel.Controls.Add(lblTitle);
            innerPanel.Controls.Add(btnPanel);
            this.Controls.Add(innerPanel);
        }

        public static DialogResult Show(IWin32Window owner, string title, string message, bool showCancel = false)
        {
            using (var dlg = new TossModalDialog(title, message, showCancel))
            {
                return dlg.ShowDialog(owner);
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
            this.Size = new Size(380, 110);
            this.BackColor = Color.FromArgb(230, 233, 237);
            this.Padding = new Padding(1);
            this.TopMost = true;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var lblMessage = new Label
            {
                Text = "프로그램이 실행중이니 잠시만 기다려 주세요",
                Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(lblMessage);
            this.Controls.Add(panel);
        }
    }

    // 메인 창
    public class MainForm : Form
    {
        private DateTimePicker dtPicker = null!;
        private RoundedButton btnConfirm = null!;
        private RoundedButton btnCopyAll = null!;
        private RoundedButton btnSaveAll = null!;
        private DataGridView gridCommands = null!;
        private List<HistoryEntry> allEvents = new();
        private List<HistoryEntry> filteredEvents = new();

        public MainForm()
        {
            this.Text = "Powershell 입력 명령어 체크리스트";
            this.Size = new Size(820, 620);
            this.MinimumSize = new Size(760, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(242, 244, 246);
            this.Font = new Font("맑은 고딕", 9.5F);

            InitializeLayout();
            LoadHistoryData();
        }

        private void InitializeLayout()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = Color.FromArgb(242, 244, 246)
            };

            var cardPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24)
            };

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Location = new Point(24, 20),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var lblDesc = new Label
            {
                Text = "- 본 프로그램은 입력하신 MDP 진단 관련하여 Powershell 명령어 취합 하는 프로그램입니다. 날짜를 선택후 확인 버튼을 눌러주세요.\r\n" +
                       "- 명령어 입력후 나오는 결과값은 수집되지 않고 단순 명령어만 수집하니 참고 부탁드립니다.",
                Font = new Font("맑은 고딕", 9F),
                ForeColor = Color.FromArgb(107, 118, 132),
                Location = new Point(24, 52),
                Size = new Size(720, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var datePanel = new Panel
            {
                Location = new Point(24, 100),
                Size = new Size(720, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            dtPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy년 MM월 dd일",
                Font = new Font("맑은 고딕", 10.5F),
                Location = new Point(0, 0),
                Width = 170,
                Height = 36
            };

            btnConfirm = new RoundedButton
            {
                Text = "확인",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Location = new Point(178, 0),
                Size = new Size(76, 36)
            };
            btnConfirm.Click += (s, e) => FilterBySelectedDate();

            datePanel.Controls.Add(dtPicker);
            datePanel.Controls.Add(btnConfirm);

            gridCommands = new DataGridView
            {
                Location = new Point(24, 150),
                Size = new Size(720, 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 36 },
                EnableHeadersVisualStyles = false
            };

            gridCommands.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            gridCommands.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(78, 89, 104);
            gridCommands.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold);
            gridCommands.ColumnHeadersHeight = 36;
            gridCommands.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 243, 254);
            gridCommands.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 31, 40);

            var colTime = new DataGridViewTextBoxColumn
            {
                HeaderText = "날짜 시간",
                Width = 170,
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
                Text = "상세 보기",
                UseColumnTextForButtonValue = true,
                Width = 110,
                FlatStyle = FlatStyle.Flat
            };

            gridCommands.Columns.AddRange(colTime, colPreview, colAction);
            gridCommands.CellContentClick += GridCommands_CellContentClick;

            var actionPanel = new Panel
            {
                Location = new Point(24, 382),
                Size = new Size(720, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnCopyAll = new RoundedButton
            {
                Text = "전체 복사",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCopyAll.Location = new Point(actionPanel.Width - 212, 0);
            btnCopyAll.Click += (s, e) => CopyAll();

            btnSaveAll = new RoundedButton
            {
                Text = "전체 TXT 저장",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Size = new Size(106, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSaveAll.Location = new Point(actionPanel.Width - 106, 0);
            btnSaveAll.Click += (s, e) => SaveAllToFile();

            actionPanel.Controls.Add(btnCopyAll);
            actionPanel.Controls.Add(btnSaveAll);

            var lblFooter = new Label
            {
                Text = "본 프로그램은 악성코드 분석을 위해 제공되는 도구입니다. 사용자는 본 프로그램을 자유롭게 수정, 사용, 배포할 수 있습니다. 단, 임의 수정 및 배포시 발생하는 문제는 수정자 본인에게 있음을 알려드립니다.",
                Font = new Font("맑은 고딕", 8F),
                ForeColor = Color.FromArgb(142, 151, 163),
                Location = new Point(24, 430),
                Size = new Size(720, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            cardPanel.Controls.Add(lblTitle);
            cardPanel.Controls.Add(lblDesc);
            cardPanel.Controls.Add(datePanel);
            cardPanel.Controls.Add(gridCommands);
            cardPanel.Controls.Add(actionPanel);
            cardPanel.Controls.Add(lblFooter);

            mainContainer.Controls.Add(cardPanel);
            this.Controls.Add(mainContainer);
        }

        private bool IsSystemInternalNoise(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return true;
            string s = cmd.Trim();

            if (s.StartsWith("#requires", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("$__cmdletization", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("TabExpansion2", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("PSReadLine", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Contains("Get-ItemProperty HKLM:", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("prompt", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("prompt\r\n") || s.StartsWith("prompt\n")) return true;
            if (s.Contains("param(") && s.Contains("$ExecutionContext")) return true;
            if (s.Contains("Set-StrictMode") && s.Contains("$global:")) return true;
            if (s.Contains("Microsoft.PowerShell.") && s.Contains("Export-ModuleMember")) return true;

            return false;
        }

        private void LoadHistoryData()
        {
            allEvents.Clear();

            try
            {
                string query = "*[System[(EventID=4104)]]";
                var logQuery = new EventLogQuery("Microsoft-Windows-PowerShell/Operational", PathType.LogName, query)
                {
                    ReverseDirection = true
                };

                using (var reader = new EventLogReader(logQuery))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            var time = record.TimeCreated ?? DateTime.Now;
                            string msg = record.FormatDescription() ?? "";

                            // 이벤트 본문 앞의 접두어 제거
                            int idx = msg.IndexOf(":\r\n", StringComparison.Ordinal);
                            if (idx < 0) idx = msg.IndexOf(":\n", StringComparison.Ordinal);
                            if (idx >= 0 && (msg.StartsWith("Scriptblock") || msg.StartsWith("스크립트 블록") || msg.StartsWith("Creating Scriptblock")))
                            {
                                msg = msg.Substring(idx + 2);
                            }

                            string cleanCmd = msg.Trim();

                            // 시스템 내부 파워쉘 스크립트 제외
                            if (IsSystemInternalNoise(cleanCmd)) continue;

                            allEvents.Add(new HistoryEntry { Time = time, FullCommand = cleanCmd });
                        }
                    }
                }
            }
            catch
            {
                LoadFallbackFile();
            }

            if (allEvents.Count == 0)
            {
                LoadFallbackFile();
            }

            FilterBySelectedDate();
        }

        private void LoadFallbackFile()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string historyPath = Path.Combine(appData, @"Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt");

            if (File.Exists(historyPath))
            {
                var fileInfo = new FileInfo(historyPath);
                var lines = File.ReadAllLines(historyPath);
                foreach (var line in lines)
                {
                    string clean = line.Trim();
                    if (!string.IsNullOrWhiteSpace(clean) && !IsSystemInternalNoise(clean))
                    {
                        allEvents.Add(new HistoryEntry { Time = fileInfo.LastWriteTime, FullCommand = clean });
                    }
                }
            }
        }

        private void FilterBySelectedDate()
        {
            DateTime target = dtPicker.Value.Date;
            filteredEvents = allEvents.FindAll(x => x.Time.Date == target);

            gridCommands.Rows.Clear();
            foreach (var item in filteredEvents)
            {
                string firstLine = item.FullCommand.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                string preview = firstLine.Length > 55 ? firstLine.Substring(0, 55) + "..." : firstLine;
                gridCommands.Rows.Add(item.Time.ToString("yyyy-MM-dd HH:mm:ss"), preview, "명령어 보기");
            }

            if (filteredEvents.Count == 0)
            {
                TossModalDialog.Show(this, "조회 결과", "선택하신 날짜에 기록된 명령어가 없습니다.");
            }
        }

        private void GridCommands_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                var selected = filteredEvents[e.RowIndex];
                using (var detailForm = new CommandDetailModal(selected.Time, selected.FullCommand))
                {
                    detailForm.ShowDialog(this);
                }
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
                    TossModalDialog.Show(this, "파일 저장 완료", "명령어 목록이 텍스트 파일로 저장되었습니다.");
                }
            }
        }
    }

    // 명령어 보기 클릭 시 나타나는 상세 팝업 창
    public class CommandDetailModal : Form
    {
        private string fullCommand;
        private DateTime cmdTime;

        public CommandDetailModal(DateTime time, string command)
        {
            this.fullCommand = command;
            this.cmdTime = time;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Size = new Size(620, 420);
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
                Height = 40
            };

            var lblTitle = new Label
            {
                Text = $"명령어 상세 보기 ({time:yyyy-MM-dd HH:mm:ss})",
                Font = new Font("맑은 고딕", 11F, FontStyle.Bold),
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
                Size = new Size(80, 32),
                Location = new Point(410, 2)
            };
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(fullCommand);
                TossModalDialog.Show(this, "복사 완료", "명령어가 클립보드에 복사되었습니다.");
            };

            var btnSave = new RoundedButton
            {
                Text = "저장하기",
                BorderRadius = 8,
                Font = new Font("맑은 고딕", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                Size = new Size(80, 32),
                Location = new Point(496, 2)
            };
            btnSave.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                    sfd.FileName = $"PS_Cmd_{cmdTime:yyyyMMdd_HHmmss}.txt";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, fullCommand, Encoding.UTF8);
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
                Text = fullCommand,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.FromArgb(249, 250, 251),
                ForeColor = Color.FromArgb(51, 61, 75),
                Dock = DockStyle.Fill
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
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
