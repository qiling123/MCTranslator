using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;

namespace MCTranslator
{
    public class RoundedButton : Button
    {
        private int _cornerRadius = 10;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get => _cornerRadius; set => _cornerRadius = value; }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = this.ClientRectangle;
            using (var path = new GraphicsPath())
            {
                int d = CornerRadius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                this.Region = new Region(path);

                // 背景
                using (var brush = new SolidBrush(this.BackColor))
                {
                    pevent.Graphics.FillPath(brush, path);
                }

                // 文本
                TextRenderer.DrawText(pevent.Graphics, this.Text, this.Font, rect, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
