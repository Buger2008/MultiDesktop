using I18N.DotNet;
using MultiDesktop.Core;
using System.Data;
using PostQuantum.FileEncryption;
using static I18N.DotNet.Localizer;

namespace MultiDesktop
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
            // 桌面配置的加载（含旧版 XML 列补全与主键建立）统一由 Core 的 DesktopRepository 负责，
            // 不再在窗体里重复建表/建列
            tblDesktopList.DataSource = DesktopRepository.Table;
        }

        private void btnAddDesktop_Click(object sender, EventArgs e)
        {
            frmAddDesktop frmAddDesktop = new();
            frmAddDesktop.ShowDialog();
            tblDesktopList.Refresh();
            DesktopManager.IndexToChange = DesktopManager.DesktopList.Rows.Count;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            // I18N 国际化
            this.Text = GlobalLocalizer.Localize(this.Text);
            label1.Text = GlobalLocalizer.Localize(label1.Text);
            btnAddDesktop.Text = GlobalLocalizer.Localize(btnAddDesktop.Text);
            btnDeleteDesktop.Text = GlobalLocalizer.Localize(btnDeleteDesktop.Text);
            btnChangeDesktop.Text = GlobalLocalizer.Localize(btnChangeDesktop.Text);
            btnEditDesktop.Text = GlobalLocalizer.Localize(btnEditDesktop.Text);
            btnSet.Text = GlobalLocalizer.Localize(btnSet.Text);
            btnAbout.Text = GlobalLocalizer.Localize(btnAbout.Text);
            notifyIcon1.Text = GlobalLocalizer.Localize(notifyIcon1.Text);
            itmDesktopList.Text = GlobalLocalizer.Localize(itmDesktopList.Text);
            itmSettingsMenu.Text = GlobalLocalizer.Localize(itmSettingsMenu.Text);
            itmAboutMenu.Text = GlobalLocalizer.Localize(itmAboutMenu.Text);
            itmExit.Text = GlobalLocalizer.Localize(itmExit.Text);

            tblDesktopList.DataSource = DesktopManager.DesktopList;
            tblDesktopList.Refresh();
            notifyIcon1.Visible = true;

            // 记录当前正在使用的桌面（用于离开加密桌面时自动重新加密）
            var current = DesktopService.DetectCurrentDesktop();
            if (current != null)
                DesktopManager.SetCurrentDesktop(current.Name, current.Path, current.Encrypted);

            int DesktopIndex = 0;
            foreach (DataRow desktopnames in DesktopManager.DesktopList.Rows)
            {
                string? desktopname = desktopnames[0].ToString();
                ToolStripMenuItem menuItem = new ToolStripMenuItem(desktopname);
                menuItem.Tag = DesktopIndex;
                itmDesktopList.DropDownItems.Add(menuItem);
                DesktopIndex++;// 存储索引
            }
        }

        private void btnDeleteDesktop_Click(object sender, EventArgs e)
        {
            if (tblDesktopList.SelectedIndexs.Length == 0) return;

            // 选中索引含表头偏移（从 1 开始），换算为 DataTable 行索引后交给 Core 删除
            var result = DesktopService.DeleteByRowIndexes(
                tblDesktopList.SelectedIndexs.Select(i => i - 1));

            if (!result.Success)
                MessageBox.Show(result.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

            tblDesktopList.Refresh();
        }

        private void btnEditDesktop_Click(object sender, EventArgs e)
        {
            int idx = tblDesktopList.SelectedIndex - 1;
            if (idx < 0 || idx >= DesktopManager.DesktopList.Rows.Count) return;
            var row = DesktopManager.DesktopList.Rows[idx];
            DesktopManager.t_DesktopName = DesktopManager.GetString(row, 0);
            DesktopManager.t_DesktopPath = DesktopManager.GetString(row, 1);
            DesktopManager.IsEdit = true;
            DesktopManager.IndexToChange = idx;
            EncryptManager.IsEncrypted = DesktopManager.GetBool(row, 5);
            frmAddDesktop frmAddDesktop = new();
            frmAddDesktop.ShowDialog();
            tblDesktopList.Refresh();
        }

        private void tblDesktopList_CellClick(object sender, AntdUI.TableClickEventArgs e)
        {
            if (tblDesktopList.SelectedIndexs.Length > 0)
            {
                btnDeleteDesktop.Enabled = true;
            }
            else
            {
                btnDeleteDesktop.Enabled = false;

            }
            if (tblDesktopList.SelectedIndexs.Length == 1)
            {
                btnChangeDesktop.Enabled = true;
                btnEditDesktop.Enabled = true;
            }
            else
            {
                btnChangeDesktop.Enabled = false;
                btnEditDesktop.Enabled = false;
            }
        }

        private async void btnChangeDesktop_Click(object sender, EventArgs e)
        {
            int idx = tblDesktopList.SelectedIndex - 1;
            if (idx < 0 || idx >= DesktopManager.DesktopList.Rows.Count) return;
            await SwitchToDesktopAsync(DesktopManager.DesktopList.Rows[idx]);
        }

        /// <summary>
        /// 切换桌面。窗体只负责弹窗收集密码，
        /// 解锁、切换目录与壁纸、离开加密桌面的重新加密等业务步骤全部由
        /// Core 的 DesktopSwitchService 完成（与 CLI 的 switch 命令共用同一实现）。
        /// </summary>
        private async Task SwitchToDesktopAsync(DataRow row)
        {
            var target = DesktopService.FromRow(row);
            if (target == null || string.IsNullOrEmpty(target.Path)) return;

            var req = DesktopSwitchService.GetSwitchRequirements(target);

            // 1. 离开的加密桌面若已解密（存在明文文件夹），先取得其密码（切换前询问，避免切换后困惑）
            string? leavingPw = null;
            if (req.LeavingNeedsPassword)
            {
                leavingPw = EncryptManager.GetSessionPassword(req.LeavingName);
                if (leavingPw == null)
                {
                    // 会话内无缓存密码：弹窗输入，并通过实际解密验证（密码错误会重新弹窗）
                    while (true)
                    {
                        using var frm = new frmInputPassword { PromptText = $"请输入桌面“{req.LeavingName}”的密码以重新加密" };
                        if (frm.ShowDialog() != DialogResult.OK)
                            return; // 用户取消，不切换
                        leavingPw = EncryptManager.Password;
                        try
                        {
                            await EncryptionService.VerifyPasswordByDecryptAsync(req.LeavingId, leavingPw!);
                            EncryptionService.SetSessionPassword(req.LeavingName, leavingPw!);
                            break;
                        }
                        catch (PqDecryptionException)
                        {
                            MessageBox.Show("密码错误，请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }

            // 2. 执行切换：目标加密时弹窗输入密码；密码错误由 Core 以 BadPassword 回报后重新弹窗
            while (true)
            {
                string? targetPw = null;
                if (req.TargetNeedsPassword)
                {
                    using var frm = new frmInputPassword { PromptText = $"请输入桌面“{target.Name}”的密码" };
                    if (frm.ShowDialog() != DialogResult.OK)
                        return; // 用户取消
                    targetPw = EncryptManager.Password;
                }

                var outcome = await DesktopSwitchService.SwitchToAsync(target, req, targetPw, leavingPw);

                if (outcome.BadPassword)
                {
                    MessageBox.Show("密码错误，请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                if (!outcome.Success)
                {
                    MessageBox.Show(outcome.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (outcome.Warning != null)
                {
                    MessageBox.Show(outcome.Warning, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            frmSet set = new frmSet();
            set.ShowDialog();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            if (SettingsService.GetNum(SettingsService.KeyExitMode) == 0)
            {
                frmClose close = new frmClose();
                close.ShowDialog();
            }
            // 关闭确认窗口可能刚刚改写了 ExitMode，这里重新读取
            if (Program.IsMinWindow || SettingsService.GetNum(SettingsService.KeyExitMode) == 1)
            {
                Hide();
            }
            else
            {
                Environment.Exit(0);
            }

        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
        }

        private void itmExit_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private async void itmDesktopList_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var clickedItem = e.ClickedItem as ToolStripMenuItem;

            if (clickedItem is not null && clickedItem.Tag is int index)
            {
                if (index < 0 || index >= DesktopManager.DesktopList.Rows.Count) return;
                await SwitchToDesktopAsync(DesktopManager.DesktopList.Rows[index]);
            }
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            frmAbout about = new frmAbout();
            about.Show();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (itmDesktopList.DropDownItems.Count == 0) { itmDesktopList.Enabled = false; }
            else { itmDesktopList.Enabled = true;}
            itmDesktopList.DropDownItems.Clear();
            int DesktopIndex = 0;
            foreach (DataRow desktopnames in DesktopManager.DesktopList.Rows)
            {
                string? desktopname = desktopnames[0].ToString();
                ToolStripMenuItem menuItem = new ToolStripMenuItem(desktopname);
                menuItem.Tag = DesktopIndex;
                itmDesktopList.DropDownItems.Add(menuItem);
                DesktopIndex++;// 存储索引
            }
        }
    }
}
