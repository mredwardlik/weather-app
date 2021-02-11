using Rg.Plugins.Popup.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace WeatherApp.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WeatherAppPage : ContentPage
    {
        public WeatherAppPage()
        {
            InitializeComponent();
        }

        private void ShowPopup(object sender, EventArgs e)
        {
            PopupNavigation.Instance.PushAsync(new PopupView());
        }
    }
}