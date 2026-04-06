using System.Windows.Forms;

namespace ADSRDashboard
{
    partial class MainDashboard
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize    = new System.Drawing.Size(1920, 1080);
            this.Name          = "MainDashboard";
            this.Text          = "ADSR - Dashboard";
            this.ResumeLayout(false);
        }
    }
}
