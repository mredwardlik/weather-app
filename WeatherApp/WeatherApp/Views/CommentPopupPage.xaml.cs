using Rg.Plugins.Popup.Pages;
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
    public partial class CommentPopupPage : PopupPage
    {
        public String SendData;
        public CommentPopupPage()
        {
            InitializeComponent();
            entrySend.Text = SendData;
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            SendData = entrySend.Text;
        }
    }
}