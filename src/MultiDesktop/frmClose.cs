using I18N.DotNet;
using MultiDesktop.Core;
using static I18N.DotNet.Localizer;

namespace MultiDesktop
{
    public partial class frmClose : Form
    {
        public frmClose()
        {
            InitializeComponent();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            if (checkbox1.Checked)
            {
                // “不再询问”时记住退出程序
                SettingsService.Update(exitModeNum: SettingsService.GetExitModeNum(SettingsService.ExitQuit));
            }
            Environment.Exit(0);
        }

        private void btnMin_Click(object sender, EventArgs e)
        {
            Program.IsMinWindow = true;
            Close();
            if (checkbox1.Checked)
            {
                // “不再询问”时记住最小化到后台
                SettingsService.Update(exitModeNum: SettingsService.GetExitModeNum(SettingsService.ExitMinimize));
            }
        }

        private void frmClose_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void frmClose_Load(object sender, EventArgs e)
        {
            // I18N 国际化
            this.Text = GlobalLocalizer.Localize(this.Text);
            label1.Text = GlobalLocalizer.Localize(label1.Text);
            btnExit.Text = GlobalLocalizer.Localize(btnExit.Text);
            btnMin.Text = GlobalLocalizer.Localize(btnMin.Text);
            checkbox1.Text = GlobalLocalizer.Localize(checkbox1.Text);
        }
    }
}
