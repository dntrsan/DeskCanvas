using System.Drawing;
using System.Windows.Forms;

namespace DeskCanvas.App.Services;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon icon;
    private readonly ToolStripMenuItem editItem;

    internal TrayService(Action open, Action add, Action toggleEdit, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("DeskCanvasを開く", null, (_, _) => open());
        menu.Items.Add("画像 / GIFを追加", null, (_, _) => add());
        editItem = new ToolStripMenuItem("編集モードをON", null, (_, _) => toggleEdit());
        menu.Items.Add(editItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => exit());

        icon = new NotifyIcon
        {
            Text = "DeskCanvas",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => open();
    }

    internal void SetEditMode(bool enabled)
    {
        editItem.Text = enabled ? "編集モードをOFF（ロック）" : "編集モードをON";
        icon.Text = enabled ? "DeskCanvas — 編集中" : "DeskCanvas — ロック中";
    }

    internal void ShowMessage(string title, string message, ToolTipIcon iconType = ToolTipIcon.Info)
    {
        icon.ShowBalloonTip(3000, title, message, iconType);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
    }
}
