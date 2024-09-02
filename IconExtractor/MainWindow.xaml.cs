using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Diagnostics;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Runtime.InteropServices;

namespace IconExtractor
{
    public class ElementSkillWrapper
    {
        private IntPtr _dllHandle;
        private delegate void InitStaticDataDelegate();
        private delegate IntPtr GetIconStaticDelegate(int skillId);

        private InitStaticDataDelegate InitStaticDataFunc;
        private GetIconStaticDelegate GetIconStaticFunc;

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        public bool LoadDll(string dllPath)
        {
            _dllHandle = LoadLibrary(dllPath);
            if (_dllHandle == IntPtr.Zero)
            {
                System.Windows.Forms.MessageBox.Show("Failed to load DLL.");
                return false;
            }

            IntPtr initStaticDataPtr = GetProcAddress(_dllHandle, "?InitStaticData@ElementSkill@GNET@@SAXXZ");
            IntPtr getIconStaticPtr = GetProcAddress(_dllHandle, "?GetIcon@ElementSkill@GNET@@SAPEBDI@Z");

            if (initStaticDataPtr == IntPtr.Zero || getIconStaticPtr == IntPtr.Zero)
            {
                System.Windows.Forms.MessageBox.Show("Failed to get function addresses.");
                FreeLibrary(_dllHandle);
                return false;
            }

            InitStaticDataFunc = Marshal.GetDelegateForFunctionPointer<InitStaticDataDelegate>(initStaticDataPtr);
            GetIconStaticFunc = Marshal.GetDelegateForFunctionPointer<GetIconStaticDelegate>(getIconStaticPtr);

            return true;
        }

        public void InitStaticData()
        {
            InitStaticDataFunc?.Invoke();
        }

        public IntPtr GetIconStatic(int skillId)
        {
            return GetIconStaticFunc?.Invoke(skillId) ?? IntPtr.Zero;
        }

