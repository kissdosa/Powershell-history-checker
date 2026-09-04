using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
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

            // 1. 실행 중 대기 팝업 표시
            using (var splash = new SplashForm())
            {
                splash.Show();
                splash.Refresh();
                
                System.Threading.Thread.Sleep(1200);

                var mainForm = new MainForm();
                splash.Close();
                Application.Run(mainForm);
            }
        }
    }

    // 대기 안내 팝업 창
    public class SplashForm : Form
    {
        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(380, 110);
            this.BackColor = Color.FromArgb(242, 244, 246);
            this.TopMost = true;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
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

    // 메인 UI 창
    public class MainForm : Form
    {
        private DateTimePicker dtPicker = null!;
        private Button btnConfirm = null!;
        private Button btnCopy = null!;
        private Button btnSave = null!;
        private TextBox txtLog = null!;
        private List<HistoryEntry> allEvents = new();

        public MainForm()
        {
            this.Text = "Powershell 입력 명령어 체크리스트";
            this.Size = new Size(680, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(242, 244, 246);
            this.Font = new Font("맑은 고딕", 9.5F);

            InitializeTossUI();
            LoadHistoryData();
        }

        private void InitializeTossUI()
        {
            var cardPanel = new Panel
            {
                BackColor = Color.White,
                Location = new Point(24, 24),
                Size = new Size(616, 432),
                Padding = new Padding(24)
            };

            var lblTitle = new Label
            {
                Text = "Powershell 입력 명령어 체크리스트",
                Font = new Font("맑은 고딕", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 31, 40),
                Location = new Point(24, 20),
                AutoSize = true
            };

            var lblDesc = new Label
            {
                Text = "- 본 프로그램은 입력하신 MDP 진단 관련하여 Powershell 명령어 취합 하는 프로그램입니다. 날짜를 선택후 확인 버튼을 눌러주세요.\r\n" +
                       "- 명령어 입력후 나오는 결과값은 수집되지 않고 단순 명령어만 수집하니 참고 부탁드립니다.",
                Font = new Font("맑은 고딕", 9F),
                ForeColor = Color.FromArgb(107, 118, 132),
                Location = new Point(24, 55),
                Size = new Size(568, 45)
            };

            var datePanel = new Panel
            {
                Location = new Point(24, 110),
                Size = new Size(568, 38)
            };

            dtPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy년 MM월 dd일",
                Font = new Font("맑은 고딕", 10F),
                Size = new Size(160, 32),
                Location = new Point(0, 3)
            };

            btnConfirm = new Button
            {
                Text = "확인",
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 32),
                Location = new Point(170, 3),
                Cursor = Cursors.Hand
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, e) => FilterBySelectedDate();

            datePanel.Controls.Add(dtPicker);
            datePanel.Controls.Add(btnConfirm);

            txtLog = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = false,
                ReadOnly = true,
                BackColor = Color.FromArgb(249, 250, 251),
                ForeColor = Color.FromArgb(51, 61, 75),
                Font = new Font("Consolas", 9.5F),
                Location = new Point(24, 160),
                Size = new Size(568, 115)
            };

            btnCopy = new Button
            {
                Text = "복사하기",
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 89, 104),
                BackColor = Color.FromArgb(242, 244, 246),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Location = new Point(382, 370),
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtLog.Text))
                {
                    Clipboard.SetText(txtLog.Text);
                    MessageBox.Show("클립보드에 복사되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnSave = new Button
            {
                Text = "TXT 저장",
                Font = new Font("맑은 고딕", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(49, 130, 246),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Location = new Point(492, 370),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveToFile();

            cardPanel.Controls.AddRange(new Control[] { lblTitle, lblDesc, datePanel, txtLog, btnCopy, btnSave });
            this.Controls.Add(cardPanel);
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

                            string cleanCmd = msg.Trim().Replace("\r\n", " ").Replace("\n", " ");
                            if (!string.IsNullOrWhiteSpace(cleanCmd))
                            {
                                allEvents.Add(new HistoryEntry { Time = time, Command = cleanCmd });
                            }
                        }
                    }
                }
            }
            catch
            {
                LoadFallbackHistoryFile();
            }

            FilterBySelectedDate();
        }

        private void LoadFallbackHistoryFile()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string historyPath = Path.Combine(appData, @"Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt");

            if (File.Exists(historyPath))
            {
                var fileInfo = new FileInfo(historyPath);
                var lines = File.ReadAllLines(historyPath);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        allEvents.Add(new HistoryEntry { Time = fileInfo.LastWriteTime, Command = line.Trim() });
                    }
                }
            }
        }

        private void FilterBySelectedDate()
        {
            DateTime targetDate = dtPicker.Value.Date;
            var filtered = allEvents.FindAll(x => x.Time.Date == targetDate);

            var sb = new StringBuilder();
            foreach (var item in filtered)
            {
                sb.AppendLine($"{item.Time:yyyy-MM-dd HH:mm:ss} | {item.Command}");
            }

            txtLog.Text = sb.Length > 0 ? sb.ToString() : "해당 날짜에 기록된 명령어가 없습니다.";
        }

        private void SaveToFile()
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text)) return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "텍스트 파일 (*.txt)|*.txt";
                sfd.FileName = $"PS_History_{dtPicker.Value:yyyyMMdd}.txt";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text, Encoding.UTF8);
                    MessageBox.Show("파일 저장이 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }

    public class HistoryEntry
    {
        public DateTime Time { get; set; }
        public string Command { get; set; } = string.Empty;
    }
}
