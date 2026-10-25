using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EarTrumpet.UI.Controls
{
    public class VolumeSlider : Slider
    {
        public float PeakValue1
        {
            get { return (float)this.GetValue(PeakValue1Property); }
            set { this.SetValue(PeakValue1Property, value); }
        }
        public static readonly DependencyProperty PeakValue1Property = DependencyProperty.Register(
          "PeakValue1", typeof(float), typeof(VolumeSlider), new PropertyMetadata(0f, new PropertyChangedCallback(PeakValueChanged)));

        public float PeakValue2
        {
            get { return (float)this.GetValue(PeakValue2Property); }
            set { this.SetValue(PeakValue2Property, value); }
        }
        public static readonly DependencyProperty PeakValue2Property = DependencyProperty.Register(
          "PeakValue2", typeof(float), typeof(VolumeSlider), new PropertyMetadata(0f, new PropertyChangedCallback(PeakValueChanged)));

        private Border _peakMeter1;
        private Border _peakMeter2;
        private Thumb _thumb;
        private Point _lastMousePosition;

        public VolumeSlider() : base()
        {
            PreviewTouchDown += OnTouchDown;
            PreviewMouseDown += OnMouseDown;
            TouchUp += OnTouchUp;
            MouseUp += OnMouseUp;
            TouchMove += OnTouchMove;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            Loaded += OnLoaded;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            // The device row is collapsed until a playback device exists. Loaded then runs
            // before this slider is measured, so a lookup there leaves the thumb null.
            CacheTemplateParts();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CacheTemplateParts();
        }

        private void CacheTemplateParts()
        {
            _thumb = GetTemplateChild("SliderThumb") as Thumb;
            _peakMeter1 = GetTemplateChild("PeakMeter1") as Border;
            _peakMeter2 = GetTemplateChild("PeakMeter2") as Border;
        }

        private void EnsureTemplateParts()
        {
            if (_thumb != null)
            {
                return;
            }

            ApplyTemplate();
            CacheTemplateParts();
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var ret = base.ArrangeOverride(arrangeBounds);
            SizeOrVolumeOrPeakValueChanged();
            return ret;
        }

        private static void PeakValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((VolumeSlider)d).SizeOrVolumeOrPeakValueChanged();
        }

        private void SizeOrVolumeOrPeakValueChanged()
        {
            if (_thumb == null || (_peakMeter1 == null && _peakMeter2 == null))
            {
                return;
            }

            var thumbWidth = _thumb.ActualWidth;
            if (_peakMeter1 != null)
            {
                _peakMeter1.Width = Math.Max(0, (ActualWidth - thumbWidth) * PeakValue1 * (Value / 100f));
            }

            if (_peakMeter2 != null)
            {
                _peakMeter2.Width = Math.Max(0, (ActualWidth - thumbWidth) * PeakValue2 * (Value / 100f));
            }
        }

        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (sender is FrameworkElement element)
            {
                VisualStateManager.GoToState(element, "Pressed", true);
            }

            var touchPoint = e.TouchDevice == null ? new Point() : e.GetTouchPoint(this).Position;
            SetPositionByControlPoint(touchPoint);
            if (e.TouchDevice != null)
            {
                CaptureTouch(e.TouchDevice);
            }

            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            EnsureTemplateParts();
            _lastMousePosition = e.GetPosition(this);
            if (sender is FrameworkElement element)
            {
                VisualStateManager.GoToState(element, "Pressed", true);
            }

            if (_thumb == null || !_thumb.IsMouseOver)
            {
                SetPositionByControlPoint(_lastMousePosition);
            }

            CaptureMouse();
            e.Handled = true;
        }

        private void OnTouchUp(object sender, TouchEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (sender is FrameworkElement element)
            {
                VisualStateManager.GoToState(element, "Normal", true);
            }

            if (e.TouchDevice != null)
            {
                ReleaseTouchCapture(e.TouchDevice);
            }

            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e == null || !IsMouseCaptured)
            {
                return;
            }

            // If the point is outside of the control, clear the hover state.
            Rect rcSlider = new Rect(0, 0, ActualWidth, ActualHeight);
            if (!rcSlider.Contains(e.GetPosition(this)) && sender is FrameworkElement element)
            {
                VisualStateManager.GoToState(element, "Normal", true);
            }

            ReleaseMouseCapture();
            e.Handled = true;
        }

        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            if (e == null || !AreAnyTouchesCaptured || e.TouchDevice == null)
            {
                return;
            }

            SetPositionByControlPoint(e.GetTouchPoint(this).Position);
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            var mousePosition = e.GetPosition(this);
            if (IsMouseCaptured && mousePosition != _lastMousePosition)
            {
                _lastMousePosition = mousePosition;
                SetPositionByControlPoint(mousePosition);
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            var amount = Math.Sign(e.Delta) * 2.0;
            ChangePositionByAmount(amount);
            e.Handled = true;
        }

        public void SetPositionByControlPoint(Point point)
        {
            var percent = point.X / ActualWidth;
            Value = Bound((Maximum - Minimum) * percent);
        }

        public void ChangePositionByAmount(double amount)
        {
            Value = Bound(Value + amount);
        }

        public double Bound(double val)
        {
            return Math.Max(Minimum, Math.Min(Maximum, val));
        }
    }
}
