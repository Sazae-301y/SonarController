using System.Windows;
using System.Windows.Controls;

namespace SonarController.Views;

public class DropdownComboBox : System.Windows.Controls.ComboBox
{
    protected override DependencyObject GetContainerForItemOverride() => new DropdownComboBoxItem();
}

public class DropdownComboBoxItem : ComboBoxItem
{
    private bool _isMouseEntering;

    public DropdownComboBoxItem()
    {
        RequestBringIntoView += OnRequestBringIntoView;
    }

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        _isMouseEntering = true;
        try
        {
            base.OnMouseEnter(e);
        }
        finally
        {
            _isMouseEntering = false;
        }
    }

    private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        // ComboBoxItem のホバーによるフォーカス移動だけを抑え、キー操作時は通常どおりスクロールする。
        if (_isMouseEntering && ReferenceEquals(e.TargetObject, this))
            e.Handled = true;
    }
}
