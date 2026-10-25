using EarTrumpet.UI.Helpers;
using System;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    public sealed class SoundOutputDeviceViewModel : BindableBase
    {
        public DeviceViewModel Device { get; }
        public string DisplayName => Device.DisplayName;
        public string Glyph => GlyphFor(Device);
        public ICommand Select { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    RaisePropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isSelected;

        public SoundOutputDeviceViewModel(DeviceViewModel device, Action select, bool isSelected)
        {
            Device = device;
            _isSelected = isSelected;
            Select = new RelayCommand(select);
            Device.PropertyChanged += OnDevicePropertyChanged;
        }

        public void Detach()
        {
            Device.PropertyChanged -= OnDevicePropertyChanged;
        }

        public static string GlyphFor(DeviceViewModel device)
        {
            if (device == null)
            {
                return "\uE7F5";
            }

            if (device.IsMuted)
            {
                return "\uE74F";
            }

            var text = ((device.DisplayName ?? string.Empty) + " " +
                        (device.InterfaceName ?? string.Empty) + " " +
                        (device.DeviceDescription ?? string.Empty)).ToLowerInvariant();

            if (text.Contains("headphone") ||
                text.Contains("headset") ||
                text.Contains("kuuloke") ||
                text.Contains("earbud") ||
                text.Contains("airpod") ||
                text.Contains("hands-free"))
            {
                return "\uE7F6";
            }

            return "\uE7F5";
        }

        private void OnDevicePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Device.DisplayName))
            {
                RaisePropertyChanged(nameof(DisplayName));
                RaisePropertyChanged(nameof(Glyph));
            }
            else if (e.PropertyName == nameof(Device.IsMuted) || e.PropertyName == nameof(Device.Volume))
            {
                RaisePropertyChanged(nameof(Glyph));
            }
        }
    }
}
