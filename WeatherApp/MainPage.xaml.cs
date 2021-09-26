using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace WeatherApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            label.Text = String.Format("Value is {0}", e.NewValue);
        }
    }
}
