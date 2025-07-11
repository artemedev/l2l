using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace l2l_aggregator.ViewModels.VisualElements
{
    public partial class CameraViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _cameraIP;

        [ObservableProperty]
        private string _selectedCameraModel;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _id;

        public CameraViewModel()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
