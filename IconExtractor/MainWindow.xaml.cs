using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Diagnostics;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace IconExtractor
{
    public partial class MainWindow : Window
    {
        private OpenFileDialog _ddsFile;
        private OpenFileDialog _txtFile;
        private Rectangle _itemIconRect;
        private Rectangle _iconFileRect;
        private Dictionary<string, string> _iconSkillMap;
        private string _skillsSrcDir;
        private string _outputFolderPath;

        public MainWindow()
        {
            InitializeComponent();
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

        private void btnSelectSkillsSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            SelectSkillsSourceFolder();
        }

        private void SelectSkillsSourceFolder()
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
                _skillsSrcDir = Path.GetDirectoryName(dialog.FileName);
                lblSkillsSource.Content = _skillsSrcDir;
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Skills source folder selection error!");
            }
        }



        private void PreloadIconSkillMap()
        {
            try
            {
                _iconSkillMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(_skillsSrcDir) || !Directory.Exists(_skillsSrcDir))
                {
                    System.Windows.Forms.MessageBox.Show("skills_src directory not found.");
                    return;
                }

                foreach (string hFilePath in Directory.GetFiles(_skillsSrcDir, "*.h"))
                {
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(hFilePath, Encoding.GetEncoding("GB18030"));
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show("Failed to read file: " + hFilePath);
                        continue;
                    }

                    string skillId = ExtractSkillIdFromFileName(hFilePath);
                    if (string.IsNullOrEmpty(skillId))
                    {
                        System.Windows.Forms.MessageBox.Show("Failed to extract skill ID from file name: " + hFilePath);
                        continue;
                    }

                    foreach (string line in lines)
                    {
                        if (line.ToLower().Contains("icon") && line.ToLower().Contains(".dds"))
                        {
                            string iconFileName = ExtractIconFileName(line);
                            if (iconFileName != null)
                            {
                                string iconFileNameLower = iconFileName.ToLower();
                                if (!_iconSkillMap.ContainsKey(iconFileNameLower))
                                {
                                    _iconSkillMap[iconFileNameLower] = skillId;
                                }
                            }
                        }
                    }
                }

                //PrintIconSkillMap();

            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in PreloadIconSkillMap: " + ex.Message);
            }
        }


        private string ExtractSkillIdFromFileName(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (fileName.StartsWith("skill"))
                {
                    string skillId = fileName.Substring(5);
                    return skillId;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in ExtractSkillIdFromFileName: " + ex.Message);
            }
            return null;
        }

        private string ExtractIconFileName(string line)
        {
            try
            {
                int startIndex = line.IndexOf("\"") + 1;
                int endIndex = line.ToLower().IndexOf(".dds\"");
                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string iconFileName = line.Substring(startIndex, endIndex - startIndex + 4).Trim().ToLower();
                    return iconFileName;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Error in ExtractIconFileName: " + ex.Message);
            }
            return null;
        }

        private void PrintIconSkillMap()
        {
            Debug.WriteLine("Printing contents of _iconSkillMap:");
            foreach (var entry in _iconSkillMap)
            {
                Debug.WriteLine($"Icon: {entry.Key} -> Skill ID: {entry.Value}");
            }
        }

        // based on dumbfck's code: https://www.elitepvpers.com/forum/pw-hacks-bots-cheats-exploits/1422109-autopot-ingame-menu.html#post12792417
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
