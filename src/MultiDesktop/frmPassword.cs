using MultiDesktop.Core;

namespace MultiDesktop
{
    public partial class frmPassword : Form
    {
        public frmPassword()
        {
            InitializeComponent();
            this.AcceptButton = btnOK;
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    Close();
            };
        }

        private void frmPassword_Load(object sender, EventArgs e)
        {
            // 首次设置密码时不显示“原密码”一栏，其余控件整体上移
            bool hasOld = EncryptionService.HasEncryptedFile(EncryptManager.DesktopID);
            label1.Visible = hasOld;
            txtOldPassword.Visible = hasOld;
            if (!hasOld)
            {
                int shift = txtOldPassword.Height + 34;
                txtNewPassword.Location = new Point(txtNewPassword.Location.X, txtNewPassword.Location.Y - shift);
                txtNewPasswordAgain.Location = new Point(txtNewPasswordAgain.Location.X, txtNewPasswordAgain.Location.Y - shift);
                label2.Location = new Point(label2.Location.X, label2.Location.Y - shift);
                label3.Location = new Point(label3.Location.X, label3.Location.Y - shift);
            }
        }

        private async void btnOK_Click(object sender, EventArgs e)
        {
            // 判定顺序、加解密与全部提示文案都由 Core 的 PasswordService 负责，
            // 窗体只负责收集输入、显示结果并更新窗体间传值用的 static 中介状态
            var result = await PasswordService.ApplyAsync(
                EncryptManager.DesktopName,
                EncryptManager.DesktopFolder,
                EncryptManager.DesktopID,
                txtOldPassword.Text,
                txtNewPassword.Text,
                txtNewPasswordAgain.Text);

            if (!result.Success)
            {
                MessageBox.Show(result.Message, result.Title, MessageBoxButtons.OK,
                    result.IsError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
                return; // 失败时保持窗口打开
            }

            if (result.Data is PasswordState state)
            {
                EncryptManager.IsEncrypted = state.Encrypted;
                EncryptManager.Password = state.SessionPassword;
                if (state.SessionPassword != null)
                    EncryptionService.SetSessionPassword(EncryptManager.DesktopName, state.SessionPassword);
            }

            MessageBox.Show(result.Message, result.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}