        public void UnloadDll()
        {
            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }
        }
    }

    public partial class MainWindow : Window
    {
        private OpenFileDialog _ddsFile;
        private OpenFileDialog _txtFile;
        private Rectangle _itemIconRect;
        private Rectangle _iconFileRect;
        private Dictionary<string, string> _iconSkillMap;
        private string _dllFilePath;
        private string _outputFolderPath;
        private ElementSkillWrapper _elementSkillWrapper;

        public MainWindow()
        {
            InitializeComponent();
            _elementSkillWrapper = new ElementSkillWrapper();
        }

        private void OpenDds()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = " |*.dds";
            openFileDialog.Title = "Select a .dds file";
            if (openFileDialog.ShowDialog() == true)
            {
                _ddsFile = openFileDialog;
                lblStatus.Content = "dds is ready to extract";
                lblFilePath.Content = _ddsFile.FileName;
            }
        }

        private void OpenTxt()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = " |*.txt";
            openFileDialog.Title = "Select a .txt file";
            if (openFileDialog.ShowDialog() == true)
            {
                _txtFile = openFileDialog;
                lblTxtPath.Content = _txtFile.FileName;
            }
        }

        private void SelectOutputFolder()
        {
            var dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select folder"
            };

            if (dialog.ShowDialog() == true)
            {
                _outputFolderPath = Path.GetDirectoryName(dialog.FileName);
                lblOutput.Content = _outputFolderPath;
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Output folder error!");
            }
        }

        private void btnSelectElementSkillDll_Click(object sender, RoutedEventArgs e)
        {
            SelectElementSkillDll();
        }

        private void SelectElementSkillDll()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DLL files|*.dll|All files|*.*",
                Title = "Select elementskill_64.dll"
            };

            if (dialog.ShowDialog() == true)
            {
                _dllFilePath = dialog.FileName;
                lblSkillsSource.Content = _dllFilePath;

                if (!_elementSkillWrapper.LoadDll(_dllFilePath))
                {
                    System.Windows.Forms.MessageBox.Show("Failed to load the selected DLL.");
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("DLL selection error!");
            }
        }

        private void PreloadIconSkillMap()
        {
            try
            {
                _elementSkillWrapper.InitStaticData();

                _iconSkillMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int _skillId = 1; _skillId <= 10000; _skillId++)
                {
                    IntPtr staticIconPtr = _elementSkillWrapper.GetIconStatic(_skillId);
                    if (staticIconPtr != IntPtr.Zero)
                    {
                        string staticIcon = Marshal.PtrToStringAnsi(staticIconPtr);
                        string decodedIcon = Encoding.GetEncoding("GB2312").GetString(Encoding.Default.GetBytes(staticIcon));

                        string iconFileNameLower = decodedIcon.ToLower();

                        if (!_iconSkillMap.ContainsKey(iconFileNameLower))
                        {
                            _iconSkillMap[iconFileNameLower] = _skillId.ToString();
                        }

                        Debug.WriteLine($"Ícone estático para skillId {_skillId}: {decodedIcon}");
                    }
                    else
                    {
                        Debug.WriteLine($"Falha ao obter o ícone estático para skillId {_skillId}.");
                    }
                }

                PrintIconSkillMap();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in PreloadIconSkillMap: " + ex.Message);
            }
        }

        private void PrintIconSkillMap()
        {
            Debug.WriteLine("Printing contents of _iconSkillMap:");
            foreach (var entry in _iconSkillMap)
            {
                Debug.WriteLine($"Icon: {entry.Key} -> Skill ID: {entry.Value}");
            }
        }

        private void ExtractIcons()
        {
            try
            {
                if (_ddsFile == null)
                {
                    System.Windows.Forms.MessageBox.Show("DDS file not selected.");
                    return;
                }

                if (_txtFile == null)
                {
                    System.Windows.Forms.MessageBox.Show("TXT file not selected.");
                    return;
                }

                if (string.IsNullOrEmpty(_outputFolderPath) || !Directory.Exists(_outputFolderPath))
                {
                    System.Windows.Forms.MessageBox.Show("Output folder not selected or doesn't exist.");
                    return;
                }

                PreloadIconSkillMap();

                Bitmap bm = _DDS.LoadImage(_ddsFile.FileName);
                bm.Save(_ddsFile.SafeFileName.Replace(".dds", ".png"));
                _iconFileRect = new Rectangle(0, 0, bm.Width, bm.Height);

                StreamReader sr = new StreamReader(_txtFile.FileName, Encoding.GetEncoding("GB2312"));

                int tempY = Convert.ToInt32(sr.ReadLine());
                int tempX = Convert.ToInt32(sr.ReadLine());
                _itemIconRect = new Rectangle(0, 0, tempY, tempX);

                tempY = Convert.ToInt32(sr.ReadLine());
                tempX = Convert.ToInt32(sr.ReadLine());
                _iconFileRect = new Rectangle(0, 0, tempX, tempY);

                string line;
                int iconIndex = 0;

                Bitmap bmpImage = new Bitmap(bm);

                while ((line = sr.ReadLine()) != null)
                {
                    try
                    {
                        Rectangle p = CalculateIconPositionFromDdsFile(iconIndex);
                        string outputFilePath = $"{_outputFolderPath}/{line.Replace(".dds", ".png").ToLower()}";

                        if (_iconSkillMap.ContainsKey(line.ToLower()))
                        {
                            outputFilePath = $"{_outputFolderPath}/{_iconSkillMap[line.ToLower()]}.png";
                        }

                        bm.Clone(p, bmpImage.PixelFormat).Save(outputFilePath);
                        lbDdsNames.Items.Add(line);
                        iconIndex++;
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show("Error processing icon: " + iconIndex.ToString() + " " + ex.Message);
                    }
                }

                sr.Close();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in ExtractIcons: " + ex.Message);
            }
        }

        private Rectangle CalculateIconPositionFromDdsFile(int iconIndex)
        {
            try
            {
                Rectangle pos = new Rectangle(0, 0, _itemIconRect.Width, _itemIconRect.Height);
                int remainder;
                int quotient = Math.DivRem(iconIndex, _iconFileRect.Width, out remainder);
                pos.X = remainder * _itemIconRect.Width;
                pos.Y = quotient * _itemIconRect.Height;

                return pos;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in CalculateIconPositionFromDdsFile: " + ex.Message);
                throw;
            }
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenDds();
        }

        private void btnExtract_Click(object sender, RoutedEventArgs e)
        {
            ExtractIcons();
            lblStatus.Content = "Done";
        }

        private void btnOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            SelectOutputFolder();
        }

        private void btnOpenTxt_Click(object sender, RoutedEventArgs e)
        {
            OpenTxt();
        }
    }
}
