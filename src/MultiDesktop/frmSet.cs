using I18N.DotNet;
using MultiDesktop.Core;
using System.Data;
using System.Linq;
using static I18N.DotNet.Localizer;

namespace MultiDesktop
{
    public partial class frmSet : Form
    {

        public frmSet()
        {
            InitializeComponent();
        }

        private void drpColorMode_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            drpColorMode.Text = drpColorMode.SelectedValue?.ToString();
            ColorModeMap.Map.TryGetValue(drpColorMode.Text, out SystemColorMode systemColorMode);
            Application.SetColorMode(systemColorMode);
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            // 写入与落盘由 Core 的 SettingsService 负责（批量更新只落盘一次）
            SettingsService.Update(
                colorNum: SettingsService.GetColorNum(drpColorMode.Text),
                exitModeNum: SettingsService.GetExitModeNum(drpExitMode.Text));
            Close();
        }

        private void frmSet_Load(object sender, EventArgs e)
        {
            // I18N 国际化
            this.Text = GlobalLocalizer.Localize(this.Text);
            label1.Text = GlobalLocalizer.Localize(label1.Text);
            label2.Text = GlobalLocalizer.Localize(label2.Text);
            label3.Text = GlobalLocalizer.Localize(label3.Text);
            btnSaveSettings.Text = GlobalLocalizer.Localize(btnSaveSettings.Text);
            btnClose.Text = GlobalLocalizer.Localize(btnClose.Text);
            button1.Text = GlobalLocalizer.Localize(button1.Text);

            drpColorMode.Text = SettingsService.GetColorName(SettingsService.GetNum(SettingsService.KeyColor));
            drpExitMode.Text = SettingsService.GetExitModeName(SettingsService.GetNum(SettingsService.KeyExitMode));
            
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();

        }

        private void btnInstallSkills_Click(object sender, EventArgs e)
        {
            // 安装逻辑（写 SKILL.md + 把程序目录加入用户 PATH）由 Core 的 SkillInstaller 负责
            var result = SkillInstaller.Install();
            MessageBox.Show(result.Message, result.Title, MessageBoxButtons.OK,
                result.IsError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }

        private void drpExitMode_SelectedValueChanged(object sender, AntdUI.ObjectNEventArgs e)
        {
            drpExitMode.Text = drpExitMode.SelectedValue?.ToString();
        }
    }
}
