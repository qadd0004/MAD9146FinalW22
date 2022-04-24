// Mohsen Qaddoura - Karim Shaloh, April 18, 2022
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace MAD9146FinalW22
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SettingsPage : ContentPage
    {
        #region Constructor
        public SettingsPage()
        {
            InitializeComponent();

            SpeechSwitch.IsToggled = App.UseSpeech;
            FaceDataSwitch.IsToggled = App.GetFaceData;

            DisplayDeviceInfo();
            DisplayAppInfo();
        }
        #endregion

        #region Methods
        private void DisplayAppInfo()
        {
            string infoString = $"{AppInfo.Name}{Environment.NewLine}" +
                $"Version: {AppInfo.VersionString}{Environment.NewLine}" +
                $"Build: {AppInfo.BuildString}{Environment.NewLine}";

            AppInformation.Text = infoString;
        }

        private void DisplayDeviceInfo()
        {
            string infoString = $"Manufacturer: {DeviceInfo.Manufacturer}{Environment.NewLine}" +
                $"Model: {DeviceInfo.Model}{Environment.NewLine}" +
                $"Version: {DeviceInfo.VersionString}{Environment.NewLine}" +
                $"Platform: {DeviceInfo.Platform}{Environment.NewLine}" +
                $"Name: {DeviceInfo.Name}{Environment.NewLine}" +
                $"Idiom: {DeviceInfo.Idiom}{Environment.NewLine}" +
                $"Device Type: {DeviceInfo.DeviceType}{Environment.NewLine}";

            DeviceInformation.Text = infoString;

        }

        #endregion

        #region Button and Switch methods
        private void AppSettingsButton_Clicked(object sender, EventArgs e)
        {
            AppInfo.ShowSettingsUI();
        }

        private void FaceDataSwitch_Toggled(object sender, EventArgs e)
        {
            App.GetFaceData = FaceDataSwitch.IsToggled;
        }

        private void SpeechSwitch_Toggled(object sender, EventArgs e)
        {
            App.UseSpeech = SpeechSwitch.IsToggled;
        }

        #endregion
    }
}