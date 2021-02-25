using Rg.Plugins.Popup.Pages;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using WeatherApp.Helper;

namespace WeatherApp.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CommentPopupPage : PopupPage
    {
        public CommentPopupPage()
        {
            InitializeComponent();
            //commentEntry.Text = CommentPopupPageState.CommentText;
            commentEntry.Text = Application.Current.Properties.ContainsKey("CommentText") ? (string)Application.Current.Properties["CommentText"] : "";
            
        }

        protected override bool OnBackgroundClicked()
        {
            //CommentPopupPageState.CommentText = commentEntry.Text;
            Application.Current.Properties["CommentText"] = commentEntry.Text;
            return base.OnBackgroundClicked();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            commentEntry.Text = "";
        }
    }
}