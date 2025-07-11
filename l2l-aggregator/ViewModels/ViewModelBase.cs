using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace l2l_aggregator.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private double windowWidth;

        [ObservableProperty]
        private double windowHeight;

        public double DynamicFontSize => Math.Clamp(WindowWidth * 0.02, 12, 48); 
        public Thickness DynamicPadding => new Thickness(Math.Clamp(WindowWidth * 0.025, 16, 48));

        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Освобождение управляемого состояния (управляемые объекты)
                    // Производные классы должны переопределить этот метод для освобождения ресурсов
                }

                // Освобождение неуправляемых ресурсов
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Не следует модифицировать, а всю логику освобождения ресурсов нужно размещать в перегруженном методе Dispose(bool disposing)
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
