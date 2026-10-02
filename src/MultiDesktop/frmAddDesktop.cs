using I18N.DotNet;
using MultiDesktop.Core;

namespace MultiDesktop
{
    public partial class frmAddDesktop : Form
    {
        public frmAddDesktop()
        {
            InitializeComponent();
            
        }

        private void btnAddDesktop_Click(object sender, EventArgs e)
        {
            string wallpaperStyle = cboWallpaperStyle.SelectedItem?.ToString() ?? DesktopRepository.DefaultWallpaperStyle;

            // 全部校验与持久化由 Core 的 DesktopService 负责，失败文案与原先逐字一致
            var result = DesktopService.AddDesktop(
                txtDesktopName.Text, txtDesktopPath.Text,
                chkEnableWallpaper.Checked, txtWallpaperPath.Text,
                wallpaperStyle, EncryptManager.IsEncrypted,
                DesktopEditState.IsEdit, DesktopEditState.IndexToChange,
                // 编辑一个已加密的桌面时，其明文文件夹已被加密删除，路径不存在属正常
                allowMissingPath: EncryptManager.IsEncrypted);

            if (result.Success)
            {
                Close();
            }
            else
            {
                MessageBox.Show(result.Message);
            }
        }

        private void btnShowFolderBrowseDialog_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                txtDesktopPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btnBrowseWallpaper_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                txtWallpaperPath.Text = openFileDialog1.FileName;
            }
        }

        private void chkEnableWallpaper_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkEnableWallpaper.Checked;
            txtWallpaperPath.Enabled = enabled;
            btnBrowseWallpaper.Enabled = enabled;
            cboWallpaperStyle.Enabled = enabled;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void frmAddDesktop_Load(object sender, EventArgs e)
        {
            // I18N 国际化
            this.Text = GlobalLocalizer.Localize(this.Text);
            label1.Text = GlobalLocalizer.Localize(label1.Text);
            label2.Text = GlobalLocalizer.Localize(label2.Text);
            label3.Text = GlobalLocalizer.Localize(label3.Text);
            label4.Text = GlobalLocalizer.Localize(label4.Text);
            chkEnableWallpaper.Text = GlobalLocalizer.Localize(chkEnableWallpaper.Text);
            btnAddDesktop.Text = GlobalLocalizer.Localize(btnAddDesktop.Text);
            btnClose.Text = GlobalLocalizer.Localize(btnClose.Text);

            txtDesktopName.Text = DesktopEditState.t_DesktopName;
            txtDesktopPath.Text = DesktopEditState.t_DesktopPath;

            // 新增桌面时重置加密状态，避免上次编辑残留
            if (!DesktopEditState.IsEdit)
                EncryptManager.Reset();

            // 默认选中"填充"
            cboWallpaperStyle.SelectedIndex = 0;

            // 编辑模式：加载已有壁纸设置（由 Core 提供只读视图，不直接读 DataRow）
            if (DesktopEditState.IsEdit)
            {
                var info = DesktopService.GetByRowIndex(DesktopEditState.IndexToChange);
                if (info != null)
                {
                    chkEnableWallpaper.Checked = info.EnableWallpaper;
                    txtWallpaperPath.Text = info.WallpaperPath;
                    int idx = cboWallpaperStyle.Items.IndexOf(info.WallpaperStyle);
                    cboWallpaperStyle.SelectedIndex = idx >= 0 ? idx : 0;
                }
            }

            // 初始化控件启用状态
            txtWallpaperPath.Enabled = chkEnableWallpaper.Checked;
            btnBrowseWallpaper.Enabled = chkEnableWallpaper.Checked;
            cboWallpaperStyle.Enabled = chkEnableWallpaper.Checked;
        }

        private void frmAddDesktop_FormClosing(object sender, FormClosingEventArgs e)
        {
            DesktopEditState.Reset();
            EncryptManager.Reset();
        }

        private void btnSetPassword_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDesktopName.Text))
            {
                MessageBox.Show("请先填写桌面名称");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtDesktopPath.Text))
            {
                MessageBox.Show("请先设置桌面路径");
                return;
            }
            // 通过 static 中介向密码窗口传递参数（沿用原方案，不使用委托）
            EncryptManager.DesktopName = txtDesktopName.Text;
            EncryptManager.DesktopFolder = txtDesktopPath.Text;
            EncryptManager.DesktopID = EncryptManager.GetZipId(EncryptManager.DesktopName);
            frmPassword frmPassword = new frmPassword();
            frmPassword.ShowDialog();
        }
    }
}
