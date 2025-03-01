﻿using Diffusion.Updater;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diffusion.Toolkit
{
    public class WelcomeModel : BaseNotify
    {
        private bool _dontShowWelcomeOnStartup;
        private string _version;

        public bool DontShowWelcomeOnStartup
        {
            get => _dontShowWelcomeOnStartup;
            set => SetField(ref _dontShowWelcomeOnStartup, value);
        }

        public string Version
        {
            get => _version;
            set => SetField(ref _version, value);
        }
    }

    /// <summary>
    /// Interaction logic for Tips.xaml
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        private readonly Settings _settings;
        private readonly WelcomeModel _model;

        public WelcomeWindow(Settings settings)
        {
            _settings = settings;

            InitializeComponent();

            var semanticVersion = SemanticVersionHelper.GetLocalVersion();

            _model = new WelcomeModel();
            _model.DontShowWelcomeOnStartup = settings.DontShowWelcomeOnStartup;
            _model.Version = semanticVersion.ToString();

            Closing += (sender, args) =>
            {
                settings.SetPristine();
                settings.DontShowWelcomeOnStartup = _model.DontShowWelcomeOnStartup;
            };

            DataContext = _model;
        }

        private void HyperLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
            e.Handled = true;
        }
    }
}
