﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using DocumentTranslationService.Core;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslation.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ViewModel ViewModel;
        private PerLanguageData perLanguageData = new();

        public MainWindow()
        {
            InitializeComponent();
            ViewModel viewModel = new();
            DataContext = viewModel;
            viewModel.OnLanguagesUpdate += ViewModel_OnLanguagesUpdate;
            viewModel.OnKeyVaultAuthenticationStart += ViewModel_OnKeyVaultAuthenticationStart;
            viewModel.OnKeyVaultAuthenticationComplete += ViewModel_OnKeyVaultAuthenticationComplete;
            AppSettingsSetter.SettingsReadComplete += AppSettingsSetter_SettingsReadComplete;
            ViewModel = viewModel;
            toLanguageBox.ItemsSource = ViewModel.ToLanguageList;
            fromLanguageBox.ItemsSource = ViewModel.FromLanguageList;
            toLanguageBoxDocuments.ItemsSource = ViewModel.ToLanguageListForDocuments;
            fromLanguageBoxDocuments.ItemsSource = ViewModel.FromLanguageListForDocuments;
            CategoryDocumentsBox.ItemsSource = ViewModel.categories.MyCategoryList;
            CategoryTextBox.ItemsSource = ViewModel.categories.MyCategoryList;
        }
        private void ViewModel_OnKeyVaultAuthenticationStart(object sender, EventArgs e)
        {
            StatusBarText1.Text = Properties.Resources.msg_SigningIn;
        }

        private async void ViewModel_OnKeyVaultAuthenticationComplete(object sender, EventArgs e)
        {
            StatusBarText1.Text = Properties.Resources.msg_SignInComplete;
            await Task.Delay(3000);
            StatusBarText1.Text = string.Empty;
        }

        private void ViewModel_OnLanguagesUpdate(object sender, EventArgs e)
        {
            toLanguageBox.SelectedValue = ViewModel.UISettings.lastToLanguage;
            if (ViewModel.UISettings.lastFromLanguage is not null)
                fromLanguageBox.SelectedValue = ViewModel.UISettings.lastFromLanguage;
            else fromLanguageBox.SelectedIndex = 0;
            toLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastToLanguageDocuments;
            if (ViewModel.UISettings.lastFromLanguageDocuments is not null)
                fromLanguageBoxDocuments.SelectedValue = ViewModel.UISettings.lastFromLanguageDocuments;
            else fromLanguageBoxDocuments.SelectedIndex = 0;
        }

        private void AppSettingsSetter_SettingsReadComplete(object sender, EventArgs e)
        {
            EnableTabs();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.InitializeAsync();
            }
            catch (ArgumentNullException ex)
            {
                SettingsTab.IsSelected = true;
                TranslateDocumentsTab.IsEnabled = false;
                if (ex.ParamName is "SubscriptionKey" or null) TranslateTextTab.IsEnabled = false;
            }
            catch (KeyVaultAccessException ex)
            {
                SettingsTab.IsSelected = true;
                TranslateDocumentsTab.IsEnabled = false;
                TranslateTextTab.IsEnabled = false;
                System.Resources.ResourceManager resx = new("DocumentTranslation.GUI.Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
                StatusBarSText1.Text = resx.GetString(ex.Message, System.Globalization.CultureInfo.CurrentCulture);
                StatusBarSText2.Text = ex.InnerException.Message;
            }
            catch (System.AggregateException ex)
            {
                SettingsTab.IsSelected = true;
                TranslateDocumentsTab.IsEnabled = false;
                TranslateTextTab.IsEnabled = false;
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = ex.InnerException.Message;
            }
            CategoryDocumentsBox.SelectedValue = ViewModel.UISettings.lastCategoryDocuments;
            CategoryTextBox.SelectedValue = ViewModel.UISettings.lastCategoryText;
            ViewModel_OnLanguagesUpdate(this, EventArgs.Empty);
        }

        private void GlossariesToUse_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
        {
            if (GlossariesListBox.Items.Count > 0)
            {
                GlossariesClearButton.Visibility = Visibility.Visible;
                GlossariesSelectButton.Visibility = Visibility.Hidden;
            }
            else
            {
                GlossariesClearButton.Visibility = Visibility.Hidden;
                GlossariesSelectButton.Visibility = Visibility.Visible;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (toLanguageBox.SelectedIndex >= 0) ViewModel.UISettings.lastToLanguage = toLanguageBox.SelectedValue as string;
            if (fromLanguageBox.SelectedIndex >= 0) ViewModel.UISettings.lastFromLanguage = fromLanguageBox.SelectedValue as string;
            if (toLanguageBoxDocuments.SelectedIndex >= 0) ViewModel.UISettings.lastToLanguageDocuments = toLanguageBoxDocuments.SelectedValue as string;
            if (fromLanguageBox.SelectedIndex >= 0) ViewModel.UISettings.lastFromLanguageDocuments = fromLanguageBoxDocuments.SelectedValue as string;
            if (CategoryDocumentsBox.SelectedItem is not null) ViewModel.UISettings.lastCategoryDocuments = ((MyCategory)CategoryDocumentsBox.SelectedItem).Name ?? string.Empty;
            else ViewModel.UISettings.lastCategoryDocuments = null;
            if (CategoryTextBox.SelectedItem is not null) ViewModel.UISettings.lastCategoryText = ((MyCategory)CategoryTextBox.SelectedItem).Name ?? string.Empty;
            else ViewModel.UISettings.lastCategoryText = null;
            ViewModel.SaveUISettings();
        }

        private void TabItemAuthentication_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.localSettings.UsingKeyVault)
            {
                subscriptionKey.IsEnabled = false;
                region.IsEnabled = false;
                storageConnectionString.IsEnabled = false;
                resourceName.IsEnabled = false;
            }
            else
            {
                subscriptionKey.IsEnabled = true;
                region.IsEnabled = true;
                storageConnectionString.IsEnabled = true;
                resourceName.IsEnabled = true;
            }
            keyVaultName.Text = ViewModel.localSettings.AzureKeyVaultName;
            subscriptionKey.Password = ViewModel.localSettings.SubscriptionKey;
            ViewModel.GetAzureRegions();
            region.ItemsSource = ViewModel.AzureRegions;
            region.SelectedIndex = ViewModel.GetIndex(ViewModel.AzureRegions, ViewModel.localSettings.AzureRegion);
            storageConnectionString.Text = ViewModel.localSettings.ConnectionStrings?.StorageConnectionString;
            resourceName.Text = ViewModel.localSettings.AzureResourceName;
            experimentalCheckbox.IsChecked = ViewModel.localSettings.ShowExperimental;
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.documentTranslationService.Category = CategoryTextBox.SelectedItem is not null ? ((MyCategory)CategoryTextBox.SelectedItem).ID : null;
            try
            {
                outputBox.Text = await ViewModel.TranslateTextAsync(inputBox.Text, fromLanguageBox.SelectedValue as string, toLanguageBox.SelectedValue as string);
                StatusBarTText2.Text = $"{inputBox.Text.Length} {Properties.Resources.msg_TranslateButton_Click_CharactersTranslated}";
                await Task.Delay(2000);
                StatusBarTText2.Text = string.Empty;
            }
            catch (InvalidCategoryException)
            {
                outputBox.Text = string.Empty;
                StatusBarTText1.Text = Properties.Resources.msg_TranslateButton_Click_Error;
                StatusBarTText2.Text = Properties.Resources.msg_TranslateButton_Click_InvalidCategory;
                await Task.Delay(2000);
                StatusBarTText1.Text = string.Empty;
                StatusBarTText2.Text = string.Empty;
            }
            catch (AccessViolationException ex)
            {
                outputBox.Text = string.Empty;
                StatusBarTText1.Text = Properties.Resources.msg_TranslateButton_Click_Error;
                StatusBarTText2.Text = ex.Message;
                await Task.Delay(2000);
                StatusBarTText1.Text = string.Empty;
                StatusBarTText2.Text = string.Empty;
            }
        }

        private async void DocumentBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            ViewModel.FilesToTranslate.Clear();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            if (ViewModel.UISettings.lastDocumentsFolder is not null) openFileDialog.InitialDirectory = ViewModel.UISettings.lastDocumentsFolder;
            try
            {
                openFileDialog.Filter = await ViewModel.GetDocumentExtensionsFilter();
            }
            catch (Azure.RequestFailedException ex)
            {
                StatusBarText1.Text = Properties.Resources.msg_Error;
                if (ex.Status == 401 || ex.Status == 403) StatusBarText2.Text = Properties.Resources.msg_S1OrHigherTierRequired;
                else StatusBarText2.Text = ex.Message;
                await Task.Delay(2000);
                StatusBarText1.Text = string.Empty;
                StatusBarText2.Text = string.Empty;
                return;
            }
            catch
            {
                StatusBarText1.Text = Properties.Resources.msg_Error;
                await Task.Delay(2000);
                StatusBarText1.Text = string.Empty;
                return;
            }
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                ViewModel.FilesToTranslate.Add(filename);
            FilesListBox.ItemsSource = ViewModel.FilesToTranslate;
            if (ViewModel.FilesToTranslate.Count > 0)
            {
                if (string.IsNullOrEmpty(TargetTextBox.Text)) TargetTextBox.Text = Path.GetDirectoryName(ViewModel.FilesToTranslate[0]) + "." + toLanguageBoxDocuments.SelectedValue as string;
            }
            SetTranslateDocumentsButtonStatus();
            return;
        }

        private void ResetUI()
        {
            ProgressBar.Value = 0;
            StatusBarText1.Text = string.Empty;
            StatusBarText2.Text = string.Empty;
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Gray;
            CancelButton.Visibility = Visibility.Visible;
            TargetOpenButton.Visibility = Visibility.Hidden;
            SetTranslateDocumentsButtonStatus();
        }

        private async void GlossariesBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            GlossariesListBox.Items.Clear();
            OpenFileDialog openFileDialog = new() { RestoreDirectory = true, CheckFileExists = true, Multiselect = true };
            try
            {
                openFileDialog.Filter = await ViewModel.GetGlossaryExtensionsFilter();
            }
            catch (Azure.RequestFailedException ex)
            {
                StatusBarText1.Text = Properties.Resources.msg_Error;
                if (ex.Status == 401 || ex.Status == 403) StatusBarText2.Text = DocumentTranslation.GUI.Properties.Resources.msg_S1OrHigherTierRequired;
                else StatusBarText2.Text = ex.Message;
                await Task.Delay(2000);
                StatusBarText1.Text = string.Empty;
                StatusBarText2.Text = string.Empty;
                return;
            }
            if (perLanguageData?.lastGlossariesFolder is not null) openFileDialog.InitialDirectory = perLanguageData.lastGlossariesFolder;
            openFileDialog.ShowDialog();
            foreach (var filename in openFileDialog.FileNames)
                GlossariesListBox.Items.Add(filename);
            GlossariesToUse_ListChanged(this, null);
            return;
        }

        private void TargetOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(ViewModel.TargetFolder)) Process.Start("explorer.exe", ViewModel.TargetFolder);
        }

        private void TargetBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ResetUI();
            FolderBrowserDialog folderBrowserDialog = new();
            if ((perLanguageData is not null) && (perLanguageData.lastTargetFolder is not null)) folderBrowserDialog.SelectedPath = perLanguageData.lastTargetFolder;
            folderBrowserDialog.ShowDialog();
            TargetTextBox.Text = folderBrowserDialog.SelectedPath;
            SetTranslateDocumentsButtonStatus();
        }

        private void SetTranslateDocumentsButtonStatus()
        {
            if ((ViewModel.FilesToTranslate.Count > 0) && (!string.IsNullOrEmpty(TargetTextBox.Text)) && (toLanguageBoxDocuments.SelectedIndex >= 0)) translateDocumentsButton.IsEnabled = true;
            else translateDocumentsButton.IsEnabled = false;
        }

        private void DocumentsTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (toLanguageBoxDocuments.SelectedIndex < 0) return;
            ResetUI();
            if (ViewModel.FilesToTranslate.Count == 0) return;
            CancelButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = true;
            ViewModel.TargetFolder = TargetTextBox.Text;
            ViewModel.UISettings.lastDocumentsFolder = Path.GetDirectoryName(ViewModel.FilesToTranslate[0]);
            if (ViewModel.GlossariesToUse is null) ViewModel.GlossariesToUse = new();
            ViewModel.GlossariesToUse.Clear();
            foreach (var item in GlossariesListBox.Items) ViewModel.GlossariesToUse.Add(item as string);
            PerLanguageData perLanguageData = new();
            if ((ViewModel.GlossariesToUse.Count > 0) && (ViewModel.GlossariesToUse[0] is not null))
            {
                perLanguageData.lastGlossariesFolder = Path.GetDirectoryName(ViewModel.GlossariesToUse[0]);
                perLanguageData.lastGlossary = ViewModel.GlossariesToUse[0];
            }
            perLanguageData.lastTargetFolder = ViewModel.TargetFolder;
            if (ViewModel.UISettings.PerLanguageFolders.ContainsKey(toLanguageBoxDocuments.SelectedValue as string))
            {
                ViewModel.UISettings.PerLanguageFolders.Remove(toLanguageBoxDocuments.SelectedValue as string);
            }
            ViewModel.UISettings.PerLanguageFolders.Add(toLanguageBoxDocuments.SelectedValue as string, perLanguageData);
            ViewModel.UISettings.lastFromLanguageDocuments = fromLanguageBoxDocuments.SelectedValue as string;
            ViewModel.UISettings.lastToLanguageDocuments = toLanguageBoxDocuments.SelectedValue as string;
            ViewModel.SaveUISettings();
            if (CategoryDocumentsBox.SelectedItem is not null) ViewModel.documentTranslationService.Category = ((MyCategory)CategoryDocumentsBox.SelectedItem).ID;
            else ViewModel.documentTranslationService.Category = null;
            DocumentTranslationBusiness documentTranslationBusiness = new(ViewModel.documentTranslationService);
            documentTranslationBusiness.OnUploadStart += DocumentTranslationBusiness_OnUploadStart;
            documentTranslationBusiness.OnUploadComplete += DocumentTranslationBusiness_OnUploadComplete;
            documentTranslationBusiness.OnStatusUpdate += DocumentTranslationBusiness_OnStatusUpdate;
            documentTranslationBusiness.OnDownloadComplete += DocumentTranslationBusiness_OnDownloadComplete;
            documentTranslationBusiness.OnContainerCreationFailure += DocumentTranslationBusiness_OnContainerCreationFailure;
            documentTranslationBusiness.OnFinalResults += DocumentTranslationBusiness_OnFinalResults;
            documentTranslationBusiness.OnThereWereErrors += DocumentTranslationBusiness_OnThereWereErrors;
            List<string> filestotranslate = new();
            foreach (var document in ViewModel.FilesToTranslate) filestotranslate.Add(document);
            List<string> glossariestouse = new();
            foreach (var glossary in ViewModel.GlossariesToUse) glossariestouse.Add(glossary);
            documentTranslationBusiness.OnContainerCreationFailure += DocumentTranslationBusiness_OnContainerCreationFailure;
            _ = documentTranslationBusiness.RunAsync(filestotranslate, fromLanguageBoxDocuments.SelectedValue as string, toLanguageBoxDocuments.SelectedValue as string, glossariestouse, ViewModel.TargetFolder);
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 3;
        }

        private void DocumentTranslationBusiness_OnUploadStart(object sender, EventArgs e)
        {
            ProgressBar.Value += 2;
            StatusBarText1.Text = Properties.Resources.msg_DocumentUploadStarted;
        }

        private void DocumentTranslationBusiness_OnContainerCreationFailure(object sender, string e)
        {
            ResetUI();
            StatusBarText1.Text = Properties.Resources.msg_StorageContainerError;
            StatusBarText2.Text = e;
            ProgressBar.Value = 0;
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBarText1.Text = Properties.Resources.msg_Canceling;
            CancelButton.IsEnabled = false;
            CancelButton.Background = Brushes.Red;
            try
            {
                await ViewModel.documentTranslationService.CancelRunAsync();
            }
            catch (UriFormatException) { }
            catch (Exception ex)
            {
                StatusBarText2.Text = ex.Message;
            }
            StatusBarText1.Text = Properties.Resources.msg_Canceled;
        }

        private void DocumentTranslationBusiness_OnUploadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 10;
            StatusBarText1.Text = Properties.Resources.msg_DocumentsUploaded;
            StatusBarText2.Text = e.Item1 + " " + Properties.Resources.msg_files + " " + e.Item2 + " " + Properties.Resources.msg_Bytes;
        }

        private void DocumentTranslationBusiness_OnStatusUpdate(object sender, StatusResponse e)
        {
            if ((e.Status?.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.ValidationFailed)
                || (e.Status?.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Failed)
                || !string.IsNullOrEmpty(e.Message))  //an error occurred, cannot continue
            {
                StatusBarText1.Text = e.Status?.Status.ToString();
                StatusBarText2.Text = e.Message;
                ProgressBar.Value = 0;
                ProgressBar.IsIndeterminate = false;
                CancelButton.IsEnabled = false;
                CancelButton.Visibility = Visibility.Hidden;
                return;
            }
            CancelButton.Background = Brushes.LightGray;
            StatusBarText1.Text = e.Status.Status.ToString();
            StringBuilder statusText = new();
            if (e.Status.DocumentsInProgress > 0) statusText.Append(Properties.Resources.msg_InProgress + e.Status.DocumentsInProgress + '\t');
            if (e.Status.DocumentsNotStarted > 0) statusText.Append(Properties.Resources.msg_Waiting + e.Status.DocumentsNotStarted + '\t');
            if (e.Status.DocumentsSucceeded > 0) statusText.Append(Properties.Resources.msg_Completed + e.Status.DocumentsSucceeded + '\t');
            if (e.Status.DocumentsFailed > 0) statusText.Append(Properties.Resources.msg_Failed + e.Status.DocumentsFailed + '\t');
            ProgressBar.Value = 10 + (e.Status.DocumentsInProgress / ViewModel.FilesToTranslate.Count * 0.2) + ((e.Status.DocumentsSucceeded + e.Status.DocumentsFailed) / ViewModel.FilesToTranslate.Count * 0.85);
            StatusBarText2.Text = statusText.ToString();
        }


        private void DocumentTranslationBusiness_OnDownloadComplete(object sender, (int, long) e)
        {
            ProgressBar.Value = 100;
            StatusBarText1.Text = Properties.Resources.msg_Done;
            StatusBarText2.Text = $"{e.Item2} {Properties.Resources.msg_Bytes} {e.Item1} {Properties.Resources.msg_DocumentsTranslated} \t";
            CancelButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Hidden;
            TargetOpenButton.Visibility = Visibility.Visible;
        }

        private void DocumentTranslationBusiness_OnFinalResults(object sender, long e)
        {
            StatusBarText2.Text += $" |\t{Properties.Resources.msg_CharactersCharged}{e}";
        }

        private void ToLanguageBoxDocuments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string langCode = toLanguageBoxDocuments.SelectedValue as string;
            if (ViewModel.UISettings.PerLanguageFolders is not null && langCode is not null) ViewModel.UISettings.PerLanguageFolders.TryGetValue(langCode, out perLanguageData);
            if (perLanguageData is not null)
            {
                if (perLanguageData.lastTargetFolder is not null) TargetTextBox.Text = perLanguageData.lastTargetFolder;
                if (perLanguageData.lastGlossary is not null) GlossariesListBox.Items.Add(perLanguageData.lastGlossary);
                else GlossariesListBox.Items.Clear();
            }
            else
            {
                GlossariesListBox.Items.Clear();
                foreach (Language lang in ViewModel.ToLanguageList)
                    if (TargetTextBox.Text.ToLowerInvariant().EndsWith("." + lang.LangCode.ToLowerInvariant()))
                        TargetTextBox.Text = TargetTextBox.Text.Substring(0, TargetTextBox.Text.Length - lang.LangCode.Length) + langCode;
            }
            GlossariesToUse_ListChanged(this, null);
        }

        private async void EnableTabs()
        {
            await Task.Delay(10);
            TranslateTextTab.IsEnabled = true;
            TranslateDocumentsTab.IsEnabled = true;
            if (string.IsNullOrEmpty(ViewModel.Settings.SubscriptionKey)) { TranslateDocumentsTab.IsEnabled = false; TranslateTextTab.IsEnabled = false; return; }
            if (string.IsNullOrEmpty(ViewModel.Settings.ConnectionStrings.StorageConnectionString)) TranslateDocumentsTab.IsEnabled = false;
            if (string.IsNullOrEmpty(ViewModel.Settings.AzureRegion)) TranslateTextTab.IsEnabled = false;
            if (string.IsNullOrEmpty(ViewModel.Settings.AzureResourceName)) TranslateDocumentsTab.IsEnabled = false;
            region.ItemsSource = ViewModel.AzureRegions;
            if (ViewModel.localSettings.UsingKeyVault)
            {
                keyVaultName.Text = ViewModel.localSettings.AzureKeyVaultName;
            }
            else
            {
                subscriptionKey.Password = ViewModel.localSettings.SubscriptionKey;
                region.SelectedIndex = ViewModel.GetIndex(ViewModel.AzureRegions, ViewModel.localSettings.AzureRegion);
                storageConnectionString.Text = ViewModel.localSettings.ConnectionStrings.StorageConnectionString;
                resourceName.Text = ViewModel.localSettings.AzureResourceName;
            }
        }

        private void SubscriptionKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.localSettings.SubscriptionKey = subscriptionKey.Password;
        }

        private void Region_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.localSettings.AzureRegion = (string)region.SelectedValue;
            if (ViewModel.documentTranslationService is not null) ViewModel.documentTranslationService.AzureRegion = (string)region.SelectedValue;
        }

        private void ResourceName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.localSettings.AzureResourceName = resourceName.Text;
        }

        private void StorageConnection_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.localSettings.ConnectionStrings is null) ViewModel.localSettings.ConnectionStrings = new Connectionstrings();
            ViewModel.localSettings.ConnectionStrings.StorageConnectionString = storageConnectionString.Text;
        }

        private async void TestSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBarSText2.Text = string.Empty;
            StatusBarSText1.Text = Properties.Resources.Label_Testing;
            try
            {
                await ViewModel.InitializeAsync();
                await ViewModel.documentTranslationService.TryCredentials();
                StatusBarSText1.Text = Properties.Resources.msg_TestPassed;
            }
            catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException ex)
            {
                string message;
                if (ex.Message == "name") message = Properties.Resources.msg_ResourceNameIncorrect;
                else message = ex.Message;
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = message;
            }
            catch (ArgumentNullException ex)
            {
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = ex.Message;
            }
            catch (KeyVaultAccessException ex)
            {
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = ex.Message;
            }
            catch (System.AggregateException ex)
            {
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = ex.InnerException.Message;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                StatusBarSText1.Text = Properties.Resources.msg_TestFailed;
                StatusBarSText2.Text = ex.InnerException.Message;
            }
            await Task.Delay(10000);
            StatusBarSText1.Text = string.Empty;
            StatusBarSText2.Text = string.Empty;
        }

        private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBarSText2.Text = string.Empty;
            StatusBarSText1.Text = Properties.Resources.msg_SettingsSaved;
            ViewModel.SaveAppSettings();
            if (ViewModel.localSettings.UsingKeyVault) StatusBarSText1.Text = Properties.Resources.msg_SigningIn;
            try
            {
                await ViewModel.InitializeAsync();
            }
            catch
            {
                TestSettingsButton_Click(this, null);
            }
            if (ViewModel.localSettings.UsingKeyVault) StatusBarSText1.Text = Properties.Resources.msg_SignInComplete;
            EnableTabs();
            await Task.Delay(3000);
            StatusBarSText1.Text = string.Empty;
        }

        private void CategoriesTab_Loaded(object sender, RoutedEventArgs e)
        {
            CategoriesGridView.AllowUserToAddRows = true;
            CategoriesGridView.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            CategoriesGridView.DataSource = ViewModel.categories.MyCategoryList;
            CategoriesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            CategoriesGridView.Columns[0].FillWeight = 2;
            CategoriesGridView.Columns[0].HeaderText = Properties.Resources.label_CategoryName;
            CategoriesGridView.Columns[1].FillWeight = 3;
            CategoriesGridView.Columns[1].HeaderText = Properties.Resources.label_CategoryId;
        }

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddCategory(CategoriesGridView.SelectedCells);
        }

        private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteCategory(CategoriesGridView.SelectedCells);
        }

        private async void SaveCategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            SavedCategoriesText.Visibility = Visibility.Visible;
            CategoriesGridView.EndEdit();
            ViewModel.SaveCategories();
            await Task.Delay(500);
            SavedCategoriesText.Visibility = Visibility.Hidden;
        }

        private void CategoryDocumentsClearButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryDocumentsBox.SelectedItem = null;
            CategoryDocumentsClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryTextClearButton_Click(object sender, RoutedEventArgs e)
        {
            CategoryTextBox.SelectedItem = null;
            CategoryTextClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryTextBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryTextBox.SelectedValue is not null) CategoryTextClearButton.Visibility = Visibility.Visible;
            else CategoryTextClearButton.Visibility = Visibility.Hidden;
        }

        private void CategoryDocumentsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryDocumentsBox.SelectedValue is not null) CategoryDocumentsClearButton.Visibility = Visibility.Visible;
            else CategoryDocumentsClearButton.Visibility = Visibility.Hidden;
        }

        private void ExperimentalCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            ViewModel.localSettings.ShowExperimental = experimentalCheckbox.IsChecked.Value;
        }

        private void ExperimentalCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.localSettings.ShowExperimental = experimentalCheckbox.IsChecked.Value;
        }

        private void FromLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fromLanguageBox.SelectedItem is not Language lang) return;
            if (lang.Bidi)
            {
                inputBox.TextAlignment = TextAlignment.Right;
                inputBox.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                inputBox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right;
            }
            else
            {
                inputBox.TextAlignment = TextAlignment.Left;
                inputBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                inputBox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            }
        }

        private void ToLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (toLanguageBox.SelectedItem is null) return;
            if ((toLanguageBox.SelectedItem as Language).Bidi)
            {
                outputBox.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                outputBox.TextAlignment = TextAlignment.Right;
                outputBox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right;
            }
            else
            {
                outputBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                outputBox.TextAlignment = TextAlignment.Left;
                outputBox.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            }
            SetTranslateDocumentsButtonStatus();
        }

        private void TranslateTextTab_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }

        private void GlossariesClearButton_Click(object sender, RoutedEventArgs e)
        {
            GlossariesListBox.Items.Clear();
            GlossariesClearButton.Visibility = Visibility.Hidden;
            GlossariesSelectButton.Visibility = Visibility.Visible;
        }
        private void DocumentTranslationBusiness_OnThereWereErrors(object sender, string e)
        {
            ThereWereErrorsButton.Visibility = Visibility.Visible;
            ViewModel.ErrorsText = e;
        }


        private void ThereWereErrorsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowErrors showErrors = new();
            showErrors.ErrorsText.Text = ViewModel.ErrorsText;
            showErrors.ShowDialog();
            ThereWereErrorsButton.Visibility = Visibility.Hidden;
        }

        private void KeyVaultName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(keyVaultName.Text))
            {
                subscriptionKey.IsEnabled = true;
                region.IsEnabled = true;
                resourceName.IsEnabled = true;
                storageConnectionString.IsEnabled = true;
            }
            else
            {
                subscriptionKey.IsEnabled = false;
                region.IsEnabled = false;
                resourceName.IsEnabled = false;
                storageConnectionString.IsEnabled = false;
            }
            ViewModel.localSettings.AzureKeyVaultName = keyVaultName.Text;
        }

        private void KeyVaultNameClearButton_Click(object sender, RoutedEventArgs e)
        {
            keyVaultName.Text = string.Empty;
            ViewModel.localSettings.AzureKeyVaultName = string.Empty;
            StatusBarSText1.Text = string.Empty;
            StatusBarSText2.Text = string.Empty;
        }
    }
}
