using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Uniconta.API.System;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.DataModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage.Attachments
{
    public partial class CWAddEditFolder : ChildWindow
    {
        public string FolderName;
        public byte Action;
        CrudAPI api;
        SQLCache folderCache;
        public CWAddEditFolder(CrudAPI api, string folderName, byte action)
        {
            InitializeComponent();
            this.api = api;
            this.FolderName = folderName;
            if (folderName != null)
                txtFolder.Text = folderName;
            this.Action = action;
            this.DataContext = this;
            switch (action)
            {
                case 0:
                    this.Title = string.Format(Uniconta.ClientTools.Localization.lookup("CreateOBJ"), Uniconta.ClientTools.Localization.lookup("Folder"));
                    break;
                case 1:
                    this.Title = string.Format(Uniconta.ClientTools.Localization.lookup("EditOBJ"), Uniconta.ClientTools.Localization.lookup("Folder"));
                    break;
                case 2:
                    this.Title = string.Format(Uniconta.ClientTools.Localization.lookup("DeleteOBJ"), Uniconta.ClientTools.Localization.lookup("Folder"));
                    txtFolder.IsReadOnly = true;
                    break;
            }
            this.KeyDown += CWCreateFolder_KeyDown;
            this.Loaded += CWCreateFolder_Loaded;
            folderCache = this.api.CompanyEntity.GetCache(typeof(DocumentFolder));
        }

        private void CWCreateFolder_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (string.IsNullOrWhiteSpace(txtFolder.Text) && Action == 1)
                    txtFolder.Focus();
                else
                    OKButton.Focus();
            }));
        }

        private void CWCreateFolder_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
            }
            else if (e.Key == Key.Enter)
            {
                if (CancelButton.IsFocused)
                {
                    this.DialogResult = false;
                    return;
                }
                SaveButton_Click(null, null);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (folderCache == null)
                return;
            DocumentFolder folder = null;
            if (FolderName != null)
                folder = folderCache.Get(FolderName) as DocumentFolder;
            FolderName = txtFolder.Text;
            switch (Action)
            {
                case 0:
                    var folderClient = new DocumentFolderClient();
                    folderClient._Name = FolderName;
                    api.Insert(folderClient).GetAwaiter().GetResult();
                    break;
                case 1:
                    if (folder != null)
                    {
                        folder._Name = FolderName;
                        api.Update(folder).GetAwaiter().GetResult();
                    }
                    break;
                case 2:
                    if (folder != null)
                        api.Delete(folder).GetAwaiter().GetResult();
                    break;
            }

            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}

