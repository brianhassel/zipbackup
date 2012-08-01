using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BrianHassel.ZipBackup {
    public partial class PasswordSettingsDialog : Form {
        public PasswordSettingsDialog() {
            InitializeComponent();
        }

        public string ArchivePassword {
            get { return txtArchivePassword.Text; }
        }

        public string FTPPassword {
            get { return txtFTPPassword.Text; }
        }

        public string EmailPassword {
            get { return txtEmailPassword.Text; }
        }

        private void btnOK_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e) {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
