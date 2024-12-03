using Maroquio;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace YukiDaemon
{
    public partial class MainForm : Form
    {
        private readonly ProfileEditor nullProfileEditor;
        private readonly SortableBindingList<ProfileEditor> profileEditors = new();
        private readonly string configFilePath = "daemon-config.json";

        public MainForm()
        {
            InitializeComponent();
            //CheckAutoRun();
            nullProfileEditor = new ProfileEditor();
            foreach (Control control in nullProfileEditor.Controls)
            {
                control.Enabled = false;
            }
        }

        private void ReplaceProfileEditor(ProfileEditor? profileEditor)
        {
            MainSplitContainer.Panel2.Controls.Clear();
            MainSplitContainer.Panel2.Controls.Add(profileEditor ?? nullProfileEditor);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // ProfileEditor
            profileEditor1.Parent = null;
            profileEditor1.Dispose();
            ReplaceProfileEditor(null);
            // Config
            if (File.Exists(configFilePath))
            {
                string content = File.ReadAllText(configFilePath, Encoding.UTF8);
                Config config = JsonConvert.DeserializeObject<Config>(content) ?? throw new Exception("Reading config error.");
                foreach (Profile profile in config.Profiles)
                {
                    ProfileEditor profileEditor = new(profile);
                    profileEditors.Add(profileEditor);
                }
            }
            // ProfilesDataGridView
            ProfilesDataGridView.AutoGenerateColumns = false;
            ProfilesDataGridView.Columns.Add(new()
            {
                HeaderText = "Name",
                DataPropertyName = nameof(ProfileEditor.ProfileName),
                CellTemplate = new DataGridViewTextBoxCell(),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
            ProfilesDataGridView.Columns.Add(new()
            {
                HeaderText = "State",
                DataPropertyName = nameof(ProfileEditor.State),
                CellTemplate = new DataGridViewTextBoxCell(),
                SortMode = DataGridViewColumnSortMode.Automatic
            });
            ProfilesDataGridView.DataSource = profileEditors;
        }

        private void AddProfileButton_Click(object sender, EventArgs e)
        {
            ProfileEditor profileEditor = new("New Profile");
            profileEditors.Add(profileEditor);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                WindowState = FormWindowState.Minimized;
                e.Cancel = true;
                return;
            }
            while (profileEditors.Count > 0)
            {
                int lastIndex = profileEditors.Count - 1;
                profileEditors[lastIndex].Dispose();
                profileEditors.RemoveAt(lastIndex);
            }
            nullProfileEditor.Dispose();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            Config config = new(profileEditors.Select(profileEditor => profileEditor.Profile).ToList());
            string content = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, content, Encoding.UTF8);
        }

        private void ProfilesDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void ProfilesDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            ReplaceProfileEditor(ProfilesDataGridView.SelectedRows.Count == 1 ? (ProfileEditor)ProfilesDataGridView.SelectedRows[0].DataBoundItem : null);
            DeleteProfileButton.Enabled = ProfilesDataGridView.SelectedRows.Count > 0;
        }

        private void ProfilesDataGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            for (int i = 0; i < ProfilesDataGridView.Rows.Count; i++)
            {
                ProfilesDataGridView.Rows[i].Selected = i == e.RowIndex;
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            foreach (ProfileEditor profileEditor in profileEditors)
            {
                if (profileEditor.AutoStart)
                {
                    profileEditor.StartProcess();
                }
            }
        }

        private void DeleteProfileButton_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in ProfilesDataGridView.SelectedRows)
            {
                ProfileEditor profileEditor = (ProfileEditor)row.DataBoundItem;
                profileEditor.Dispose();
                profileEditors.Remove(profileEditor);
            }
        }

        private void QuitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            bool isAdmin = CheckIfAdmin();

            if (!isAdmin)
            {
                // 如果不是管理员，使用提升权限启动当前应用程序
                RestartElevated();
                return;
            }
            AutoRun();
        }

        /// <summary>
        /// 检查是否开机启动，并设置控件状态
        /// </summary>
        private void CheckAutoRun()
        {
            string strFilePath = Application.ExecutablePath;
            string strFileName = Path.GetFileName(strFilePath);

            if (SystemHelper.IsAutoRun(strFilePath, strFileName))
            {
                toolStripMenuItem1.Checked = true;
            }
            else
            {
                toolStripMenuItem1.Checked = false;
            }
        }

        private void AutoRun()
        {
            //toolStripMenuItem1.Checked = !toolStripMenuItem1.Checked;
            string strFilePath = Application.ExecutablePath;
            string strFileName = Path.GetFileName(strFilePath);
            string dirName = Path.GetDirectoryName(strFilePath);
            SystemHelper.SetAutoRun(strFilePath, strFileName, toolStripMenuItem1.Checked);

        }

        private static bool CheckIfAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartElevated()
        {
            // 获取当前执行的exe文件路径
            string exeName = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // 启动新的进程，以管理员权限运行
            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.UseShellExecute = true;
            processStartInfo.Verb = "runas";
            processStartInfo.FileName = exeName;

            try
            {
                Process.Start(processStartInfo);
                // 当前进程退出
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("提升权限失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SettingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CheckAutoRun();
        }

        private void toolStripMenuItem1_CheckedChanged(object sender, EventArgs e)
        {
            bool isAdmin = CheckIfAdmin();

            if (!isAdmin)
            {
                // 如果不是管理员，使用提升权限启动当前应用程序
                RestartElevated();
                return;
            }
            AutoRun();
        }

    }
}
