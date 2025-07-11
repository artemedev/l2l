using Avalonia.Controls;
using System.ComponentModel;

namespace l2l_aggregator.Controls.Keyboard.Layout
{
    public abstract class KeyboardLayout : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _layoutName = "Default";

        public string LayoutName
        {
            get => _layoutName;
            set
            {
                if (_layoutName != value)
                {
                    _layoutName = value;
                    OnPropertyChanged(nameof(LayoutName));
                }
            }
        }
    }
}
