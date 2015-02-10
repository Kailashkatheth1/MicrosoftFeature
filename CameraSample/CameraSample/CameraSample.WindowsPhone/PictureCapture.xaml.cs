﻿using Feature;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkID=390556 上有介绍

namespace CameraSample
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public partial class PictureCapture : Page
    {
        #region Field
        private MediaCapture mediaCapture = null;
        private ZoomControl zoomControl = null;
        private const string conCameraPanel = "cameraPanel";
        private const string conCameraBack = "Back";
        private const string conCameraFront = "Front";

        #endregion

        #region Constructor
        public PictureCapture()
        {
            this.InitializeComponent();
        }

        #endregion

        /// <summary>
        /// 在此页将要在 Frame 中显示时进行调用。
        /// </summary>
        /// <param name="e">描述如何访问此页的事件数据。
        /// 此参数通常用于配置页。</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            HardwareButtons.CameraHalfPressed -= HardwareButtonsOnCameraHalfPressed;
            HardwareButtons.CameraPressed -= HardwareButtonsOnCameraPressed;
            HardwareButtons.CameraHalfPressed += HardwareButtonsOnCameraHalfPressed;
            HardwareButtons.CameraPressed += HardwareButtonsOnCameraPressed;
            await this.StartCapturePreviewAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.CameraHalfPressed -= HardwareButtonsOnCameraHalfPressed;
            HardwareButtons.CameraPressed -= HardwareButtonsOnCameraPressed;
            (App.Current as App).CleanUpCapture();
        }

        private void AppbarFlashAuto_Click(object sender, RoutedEventArgs e)
        {
            if (this.mediaCapture != null && this.mediaCapture.VideoDeviceController.FlashControl.Supported)
            {
                FlashControl flashControl = this.mediaCapture.VideoDeviceController.FlashControl;
                if (flashControl.Auto)
                {
                    flashControl.Auto = false;
                    flashControl.Enabled = true;
                    var icon = new BitmapIcon();
                    icon.UriSource = new Uri("ms-appx:///Assets/appbar.camera.flash.png");
                    this.AppbarFlashAuto.Icon = icon;
                }
                else if (flashControl.Enabled)
                {
                    flashControl.Enabled = false;
                    var icon = new BitmapIcon();
                    icon.UriSource = new Uri("ms-appx:///Assets/appbar.camera.flash.off.png");
                    this.AppbarFlashAuto.Icon = icon;
                }
                else
                {
                    flashControl.Auto = true;
                    flashControl.Enabled = true;
                    var icon = new BitmapIcon();
                    icon.UriSource = new Uri("ms-appx:///Assets/appbar.camera.flash.auto.png");
                    this.AppbarFlashAuto.Icon = icon;
                }
            }
        }

        private async void AppbarSwitchCamera_Click(object sender, RoutedEventArgs e)
        {
            if (this.mediaCapture != null)
            {
                (App.Current as App).CleanUpCapture();
                var obj = Global.Current.LocalSettings.LoadData(conCameraPanel);
                if (obj != null)
                {
                    var devicePanel = obj.ToString();
                    if (devicePanel == conCameraBack)
                    {
                        Global.Current.LocalSettings.SaveData(conCameraPanel, conCameraFront);
                        this.captureElement.FlowDirection = Windows.UI.Xaml.FlowDirection.RightToLeft;
                    }
                    else
                    {
                        Global.Current.LocalSettings.SaveData(conCameraPanel, conCameraBack);
                        this.captureElement.FlowDirection = Windows.UI.Xaml.FlowDirection.LeftToRight;
                    }
                }
                else
                {
                    Global.Current.LocalSettings.SaveData(conCameraPanel, conCameraBack);
                    this.captureElement.FlowDirection = Windows.UI.Xaml.FlowDirection.LeftToRight;
                }
                await this.StartCapturePreviewAsync();
            }
        }

        private void zoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.zoomControl.Value = (float)this.zoomSlider.Value;
        }

        private async void AppbarCameraCapture_Click(object sender, RoutedEventArgs e)
        {
            await this.CapturePhoto();
        }

        private async void HardwareButtonsOnCameraHalfPressed(object sender, CameraEventArgs cameraEventArgs)
        {
            if (mediaCapture.VideoDeviceController.FocusControl.Supported)
                await mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
        }

        private async void HardwareButtonsOnCameraPressed(object sender, CameraEventArgs cameraEventArgs)
        {
            await this.CapturePhoto();
        }

        public async System.Threading.Tasks.Task StartCapturePreviewAsync()
        {
            try
            {
                Windows.Devices.Enumeration.Panel devicePanel = Windows.Devices.Enumeration.Panel.Back;
                var obj = Global.Current.LocalSettings.LoadData(conCameraPanel);
                if (obj != null && obj.ToString() == conCameraFront)
                {
                    devicePanel = Windows.Devices.Enumeration.Panel.Front;
                    this.captureElement.FlowDirection = Windows.UI.Xaml.FlowDirection.RightToLeft;
                }
                else
                    this.captureElement.FlowDirection = Windows.UI.Xaml.FlowDirection.LeftToRight;
                if ((App.Current as App).mediaCapture == null)
                    await (App.Current as App).InitializeCapture(devicePanel);
                Global.Current.LocalSettings.SaveData(conCameraPanel, devicePanel.ToString());
                this.mediaCapture = (App.Current as App).mediaCapture;
                this.zoomControl = this.mediaCapture.VideoDeviceController.ZoomControl;
                if(this.mediaCapture.VideoDeviceController.FocusControl.Supported)
                {
                    var focusSettings = new FocusSettings();
                    focusSettings.AutoFocusRange = AutoFocusRange.FullRange;
                    focusSettings.Mode = FocusMode.Auto;
                    focusSettings.WaitForFocus = true;
                    focusSettings.DisableDriverFallback = false;
                    this.mediaCapture.VideoDeviceController.FocusControl.Configure(focusSettings);
                }
                if (this.zoomControl != null && this.zoomControl.Supported)
                {
                    this.zoomSlider.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    this.zoomSlider.IsEnabled = true;
                    this.zoomSlider.Maximum = this.zoomControl.Max;
                    this.zoomSlider.Minimum = this.zoomControl.Min;
                    this.zoomSlider.StepFrequency = this.zoomControl.Step;
                    this.zoomSlider.Value = this.zoomControl.Value;
                }
                else
                {
                    this.zoomSlider.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
                this.AppbarSwitchCamera.IsEnabled = (App.Current as App).isSupportFront;
                await this.mediaCapture.VideoDeviceController.ExposureControl.SetAutoAsync(true);
                this.captureElement.Source = this.mediaCapture;
                await this.mediaCapture.StartPreviewAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task CapturePhoto()
        {
            try
            {
                if ((App.Current as App).IsProcessingPicture)
                    return;
                (App.Current as App).IsProcessingPicture = true;
                using (var randomAccessStream = new InMemoryRandomAccessStream())
                {
                    await mediaCapture.CapturePhotoToStreamAsync(Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg(), randomAccessStream);
                    randomAccessStream.Seek(0);
                    var outStream = new InMemoryRandomAccessStream();
                    var task = new Task(() =>
                    {
                        this.EncodedPhoto(randomAccessStream, outStream);
                    });
                    task.Start();
                    Frame.Navigate(typeof(PictureEdit), outStream);
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void EncodedPhoto(IRandomAccessStream inStream, IRandomAccessStream outStream)
        {
            Guid JpegDecodderID = BitmapDecoder.JpegDecoderId;
            Guid JpegEncoderID = BitmapEncoder.JpegEncoderId;
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(JpegDecodderID, inStream);
            BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(outStream, decoder);
            var obj = Global.Current.LocalSettings.LoadData(conCameraPanel);
            if (obj != null && obj.ToString() == conCameraFront)
                encoder.BitmapTransform.Flip = BitmapFlip.Horizontal;
            else
                encoder.BitmapTransform.Flip = BitmapFlip.None;
            encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
            encoder.BitmapTransform.InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.Fant;
            encoder.IsThumbnailGenerated = true;
            await encoder.FlushAsync();
            (App.Current as App).IsProcessingPicture = false;
        }

    }
}
