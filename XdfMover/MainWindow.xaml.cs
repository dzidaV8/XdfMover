using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

namespace XdfMover
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private NumberStyles params_Base = NumberStyles.HexNumber;

        private int offset = 0;
        private int start = 0;
        private int end = 0;

        private bool Is_Addr_In_Range(int address)
        {
            return (address >= start) && (address < end);
        }

        private XDocument doc;

        private string path;

        private int bin_size = 0;

        private int base_offset = 0;

        private void Load_XDF_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select XDF File",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "xdf",
                Filter = "xdf files (*.xdf)|*.xdf",
                FilterIndex = 2,
                RestoreDirectory = true,
                Multiselect = false,
                ReadOnlyChecked = true,
                ShowReadOnly = true
            };
            bin_size = 0;
            base_offset = 0;
            doc = null;

            if (openFileDialog.ShowDialog() == true)
            {
                bt_Modify.IsEnabled = true;
                path = openFileDialog.FileName;
                doc = new XDocument(new XComment(" Written " + DateTime.Now.ToString("G", DateTimeFormatInfo.InvariantInfo)), XElement.Load(path));
                textBox1.Background.Opacity = 0.1;
                textBox1.Clear();
                textBox1.AppendText("Loaded " + path + Environment.NewLine);
                tb_SaveName.Text = System.IO.Path.GetFileNameWithoutExtension(path) + "_mod.xdf";
                XElement el = doc.Elements("XDFFORMAT").Elements("XDFHEADER").First();
                textBox1.AppendText("Title: " + el.Element("deftitle").Value.ToString() + Environment.NewLine);
                bin_size = Convert.ToInt32(el.Element("REGION").Attribute("size").Value.Trim(), 16);
                base_offset = Convert.ToInt32(el.Element("BASEOFFSET").Attribute("offset").Value.Trim(), 16);
                textBox1.AppendText("Binary size: 0x" + bin_size.ToString("X") + " offset ");
                if (Convert.ToBoolean(Convert.ToInt32(el.Element("BASEOFFSET").Attribute("subtract").Value.Trim())))
                {
                    base_offset = 0 - base_offset;
                    textBox1.AppendText("-");
                }
                textBox1.AppendText("0x" + Math.Abs(base_offset) + Environment.NewLine);
            }
        }

        private void RB_Hex_Clicked(object sender, RoutedEventArgs e)
        {
            params_Base = NumberStyles.HexNumber;
        }

        private void RB_Decimal_Clicked(object sender, RoutedEventArgs e)
        {
            params_Base = NumberStyles.Integer;
        }

        private string Process_TextBox(TextBox tb)
        {
            string ret_str = tb.Text.Trim();
            if (params_Base == NumberStyles.HexNumber)
            {
                ret_str = ret_str.Replace("0x", "").Replace("&h", "").Replace("$", "");
            }
            return ret_str;
        }

        private bool ValidateParams(bool test)
        {
            if (lb_Error == null)
            {
                return false;
            }

            bool ok = true;
            lb_Error.Content = "";
            if (!int.TryParse(Process_TextBox(tb_Offset), params_Base, CultureInfo.CurrentCulture, out offset))
            {
                lb_Error.Content += "Offset invalid!" + Environment.NewLine;
                ok = false;
            }
            if(!int.TryParse(Process_TextBox(tb_StartAddr), params_Base, CultureInfo.CurrentCulture, out start))
            {
                lb_Error.Content += "Start address invalid!" + Environment.NewLine;
                ok = false;
            }
            if(!int.TryParse(Process_TextBox(tb_EndAddr), params_Base, CultureInfo.CurrentCulture, out end))
            {
                lb_Error.Content += "End address invalid!" + Environment.NewLine;
                ok = false;
            }
            if (ok && test)
            {
                if (!(start < end))
                {
                    lb_Error.Content += "Start must be less than end!" + Environment.NewLine;
                    ok = false;
                }
                if ((offset == 0))
                {
                    lb_Error.Content += "Zero offset, nothing to do!";
                    ok = false;
                }
            }
            return ok;
        }

        private bool IsFileNameCorrect(string fileName)
        {
            return !fileName.Any(f => System.IO.Path.GetInvalidFileNameChars().Contains(f));
        }

        private bool CheckFileName(string name)
        {
            if ((!IsFileNameCorrect(name)) || (name == ""))
            {
                lb_Error.Content += "Improper save file name!";
                return false;
            }
            return true;
        }

        private void BT_Modify_Click(object sender, RoutedEventArgs e)
        {
            string name = tb_SaveName.Text;
            if (ValidateParams(true) && CheckFileName(name))
            {
                if (!name.EndsWith(".xdf")) name += ".xdf";
                tb_SaveName.Text = name;
                textBox1.AppendText("********************* STARTING *******************" + Environment.NewLine);
                textBox1.AppendText("********************* SUCCESS ********************" + Environment.NewLine
                                    + "Modified " + ModifyXdf().ToString() + " entries." + Environment.NewLine);
                textBox1.AppendText("********************* SAVING  ********************" + Environment.NewLine);

                string save_file = System.IO.Path.GetDirectoryName(path) + "\\" + name;
                SaveXdf(save_file);
            }
            textBox1.ScrollToEnd();
        }

        private int ModifyXdf()
        {
            int converted = 0;
            foreach (XElement element in doc.Elements("XDFFORMAT").Elements("XDFCONSTANT"))
            {
                if (element.Element("EMBEDDEDDATA").Attribute("mmedaddress") != null)
                {
                    int addr = Convert.ToInt32(element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value.Trim(), 16);
                    if (Is_Addr_In_Range(addr))
                    {
                        textBox1.AppendText(element.Element("title").Value.ToString() + Environment.NewLine);
                        converted++;
                        textBox1.AppendText("\t" + addr.ToString("X5") + " => ");
                        addr += offset;
                        textBox1.AppendText(addr.ToString("X5") + Environment.NewLine);
                        element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value = "0x" + addr.ToString("X");
                    }

                    // Check for addresses used in math calculations
                    foreach (XElement el in element.Elements("MATH"))
                    {
                        if (el.Element("VAR").Attribute("address") != null)
                        {
                            if (Is_Addr_In_Range(addr))
                            {
                                converted++;
                                textBox1.AppendText("\t" + addr.ToString("X5") + " => ");
                                addr += offset;
                                textBox1.AppendText(addr.ToString("X5") + Environment.NewLine);
                                el.Element("VAR").Attribute("address").Value = "0x" + addr.ToString("X");
                            }
                        }
                    }
                }
            }

            foreach (XElement element in doc.Elements("XDFFORMAT").Elements("XDFTABLE").Elements("XDFAXIS"))
            {
                if (element.Element("EMBEDDEDDATA").Attribute("mmedaddress") != null)
                {
                    int addr = Convert.ToInt32(element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value.Trim(), 16);
                    if (Is_Addr_In_Range(addr))
                    {
                        textBox1.AppendText(element.Parent.Element("title").Value.ToString() + Environment.NewLine);
                        converted++;
                        textBox1.AppendText("\t" + addr.ToString("X5") + " => ");
                        addr += offset;
                        textBox1.AppendText(addr.ToString("X5") + Environment.NewLine);
                        element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value = "0x" + addr.ToString("X");
                    }

                    // Check for addresses used in math calculations
                    foreach (XElement el in element.Elements("MATH"))
                    {
                        if (el.Element("VAR").Attribute("address") != null)
                        {
                            if (Is_Addr_In_Range(addr))
                            {
                                converted++;
                                textBox1.AppendText("\t" + addr.ToString("X5") + " => ");
                                addr += offset;
                                textBox1.AppendText(addr.ToString("X5") + Environment.NewLine);
                                el.Element("VAR").Attribute("address").Value = "0x" + addr.ToString("X");
                            }
                        }
                    }
                }
            }

            foreach (XElement element in doc.Elements("XDFFORMAT").Elements("XDFFLAG"))
            {
                if (element.Element("EMBEDDEDDATA").Attribute("mmedaddress") != null)
                {
                    var addr = Convert.ToInt32(element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value.Trim(), 16);
                    if (Is_Addr_In_Range(addr))
                    {
                        textBox1.AppendText(element.Element("title").Value.ToString() + Environment.NewLine);
                        converted++;
                        textBox1.AppendText("\t" + addr.ToString("X5") + " => ");
                        addr += offset;
                        textBox1.AppendText(addr.ToString("X5") + Environment.NewLine);
                        element.Element("EMBEDDEDDATA").Attribute("mmedaddress").Value = "0x" + addr.ToString("X");
                    }
                }
            }
            return converted;
        }

        private void SaveXdf(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                NewLineHandling = NewLineHandling.Entitize,
                Indent = true,
                Encoding = Encoding.ASCII
            };

            using (XmlWriter xw = XmlWriter.Create(path, settings))
            {
                doc.Save(xw);
                textBox1.AppendText("Saved to: " + path + Environment.NewLine);
            }
        }

        private void TB_TextChanged(object sender, TextChangedEventArgs e)
        {
			ValidateParams(false);
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }
    }
}
