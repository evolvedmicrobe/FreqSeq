using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using FREQSeq;
using Forms=System.Windows.Forms;

namespace FreqSeqWPF
{
    /// <summary>
    /// Interaction logic for ImportSeqPage.xaml
    /// </summary>
    public partial class ImportSeqPage : Page
    {        
        /// <summary>
        /// Describes Molecule Type
        /// </summary>
        
        /// <summary>
        /// Describes the selected filenames. 
        /// </summary>
        private ObservableCollection<string> fileNames;
        
        #region -- Constructor --

        /// <summary>
        /// Initializes the Opendialog.
        /// </summary>
        /// <param name="types">Supported file Types</param>
        /// <param name="info">Collection of the files and the sequences parsed from them</param>
        /// <param name="showFileBrowserAtStartup">Indicates whether to show file browse dialog by default</param>
        public ImportSeqPage()
        {

            this.fileNames = new ObservableCollection<string>();
            this.InitializeComponent();

            this.btnImport.Click += new RoutedEventHandler(this.OnImportButtonClick);
            this.btnImportCancel.Click += new RoutedEventHandler(this.OnCancelAnimationButtonClick);
            this.btnBrowse.Click += new RoutedEventHandler(this.OnBrowseButtonClick);
            this.btnBrowse.Focus();
            
            

            
        }

        #endregion

        #region -- Public Events --

        /// <summary>
        /// Event to close the Pop up, It informs the 
        /// Controller that the pop is closed and to 
        /// close the Gray background.
        /// </summary>
        public event EventHandler ClosePopup;

        /// <summary>
        /// Event to cancel the import of files, It informs the 
        /// Controller to cancel the import of files.
        /// </summary>
        public event EventHandler CancelImport;
        #endregion



        #region -- Public Methods --

        /// <summary>
        /// Hides the animation and shows the 
        /// import and cancel button
        /// </summary>
        public void OnCancelImport()
        {
            buttonPanel.Visibility = Visibility.Visible;
            animationPanel.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region -- Private Methods --

     

        public List<string> LogData=new List<string>();
        void AF_LoggerEvent(object sender, string Message)
        {
            LogData.Add(Message);
        }

        /// <summary>
        /// On import button click would inform the controller to import files,
        /// would also pass the list of filenames and the molecule type as event args.
        /// </summary>
        /// <param name="sender">Framework Element</param>
        /// <param name="e">Routed event args</param>
        private void OnImportButtonClick(object sender, RoutedEventArgs e)
        {
            //// Creates the collection of the File names.
            buttonPanel.Visibility = Visibility.Collapsed;
            animationPanel.Visibility = Visibility.Visible;
            AlleleFinder AF= App.AlleleSearcher;
            Thread.Sleep(100);
            Thread t = new Thread(RunAnalysis);
           
            t.Start();
            
        }
        private void RunAnalysis()
        {
            try
            {
                AlleleFinder AF = App.AlleleSearcher;
                Thread.Sleep(100);
                DateTime start = DateTime.Now;
                AF.Verbose = true;
                AF.LoggerEvent += new LogEventHandler(AF_LoggerEvent);
                AF.OutputFileNamePrefix = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)+"\\" + AF.OutputFileNamePrefix.Replace(".csv", "");
                AF.SetFileNames(fileNames.ToList());
                AF.ParseFiles();
                AF.MakeReport();
                double totMinutes = DateTime.Now.Subtract(start).TotalMinutes;
                LogData.Add("Finished successfully");
                LogData.Add("Analysis took: " + totMinutes.ToString("F") + " minutes");

                Dispatcher.Invoke((Action)(() => { Continue(); }));
            }
            catch (Exception thrown)
            {
                MessageBox.Show("Error: Could not run analysis\nException is: " + thrown.Message,"Error",MessageBoxButton.OK,MessageBoxImage.Error);
                Dispatcher.BeginInvoke((Action)(() => { Application.Current.Shutdown(); }));
            }
        }
        private void Continue()
        {
            FinishPage FP = new FinishPage(LogData);

            this.NavigationService.Navigate(FP); 
        }
        /// <summary>
        /// On cancel button click would close the Importing dialog and would 
        /// inform the controller
        /// </summary>
        /// <param name="sender">Framework Element</param>
        /// <param name="e">Routed Event args</param>
        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            //// Raise the event to controller, inform closing of the pop up
            if (this.ClosePopup != null)
            {
                this.ClosePopup(sender, e);
            }


        }

        /// <summary>
        /// On cancel button click of Importing of files would inform 
        /// the controller to cancel the import of files through events  
        /// </summary>
        /// <param name="sender">Framework Element</param>
        /// <param name="e">Routed events args</param>
        private void OnCancelAnimationButtonClick(object sender, RoutedEventArgs e)
        {
            //// Raise the event 
           
                MessageBox.Show("Ending Program");
                Dispatcher.Invoke((Action)(()=>{Application.Current.Shutdown();}));
            
        }

        /// <summary>
        /// Handles the click on the Browse button,Launches the Windows Open File dialog
        /// with custom File formats filters being set to the dialog.
        /// On selection of files shows the paths of the selected files on the screen.
        /// Gives option to import the files.
        /// </summary>
        /// <param name="sender">Framework Element</param>
        /// <param name="e">Routed event args</param>
        private void OnBrowseButtonClick(object sender, RoutedEventArgs e)
        {
            //// Launch the FileDialog
            this.LaunchWindowFileDialog();
        }

        /// <summary>
        /// Launches the File Dialog, creates the selected filenames list, 
        /// also validates the selected file name for import.
        /// </summary>
        /// <param name="fileDialog">OpenFiledialog instance to be launched</param>
        private void LaunchWindowFileDialog()
        {
            //// Create and launch the Windows File Dialog, Set various validations
            using (System.Windows.Forms.OpenFileDialog fileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                fileDialog.Multiselect = true;
                fileDialog.CheckFileExists = true;
                fileDialog.CheckPathExists = true;
                fileDialog.Filter = "Fastq Files|*.*";

                // On SuccessFull selection of the files. 
                if (fileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Reset the file name collection
                    this.fileNames = new ObservableCollection<string>();
                    this.fileNameList.Items.Clear();

                    //// Validate the file type and create a list of file names to be displayed on the screen.
                    foreach (string file in fileDialog.FileNames)
                    {
                        fileNames.Add(file);     
                    }
                    fileNameList.ItemsSource = fileNames;
                    this.btnImport.Focus();
                    btnImport.IsEnabled = true;
                }
                else
                {
                    this.btnBrowse.Focus();
                  
                }
            }
        }




        #endregion

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

    }
}
