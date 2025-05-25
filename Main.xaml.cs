using System;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Windows.Media;
using System.Reflection;
using System.Text;

namespace Yafes
{
    public partial class Main : Window

    {
        private bool _driversMessageShown = false;
        private bool _programsMessageShown = false;
        private readonly string driversFolder = "C:\\Drivers";
        private readonly string programsFolder = "C:\\Programs"; // Yeni programlar klasörü
        private readonly string alternativeDriversFolder = "F:\\MSI Drivers"; // Alternatif sürücü klasörü
        private readonly string alternativeProgramsFolder = "F:\\Programs"; // Alternatif programlar klasörü
        private readonly HttpClient httpClient = new HttpClient();
        private List<DriverInfo> drivers = new List<DriverInfo>();
        private List<ProgramInfo> programs = new List<ProgramInfo>(); // Yeni program listesi
        private int currentDriverIndex = 0;
        private int currentProgramIndex = 0; // Yeni programlar için indeks
        private bool isInstalling = false;
        private Dictionary<string, string> driverStatusMap = new Dictionary<string, string>();
        private Dictionary<string, string> programStatusMap = new Dictionary<string, string>(); // Program durum haritası
        private Dictionary<string, Dictionary<string, bool>> categorySelections = new Dictionary<string, Dictionary<string, bool>>();
        // Ana listeler - değişmeyecek kaynak listeler
        private List<DriverInfo> masterDrivers = new List<DriverInfo>();
        private List<ProgramInfo> masterPrograms = new List<ProgramInfo>();
        // Kategori değişkenleri
        private string currentCategory = "Sürücüler"; // Varsayılan kategori

        public Main()
        {
            InitializeComponent();
            txtLog.AppendText("Yafes Kurulum Aracı başlatıldı\n");
            txtLog.AppendText("Lütfen 'Yükle' butonuna tıklayarak işleme başlayın\n");

            // Ana klasörleri oluştur
            if (!Directory.Exists(driversFolder))
            {
                Directory.CreateDirectory(driversFolder);
            }

            if (!Directory.Exists(programsFolder))
            {
                Directory.CreateDirectory(programsFolder);
            }

            // Ana listeleri oluştur
            masterDrivers = new List<DriverInfo>();
            masterPrograms = new List<ProgramInfo>();
            drivers = new List<DriverInfo>();
            programs = new List<ProgramInfo>();

            // Sürücü bilgilerini ekle
            InitializeDrivers();

            // Program bilgilerini ekle
            InitializePrograms();

            // Kategori sistemini başlat
            InitializeCategories();

            // İnternet bağlantısını kontrol et ve bilgilendir
            if (IsInternetAvailable())
            {
                txtLog.AppendText("Online Bağlantı Hazır...\n");
            }
            else
            {
                txtLog.AppendText("İnternet bağlantısı bulunamadı. Gömülü kaynaklar veya alternatif klasörlerden yükleme yapılacak\n");

                // Alternatif klasörlerin varlığını kontrol et
                if (Directory.Exists(alternativeDriversFolder))
                {
                    txtLog.AppendText("Alternatif sürücü klasörü bulundu.\n");
                }
                else
                {
                    txtLog.AppendText("UYARI: Alternatif sürücü klasörü bulunamadı: " + alternativeDriversFolder + "\n");
                }

                if (Directory.Exists(alternativeProgramsFolder))
                {
                    txtLog.AppendText("Alternatif program klasörü bulundu.\n");
                }
                else
                {
                    txtLog.AppendText("UYARI: Alternatif program klasörü bulunamadı: " + alternativeProgramsFolder + "\n");
                }
            }

            // Gömülü kaynakları listeleyerek kontrol et (DEBUG amaçlı)
            ListEmbeddedResources();
        }

        // Gömülü kaynakları listele ve log'a yaz (DEBUG amaçlı)
        private void ListEmbeddedResources()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Gömülü kaynaklar:");
                foreach (var name in resourceNames)
                {
                    sb.AppendLine($"  - {name}");
                }

                // Debug konsola yaz
                Console.WriteLine(sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Gömülü kaynaklar listelenirken hata: {ex.Message}");
            }
        }

        // İnternet bağlantısını kontrol et
        private bool IsInternetAvailable()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 2000); // Google DNS sunucusuna 2 saniye timeout ile ping at
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false; // Herhangi bir hata durumunda internet yok olarak kabul et
            }
        }

        private void InitializeDrivers()
        {
            // Önce listeleri temizle
            masterDrivers.Clear();
            drivers.Clear();

            // Sürücüleri ekle
            masterDrivers.Add(new DriverInfo
            {
                Name = "NVIDIA Graphics Driver",
                Url = "https://tr.download.nvidia.com/Windows/576.40/576.40-desktop-win10-win11-64bit-international-dch-whql.exe",
                FileName = "nvidia_driver.exe",
                ProcessName = "setup",
                InstallArguments = "/s /n",
                IsZip = false,
                AlternativeSearchPattern = "nvidia*.exe",
                ResourceName = "Yafes.Resources.nvidia_driver.exe"
            });

            masterDrivers.Add(new DriverInfo
            {
                Name = "Realtek PCIe LAN Driver",
                Url = "https://download.msi.com/dvr_exe/mb/realtek_pcielan_w10.zip",
                FileName = "realtek_lan.zip",
                ProcessName = "setup",
                InstallArguments = "/s",
                IsZip = true,
                AlternativeSearchPattern = "*lan*.zip",
                ResourceName = "Yafes.Resources.realtek_pcielan_w10.zip"
            });

            masterDrivers.Add(new DriverInfo
            {
                Name = "Realtek Audio Driver",
                Url = "https://download.msi.com/dvr_exe/mb/realtek_audio_R.zip",
                FileName = "realtek_audio.zip",
                ProcessName = "setup",
                InstallArguments = "/s",
                IsZip = true,
                AlternativeSearchPattern = "*audio*.zip",
                ResourceName = "Yafes.Resources.realtek_audio_R.zip"
            });

            // Her sürücü için durum haritası başlat
            foreach (var driver in masterDrivers)
            {
                driverStatusMap[driver.Name] = "Bekliyor";
            }
        }

        private void InitializePrograms()
        {
            // Önce listeleri tamamen temizle
            masterPrograms.Clear();
            programs.Clear();

            // Log ekle
            Console.WriteLine("Program listesi yükleniyor...");

            // Programları ekle - TÜM PROGRAMLARI EKLEDİĞİMİZDEN EMİN OLALIM
            masterPrograms.Add(new ProgramInfo
            {
                Name = "Discord",
                Url = "https://discord.com/api/downloads/distributions/app/installers/latest?channel=stable&platform=win&arch=x64",
                FileName = "DiscordSetup.exe",
                ProcessName = "DiscordSetup",
                InstallArguments = "-s",
                IsZip = false,
                AlternativeSearchPattern = "discord*.exe",
                ResourceName = "Yafes.Resources.DiscordSetup.exe",
                SpecialInstallation = false
            });

            masterPrograms.Add(new ProgramInfo
            {
                Name = "WinRAR",
                Url = "https://www.win-rar.com/postdownload.html?&L=5",
                FileName = "winrar-x64-711tr.exe",
                ProcessName = "WinRAR",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "winrar*.exe",
                ResourceName = "Yafes.Resources.winrar-x64-711tr.exe",
                SpecialInstallation = true
            });

            masterPrograms.Add(new ProgramInfo
            {
                Name = "Opera",
                Url = "https://www.opera.com/tr/computer/thanks?ni=stable&os=windows",
                FileName = "OperaSetup.exe",
                ProcessName = "opera",
                InstallArguments = "--silent --installfolder=\"C:\\Program Files\\Opera\"",
                IsZip = false,
                AlternativeSearchPattern = "opera*.exe",
                ResourceName = "Yafes.Resources.OperaSetup.exe",
                SpecialInstallation = true // ← Bu satırı true yapın

            });

            // Eksik programları ekleyelim
            masterPrograms.Add(new ProgramInfo
            {
                Name = "Steam",
                Url = "https://cdn.fastly.steamstatic.com/client/installer/SteamSetup.exe",
                FileName = "steam_installer.exe",
                ProcessName = "Steam",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "steam*.exe",
                ResourceName = "Yafes.Resources.steam_installer.exe",
                SpecialInstallation = false
            });

            masterPrograms.Add(new ProgramInfo
            {
                Name = "Lightshot",
                Url = "https://app.prntscr.com/build/setup-lightshot.exe",
                FileName = "lightshot_installer.exe",
                ProcessName = "setup-lightshot",
                InstallArguments = "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL",
                IsZip = false,
                AlternativeSearchPattern = "*lightshot*.exe",
                ResourceName = "Yafes.Resources.lightshot_installer.exe",
                SpecialInstallation = false
            });

            masterPrograms.Add(new ProgramInfo
            {
                Name = "Notepad++",
                Url = "https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.7.7/npp.8.7.7.Installer.x64.exe",
                FileName = "npp_installer.exe",
                ProcessName = "notepad++",
                InstallArguments = "/S",
                IsZip = false,
                AlternativeSearchPattern = "npp*.exe",
                ResourceName = "Yafes.Resources.npp_installer.exe",
                SpecialInstallation = false
            });
            masterPrograms.Add(new ProgramInfo
            {
                Name = "Visual Studio Setup",
                Url = "", // İndirme yok, sadece Resources'dan
                FileName = "VisualStudioSetup.exe",
                ProcessName = "VisualStudioSetup",
                InstallArguments = "/quiet", // Visual Studio için silent kurulum
                IsZip = false,
                AlternativeSearchPattern = "VisualStudioSetup*.exe",
                ResourceName = "Yafes.Resources.VisualStudioSetup.exe",
                SpecialInstallation = false
            });

            // uTorrent
            masterPrograms.Add(new ProgramInfo
            {
                Name = "uTorrent",
                Url = "", // İndirme yok, sadece Resources'dan
                FileName = "uTorrent 3.6.0.47196.exe",
                ProcessName = "uTorrent 3.6.0.47196",
                InstallArguments = "/S", // uTorrent için silent kurulum
                IsZip = false,
                AlternativeSearchPattern = "uTorrent*.exe",
                ResourceName = "Yafes.Resources.uTorrent 3.6.0.47196.exe",
                SpecialInstallation = false
            });

            // EA App Installer
            masterPrograms.Add(new ProgramInfo
            {
                Name = "EA App",
                Url = "", // İndirme yok, sadece Resources'dan
                FileName = "EAappInstaller.exe",
                ProcessName = "EAappInstaller",
                InstallArguments = "/quiet", // EA App için silent kurulum
                IsZip = false,
                AlternativeSearchPattern = "EAapp*.exe",
                ResourceName = "Yafes.Resources.EAappInstaller.exe",
                SpecialInstallation = false
            });
            masterPrograms.Add(new ProgramInfo
{
    Name = "Driver Booster",
    Url = "", // İndirme yok, sadece Resources'dan
    FileName = "driver_booster_setup.exe",
    ProcessName = "driver_booster_setup",
    InstallArguments = "/VERYSILENT /NORESTART /NoAutoRun", // Silent kurulum
    IsZip = false,
    AlternativeSearchPattern = "driver_booster*.exe",
    ResourceName = "Yafes.Resources.driver_booster_setup.exe",
    SpecialInstallation = true // Özel kurulum gerekli
});

// Revo Uninstaller Pro - Özel kurulum gerekli
masterPrograms.Add(new ProgramInfo
{
    Name = "Revo Uninstaller Pro",
    Url = "", // İndirme yok, sadece Resources'dan
    FileName = "RevoUninProSetup.exe",
    ProcessName = "RevoUninProSetup",
    InstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", // Silent kurulum
    IsZip = false,
    AlternativeSearchPattern = "RevoUnin*.exe",
    ResourceName = "Yafes.Resources.RevoUninProSetup.exe",
    SpecialInstallation = true // Özel kurulum gerekli
});

            // Program sayısını logla
            Console.WriteLine($"Toplam {masterPrograms.Count} program yüklendi.");

            // Her program için durum haritası başlat
            foreach (var program in masterPrograms)
            {
                programStatusMap[program.Name] = "Bekliyor";
            }
        }
        private void InitializeCategories()
        {
            // Butonları doğrudan kullan - XAML'de zaten tanımlı
            btnDriverCategory.Tag = "Sürücüler";
            btnProgramsCategory.Tag = "Programlar";

            // Click olaylarını ata (XAML'de tanımlı olsa bile bir kez daha atamak sorun olmaz)
            btnDriverCategory.Click += CategoryButton_Click;
            btnProgramsCategory.Click += CategoryButton_Click;

            // Her kategori için veri listelerini oluştur
            drivers = new List<DriverInfo>();
            programs = new List<ProgramInfo>();

            // Driver bilgilerini ekle
            InitializeDrivers();

            // Program bilgilerini ekle
            InitializePrograms();

            // Varsayılan kategoriyi seç
            UpdateCategoryView("Sürücüler");

            // Başlangıçta Sürücüler butonunu vurgula
            btnDriverCategory.Background = new SolidColorBrush(Colors.SkyBlue);
            btnDriverCategory.Foreground = new SolidColorBrush(Colors.White);
            btnDriverCategory.FontWeight = FontWeights.Bold;

            // Programlar butonunu normal stile ayarla
            btnProgramsCategory.Background = new SolidColorBrush(Colors.LightGray);
            btnProgramsCategory.Foreground = new SolidColorBrush(Colors.Black);
            btnProgramsCategory.FontWeight = FontWeights.Normal;
        }
        // Kategori butonu tıklama olayı
        // Kategori butonu tıklama olayı
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category)
            {
                // Eğer kurulum yapılıyorsa kategori değişikliğine izin verme
                if (isInstalling)
                {
                    MessageBox.Show("Kurulum devam ederken kategori değiştirilemez!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Aynı kategori seçiliyse hiçbir şey yapma 
                if (currentCategory == category)
                {
                    return;
                }

                // Kategori görünümünü güncelle
                UpdateCategoryView(category);
            }
        }
        /// <summary>
        /// Opera'yı şifre import özelliği ile kurar
        /// </summary>
        private async Task InstallOperaWithPasswordImport(ProgramInfo program, string installPath)
        {
            try
            {
                // Opera Password Manager'ı oluştur
                var operaManager = new OperaPasswordManager((message) =>
                {
                    // Log callback - UI thread'de güvenli şekilde çalışır
                    Dispatcher.Invoke(() => txtLog.AppendText(message + "\n"));
                });

                // Progress bar'ı güncelle
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 0;
                    progressBarStatus.Value = 0;
                    txtStatusBar.Text = "Opera kuruluyor ve şifreler import ediliyor...";
                });

                // Opera kurulum + şifre import işlemini başlat
                bool success = await operaManager.InstallOperaWithPasswordImport(installPath, program.InstallArguments);

                // Progress bar'ı tamamla
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = 100;
                    progressBarStatus.Value = 100;
                    txtStatusBar.Text = success ? "Opera kurulumu ve şifre import tamamlandı" : "Opera kurulumu tamamlandı";
                });

                if (!success)
                {
                    throw new Exception("Opera kurulum işlemi başarısız oldu.");
                }
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"Opera kurulum hatası: {ex.Message}\n");
                throw; // Hatayı üst katmana ilet
            }
        }

        private void UpdateCategoryView(string category)
        {
            // Eğer aynı kategoriye tekrar tıklandıysa, hiçbir şey yapma
            if (currentCategory == category)
            {
                return;
            }

            // Mevcut görünümdeki seçimleri kaydet
            if (!string.IsNullOrEmpty(currentCategory))
            {
                SaveCurrentSelections(currentCategory);
            }

            // Kategori değiştir
            string oldCategory = currentCategory;
            currentCategory = category;
            RefreshListWithSavedSelections(category);

            // Butonların görünümünü güncelle
            btnDriverCategory.Background = new SolidColorBrush(Colors.LightGray);
            btnDriverCategory.Foreground = new SolidColorBrush(Colors.Black);
            btnDriverCategory.FontWeight = FontWeights.Normal;

            btnProgramsCategory.Background = new SolidColorBrush(Colors.LightGray);
            btnProgramsCategory.Foreground = new SolidColorBrush(Colors.Black);
            btnProgramsCategory.FontWeight = FontWeights.Normal;

            // Seçili kategori düğmesini vurgula
            if (category == "Sürücüler")
            {
                btnDriverCategory.Background = new SolidColorBrush(Colors.SkyBlue);
                btnDriverCategory.Foreground = new SolidColorBrush(Colors.White);
                btnDriverCategory.FontWeight = FontWeights.Bold;
            }
            else if (category == "Programlar")
            {
                btnProgramsCategory.Background = new SolidColorBrush(Colors.SkyBlue);
                btnProgramsCategory.Foreground = new SolidColorBrush(Colors.White);
                btnProgramsCategory.FontWeight = FontWeights.Bold;
            }

            // Liste görünümünü güncelle
            RefreshListWithSavedSelections(category);

            // Sadece gerçekten kategori değişmişse log mesajı yaz
            if (oldCategory != category)
            {
                // Log mesajlarını ekle
               // txtLog.AppendText($"\n{category} kategorisine geçildi. Kurmak istediğiniz öğeleri seçin ve 'Yükle' butonuna tıklayın.\n");
               // txtLog.AppendText($"NOT: Sadece {category} kategorisindeki seçili öğeler kurulacaktır.\n");
            }
        }

        // Mevcut seçimleri sakla
        private void SaveCurrentSelections(string category)
        {
            Dictionary<string, bool> selections = new Dictionary<string, bool>();

            foreach (ListBoxItem item in lstDrivers.Items)
            {
                if (item.Content is CheckBox checkBox && checkBox.Content != null)
                {
                    string name = checkBox.Content.ToString();
                    bool isChecked = checkBox.IsChecked ?? false;
                    selections[name] = isChecked;
                }
            }

            categorySelections[category] = selections;
        }

        // Liste içeriğini kaydedilmiş seçimlerle yenile
        // Liste içeriğini kaydedilmiş seçimlerle yenile
        private void RefreshListWithSavedSelections(string category)
        {
            // Her zaman UI listesini temizle
            lstDrivers.Items.Clear();

            // Kategori için önceden kaydedilmiş seçimler
            Dictionary<string, bool> savedSelections = null;
            if (categorySelections.ContainsKey(category))
            {
                savedSelections = categorySelections[category];
            }

            if (category == "Sürücüler")
            {
                // Sürücüleri göster - SADECE ANA LİSTEDEN
                foreach (var driver in masterDrivers)
                {
                    bool isChecked = true; // Varsayılan değer

                    // Kaydedilmiş seçim varsa onu kullan
                    if (savedSelections != null && savedSelections.ContainsKey(driver.Name))
                    {
                        isChecked = savedSelections[driver.Name];
                    }

                    var checkBox = new CheckBox
                    {
                        Content = driver.Name,
                        IsChecked = isChecked,
                        Tag = driver,
                        Margin = new Thickness(5, 2, 5, 2),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        // CYBER TEMA STİLLERİ
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 255)), // #00F5FF
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };

                    // Cyber CheckBox stili uygula (XAML'deki stili kullan)
                    checkBox.Style = (Style)FindResource("CyberCheckBoxStyle");

                    var item = new ListBoxItem();
                    item.Content = checkBox;
                    lstDrivers.Items.Add(item);
                }
            }
            else if (category == "Programlar")
            {
                // Programları göster - SADECE ANA LİSTEDEN
                foreach (var program in masterPrograms)
                {
                    bool isChecked = true; // Varsayılan değer

                    // Kaydedilmiş seçim varsa onu kullan
                    if (savedSelections != null && savedSelections.ContainsKey(program.Name))
                    {
                        isChecked = savedSelections[program.Name];
                    }

                    var checkBox = new CheckBox
                    {
                        Content = program.Name,
                        IsChecked = isChecked,
                        Tag = program,
                        Margin = new Thickness(5, 2, 5, 2),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        // CYBER TEMA STİLLERİ
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 255)), // #00F5FF
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };

                    // Cyber CheckBox stili uygula (XAML'deki stili kullan)
                    checkBox.Style = (Style)FindResource("CyberCheckBoxStyle");

                    var item = new ListBoxItem();
                    item.Content = checkBox;
                    lstDrivers.Items.Add(item);
                }
            }
        }

        private void UpdateDriverList()
        {
            lstDrivers.Items.Clear();
            foreach (var driver in drivers)
            {
                var checkBox = new CheckBox
                {
                    Content = driver.Name,
                    IsChecked = true,
                    Tag = driver,
                    Margin = new Thickness(5, 2, 5, 2),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    // CYBER TEMA STİLLERİ
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 245, 255)), // #00F5FF
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                };

                // Cyber CheckBox stili uygula
                checkBox.Style = (Style)FindResource("CyberCheckBoxStyle");

                var item = new ListBoxItem();
                item.Content = checkBox;
                lstDrivers.Items.Add(item);
            }
        }

        private void btnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (isInstalling)
            {
                MessageBox.Show("Kurulum zaten devam ediyor!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Log'u temizle ve başlangıç mesajını yaz
                txtLog.Clear();
                txtLog.AppendText("Yafes Kurulum Aracı başlatıldı\n");

                // Her zaman önce sürücüleri, sonra programları kur - kategori seçiminden bağımsız olarak
                PrepareInstallation();
            }
            catch (Exception ex)
            {
                isInstalling = false;
                btnInstall.IsEnabled = true;
                btnAddDriver.IsEnabled = true;
                MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Kurulum için hazırlık yap
        // Kurulum için hazırlık yap
        private void PrepareInstallation()
        {
            // Butonları devre dışı bırak
            btnInstall.IsEnabled = false;
            btnAddDriver.IsEnabled = false;

            isInstalling = true;
            currentDriverIndex = 0;
            currentProgramIndex = 0;

            // Mevcut kategorideki seçimleri kaydet
            SaveCurrentSelections(currentCategory);

            // Kurulacak sürücü ve program listelerini kopya olarak hazırla
            drivers = new List<DriverInfo>();
            programs = new List<ProgramInfo>();

            // Seçili kategoriye göre sadece işaretli öğeleri ekle
            if (currentCategory == "Sürücüler")
            {
                // Seçili sürücüleri tespit et
                foreach (ListBoxItem item in lstDrivers.Items)
                {
                    if (item.Content is CheckBox checkBox && checkBox.IsChecked == true && checkBox.Tag is DriverInfo driver)
                    {
                        drivers.Add(driver);
                    }
                }

                txtLog.AppendText($"ADIM 1: Toplam {drivers.Count} sürücü kurulumu başlatılıyor...\n");
                if (drivers.Count > 0)
                {
                    StartNextDriverInstallation();
                }
                else
                {
                    txtLog.AppendText("Kurulacak sürücü bulunamadı. İşlem tamamlandı.\n");
                    CompleteInstallation();
                }
            }
            else if (currentCategory == "Programlar")
            {
                // Seçili programları tespit et
                foreach (ListBoxItem item in lstDrivers.Items)
                {
                    if (item.Content is CheckBox checkBox && checkBox.IsChecked == true && checkBox.Tag is ProgramInfo program)
                    {
                        programs.Add(program);
                    }
                }

                txtLog.AppendText($"ADIM 1: Toplam {programs.Count} program kurulumu başlatılıyor...\n");
                if (programs.Count > 0)
                {
                    StartProgramInstallations();
                }
                else
                {
                    txtLog.AppendText("Kurulacak program bulunamadı. İşlem tamamlandı.\n");
                    CompleteInstallation();
                }
            }
        }

        // Sürücü listesini hazırla - SADECE SEÇİLİ OLANLAR
        private void PrepareDriveList()
        {
            // Seçili sürücüleri belirle
            List<DriverInfo> selectedDrivers = new List<DriverInfo>();

            // Mevcut listedeki sürücüleri bul
            foreach (ListBoxItem item in lstDrivers.Items)
            {
                if (item.Content is CheckBox checkBox && checkBox.IsChecked == true && checkBox.Tag is DriverInfo driver)
                {
                    selectedDrivers.Add(driver);
                    // Başlangıçta her sürücü için durum haritası oluştur
                    driverStatusMap[driver.Name] = "Bekliyor";
                }
            }

            drivers = selectedDrivers;
            txtLog.AppendText($"Toplam {drivers.Count} sürücü kurulacak.\n");
        }

        // Program listesini hazırla - SADECE SEÇİLİ OLANLAR
        private void PrepareProgramList()
        {
            // Seçili programları belirle
            List<ProgramInfo> selectedPrograms = new List<ProgramInfo>();

            // Mevcut listedeki programları bul
            foreach (ListBoxItem item in lstDrivers.Items)
            {
                if (item.Content is CheckBox checkBox && checkBox.IsChecked == true && checkBox.Tag is ProgramInfo program)
                {
                    selectedPrograms.Add(program);
                    // Başlangıçta her program için durum haritası oluştur
                    programStatusMap[program.Name] = "Bekliyor";
                }
            }

            programs = selectedPrograms;
            txtLog.AppendText($"Toplam {programs.Count} program kurulacak.\n");
        }

        private async void StartNextDriverInstallation()
        {
            if (currentDriverIndex >= drivers.Count)
            {
                // Tüm sürücülerin kurulumu tamamlandı
                txtLog.AppendText("Tüm sürücü kurulumları tamamlandı!\n");

                // Program kurulumlarına geç
                StartProgramInstallations();
                return;
            }

            DriverInfo currentDriver = drivers[currentDriverIndex];

            // Ana klasördeki sürücü dosyasının tam yolu
            string filePath = Path.Combine(driversFolder, currentDriver.FileName);

            // Dosyanın ana klasörde varlığını kontrol et
            bool fileExists = File.Exists(filePath);

            if (fileExists)
            {
                // Dosya ana klasörde zaten var, direkt kuruluma geç
                await InstallDriver(currentDriver, filePath);
            }
            else
            {
                // Önce gömülü kaynakları kontrol et
                bool resourceExtracted = await ExtractEmbeddedResource(currentDriver.ResourceName, filePath);

                if (resourceExtracted)
                {
                    // Gömülü kaynak başarıyla çıkarıldı, kuruluma geç
                    txtLog.AppendText($"Gömülü kaynaktan {currentDriver.Name} çıkarıldı\n");
                    await InstallDriver(currentDriver, filePath);
                }
                else if (IsInternetAvailable())
                {
                    // İnternet bağlantısı var, dosya yok, indir
                    driverStatusMap[currentDriver.Name] = "İndiriliyor";
                    UpdateFormattedLogs();
                    await DownloadDriver(currentDriver, filePath);
                }
                else
                {
                    // İnternet bağlantısı yok, alternatif klasörden sürücüyü ara
                    driverStatusMap[currentDriver.Name] = "Alternatiften Kurulum";
                    UpdateFormattedLogs();

                    // Alternatif klasörde sürücüyü ara
                    string? alternativeFilePath = FindDriverInAlternativeFolder(currentDriver);

                    if (alternativeFilePath != null)
                    {
                        // Alternatif klasörde sürücü bulundu, kur
                        txtLog.AppendText($"Alternatif klasörde sürücü bulundu: {alternativeFilePath}\n");
                        await InstallDriver(currentDriver, alternativeFilePath);
                    }
                    else
                    {
                        // Alternatif klasörde sürücü bulunamadı, hata
                        txtLog.AppendText($"Alternatif klasörde sürücü bulunamadı! Desen: {currentDriver.AlternativeSearchPattern}\n");
                        driverStatusMap[currentDriver.Name] = "Hata";
                        UpdateFormattedLogs();
                        HandleDriverError();
                    }
                }
            }
        }

        // Program kurulumlarını başlat
        private void StartProgramInstallations()
        {
            txtLog.AppendText("\nADIM 2: Program kurulumları başlatılıyor...\n");
            if (programs.Count > 0)
            {
                StartNextProgramInstallation();
            }
            else
            {
                CompleteInstallation();
            }
        }

        // Program kurulum işlemi başlat
        // Program kurulum işlemi başlat
        private async void StartNextProgramInstallation()
        {
            if (currentProgramIndex >= programs.Count)
            {
                // Tüm programların kurulumu tamamlandı
                txtLog.AppendText("Tüm program kurulumları tamamlandı!\n");

                // Kurulumu tamamla
                CompleteInstallation();
                return;
            }

            ProgramInfo currentProgram = programs[currentProgramIndex];

            // WinRAR için özel durum: Doğrudan Resources klasöründen kurulum yap
            if (currentProgram.Name == "WinRAR")
            {
                string resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", currentProgram.FileName);
                string programPath = Path.Combine(programsFolder, currentProgram.FileName);

                if (File.Exists(resourcePath))
                {
                    txtLog.AppendText($"WinRAR için doğrudan resources klasöründen kurulum yapılıyor: {resourcePath}\n");

                    try
                    {
                        // Resources klasöründeki dosyanın boyutunu kontrol et
                        FileInfo resourceFile = new FileInfo(resourcePath);
                        txtLog.AppendText($"WinRAR dosya boyutu: {resourceFile.Length / 1024} KB\n");

                        if (resourceFile.Length < 1024 * 1024) // En az 1 MB olmalı
                        {
                            txtLog.AppendText($"UYARI: WinRAR dosyası çok küçük: {resourceFile.Length / 1024} KB\n");
                        }

                        // Resources'dan Programs klasörüne WinRAR kurulum dosyasını kopyala
                        try
                        {
                            File.Copy(resourcePath, programPath, true);
                            txtLog.AppendText($"WinRAR dosyası başarıyla Programs klasörüne kopyalandı: {programPath}\n");
                        }
                        catch (Exception copyEx)
                        {
                            txtLog.AppendText($"WinRAR dosyası Programs klasörüne kopyalanırken hata: {copyEx.Message}\n");
                        }

                        // Doğrudan resources klasöründeki dosyayı kullan (güvenli olan bu)
                        await InstallProgram(currentProgram, resourcePath);
                        return;
                    }
                    catch (Exception ex)
                    {
                        txtLog.AppendText($"Resources klasöründen kurulum hatası: {ex.Message}\n");
                        // Normal kurulum akışına devam et
                    }
                }
                else
                {
                    txtLog.AppendText($"Resources klasöründe WinRAR bulunamadı: {resourcePath}\n");
                }
            }

            // Program klasörü yolu
            string filePath = Path.Combine(programsFolder, currentProgram.FileName);

            // Dosyanın program klasöründe varlığını kontrol et
            bool fileExists = File.Exists(filePath);

            if (fileExists)
            {
                // Dosya boyutunu kontrol et
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024 * 1024 && currentProgram.Name.Contains("RAR"))
                {
                    txtLog.AppendText($"UYARI: {currentProgram.Name} dosyası bozuk veya eksik: {fileInfo.Length / 1024} KB\n");
                    // Dosya var ama boyutu küçük, yeniden çıkarmayı dene
                    fileExists = false;
                }
                else
                {
                    // Dosya ana klasörde zaten var, direkt kuruluma geç
                    await InstallProgram(currentProgram, filePath);
                }
            }

            if (!fileExists)
            {
                // Önce gömülü kaynakları kontrol et
                bool resourceExtracted = await ExtractEmbeddedResource(currentProgram.ResourceName, filePath);

                if (resourceExtracted)
                {
                    // Gömülü kaynak başarıyla çıkarıldı, kuruluma geç
                    txtLog.AppendText($"Gömülü kaynaktan {currentProgram.Name} çıkarıldı\n");
                    await InstallProgram(currentProgram, filePath);
                }
                else if (IsInternetAvailable())
                {
                    // İnternet bağlantısı var, dosya yok, indir
                    programStatusMap[currentProgram.Name] = "İndiriliyor";
                    UpdateFormattedLogs();
                    await DownloadProgram(currentProgram, filePath);
                }
                else
                {
                    // İnternet bağlantısı yok, alternatif klasörden programı ara
                    programStatusMap[currentProgram.Name] = "Alternatiften Kurulum";
                    UpdateFormattedLogs();

                    // Alternatif klasörde programı ara
                    string? alternativeFilePath = FindProgramInAlternativeFolder(currentProgram);

                    if (alternativeFilePath != null)
                    {
                        // Alternatif klasörde program bulundu, kur
                        txtLog.AppendText($"Alternatif klasörde program bulundu: {alternativeFilePath}\n");
                        await InstallProgram(currentProgram, alternativeFilePath);
                    }
                    else
                    {
                        // Alternatif klasörde program bulunamadı, hata
                        txtLog.AppendText($"Alternatif klasörde program bulunamadı! Desen: {currentProgram.AlternativeSearchPattern}\n");
                        programStatusMap[currentProgram.Name] = "Hata";
                        UpdateFormattedLogs();
                        HandleProgramError();
                    }
                }
            }
        }

        // Kurulum tamamlandığında yapılacak işlemler
        private void CompleteInstallation()
        {
            isInstalling = false;
            btnInstall.IsEnabled = true;
            btnAddDriver.IsEnabled = true;

            progressBar.Value = 100;
            progressBarStatus.Value = 100;
            txtStatusBar.Text = "Tüm kurulumlar tamamlandı";

            // Son bir kez tüm log formatını güncelle
            UpdateCombinedLogs();

            txtLog.AppendText("\n*** TÜM KURULUMLAR TAMAMLANDI! ***\n");

            // Listeleri güncelleme
            RefreshListWithSavedSelections(currentCategory);

            if (chkRestart.IsChecked == true)
            {
                MessageBoxResult result = MessageBox.Show("Kurulum tamamlandı. Bilgisayarı yeniden başlatmak istiyor musunuz?",
                    "Yeniden Başlat", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("shutdown", "/r /t 10");
                    Application.Current.Shutdown();
                }
            }
        }

        // Gömülü kaynağı dosyaya çıkar - bool döndürür (başarılı mı?)
        private async Task<bool> ExtractEmbeddedResource(string resourceName, string outputFilePath)
        {
            try
            {
                // Gömülü kaynağı assembly'den al
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        // Kaynak bulunamadıysa başarısız
                        txtLog.AppendText($"Kaynak bulunamadı: {resourceName}\n");
                        return false;
                    }

                    long expectedSize = resourceStream.Length;
                    txtLog.AppendText($"Kaynak boyutu: {expectedSize / 1024} KB\n");

                    // Dosya içeriğini buffer'a oku ve dosyaya yaz
                    using (FileStream fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long totalBytesRead = 0;
                        long totalBytes = resourceStream.Length;

                        // İlerleme çubuğunu başlat
                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = 0;
                            progressBarStatus.Value = 0;
                            txtStatusBar.Text = $"Gömülü kaynak çıkartılıyor... %0";
                        });

                        // Dosyayı oku ve yaz
                        while ((bytesRead = await resourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;

                            // İlerleme durumunu güncelle
                            int progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);

                            Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = progressPercentage;
                                progressBarStatus.Value = progressPercentage;
                                txtStatusBar.Text = $"Gömülü kaynak çıkartılıyor... %{progressPercentage}";
                            });
                        }
                    }

                    // Dosya boyutunu kontrol et
                    FileInfo fileInfo = new FileInfo(outputFilePath);
                    if (fileInfo.Length < expectedSize * 0.9) // En az beklenen boyutun %90'ı olmalı
                    {
                        txtLog.AppendText($"UYARI: Çıkarılan dosya eksik olabilir. Beklenen: {expectedSize / 1024} KB, Gerçek: {fileInfo.Length / 1024} KB\n");
                        return false;
                    }

                    // Başarıyla tamamlandı
                    txtLog.AppendText($"Dosya başarıyla çıkarıldı: {fileInfo.Length / 1024} KB\n");
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Hata oluşursa consola yaz ve başarısız olarak dön
                txtLog.AppendText($"[HATA] Gömülü kaynak çıkartma hatası: {ex.Message}\n");
                Console.WriteLine($"[ERROR] Gömülü kaynak çıkartma hatası: {ex.Message}");
                return false;
            }
        }

        // Tüm log bilgisini güncelle - hem sürücüler hem de programlar için
        private void UpdateFormattedLogs()
        {
            UpdateCombinedLogs();
        }

        // Hem sürücüleri hem programları içeren kombine log
        private void UpdateCombinedLogs()
        {
            // Tüm log içeriğini temizle
            txtLog.Clear();
            txtLog.AppendText("Yafes Kurulum Aracı başlatıldı\n");

            // Sürücü bölümünü ekle
            if (drivers.Count > 0)
            {
                txtLog.AppendText("\n=== SÜRÜCÜLER ===\n");

                // Her sürücü için formatlanmış log bilgisini ekle
                for (int i = 0; i < drivers.Count; i++)
                {
                    DriverInfo driver = drivers[i];
                    string status = driverStatusMap.ContainsKey(driver.Name) ? driverStatusMap[driver.Name] : "Bekliyor";

                    txtLog.AppendText($"{i + 1}. {driver.Name} kurulumu\n");

                    if (status == "Başarılı")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        if (driver.IsZip)
                            txtLog.AppendText($"   - Çıkartılıyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Kuruluyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Durum: Başarılı ✓\n");
                    }
                    else if (status == "İndiriliyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Çıkartılıyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Çıkartılıyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Kuruluyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        if (driver.IsZip)
                            txtLog.AppendText($"   - Çıkartılıyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Kuruluyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Alternatiften Kurulum")
                    {
                        txtLog.AppendText($"   - Alternatif klasörden yükleniyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Hata")
                    {
                        txtLog.AppendText($"   - Durum: Hata ✗\n");
                    }
                    else
                    {
                        txtLog.AppendText($"   - Durum: {status}\n");
                    }
                }
            }

            // Program bölümünü ekle
            if (programs.Count > 0)
            {
                txtLog.AppendText("\n=== PROGRAMLAR ===\n");

                // Her program için formatlanmış log bilgisini ekle
                for (int i = 0; i < programs.Count; i++)
                {
                    ProgramInfo program = programs[i];
                    string status = programStatusMap.ContainsKey(program.Name) ? programStatusMap[program.Name] : "Bekliyor";

                    txtLog.AppendText($"{i + 1}. {program.Name} kurulumu\n");

                    if (status == "Başarılı")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        if (program.IsZip)
                            txtLog.AppendText($"   - Çıkartılıyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Kuruluyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Durum: Başarılı ✓\n");
                    }
                    else if (status == "İndiriliyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Çıkartılıyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Çıkartılıyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Kuruluyor")
                    {
                        txtLog.AppendText($"   - İndiriliyor... Tamamlandı\n");
                        if (program.IsZip)
                            txtLog.AppendText($"   - Çıkartılıyor... Tamamlandı\n");
                        txtLog.AppendText($"   - Kuruluyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Alternatiften Kurulum")
                    {
                        txtLog.AppendText($"   - Alternatif klasörden yükleniyor... \n");
                        txtLog.AppendText($"   - Durum: İşlem devam ediyor\n");
                    }
                    else if (status == "Hata")
                    {
                        txtLog.AppendText($"   - Durum: Hata ✗\n");
                    }
                    else
                    {
                        txtLog.AppendText($"   - Durum: {status}\n");
                    }
                }
            }
        }

        // Alternatif klasörde sürücü ara - CS8603 düzeltmesi: Nullable dönüş tipi
        private string? FindDriverInAlternativeFolder(DriverInfo driver)
        {
            try
            {
                // Alternatif klasörün varlığını kontrol et
                if (!Directory.Exists(alternativeDriversFolder))
                {
                    txtLog.AppendText($"Alternatif sürücü klasörü bulunamadı: {alternativeDriversFolder}\n");
                    return null;
                }

                // Alternatif klasörde belirtilen desene göre dosya ara
                string[] files = Directory.GetFiles(alternativeDriversFolder, driver.AlternativeSearchPattern, SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    // İlk bulunan dosyayı döndür
                    return files[0];
                }

                return null;
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"Alternatif klasörde arama hatası: {ex.Message}\n");
                return null;
            }
        }

        private async Task DownloadDriver(DriverInfo driver, string filePath)
        {
            try
            {
                txtStatusBar.Text = $"{driver.Name} indiriliyor...";
                progressBarStatus.Value = 0;

                // İndirme işlemi
                using (var response = await httpClient.GetAsync(driver.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // Toplam dosya boyutunu al
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;
                        int lastPercentage = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;

                            // İlerleme çubuğunu güncelle
                            if (totalBytes > 0)
                            {
                                int progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);

                                // Sadece yüzde değeri değiştiyse ve her %25'lik artışta güncelle
                                if (progressPercentage > lastPercentage && (progressPercentage % 25 == 0 || progressPercentage == 100))
                                {
                                    progressBar.Value = progressPercentage;
                                    progressBarStatus.Value = progressPercentage;
                                    txtStatusBar.Text = $"{driver.Name} indiriliyor... %{progressPercentage}";
                                    lastPercentage = progressPercentage;
                                }
                                else
                                {
                                    // Daima progress bar'ı güncelle ama log'a yazma
                                    progressBar.Value = progressPercentage;
                                    progressBarStatus.Value = progressPercentage;
                                    txtStatusBar.Text = $"{driver.Name} indiriliyor... %{progressPercentage}";
                                }
                            }
                        }
                    }
                }

                // Sürücüyü kur
                await InstallDriver(driver, filePath);
            }
            catch (Exception ex)
            {
                // Hata durumunda durumu güncelle
                driverStatusMap[driver.Name] = "Hata";
                UpdateFormattedLogs();
                txtLog.AppendText($"İndirme hatası: {ex.Message}\n");
                HandleDriverError();
            }
        }

        private async Task InstallDriver(DriverInfo driver, string filePath)
        {
            try
            {
                txtStatusBar.Text = $"{driver.Name} kuruluyor...";
                progressBar.Value = 0;

                string installPath = filePath;
                string extractPath = "";

                // Eğer ZIP dosyası ise, çıkart
                if (driver.IsZip)
                {
                    // Durum güncelle - Çıkartılıyor
                    driverStatusMap[driver.Name] = "Çıkartılıyor";
                    UpdateFormattedLogs();

                    extractPath = Path.Combine(driversFolder, Path.GetFileNameWithoutExtension(driver.FileName));

                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }

                    Directory.CreateDirectory(extractPath);

                    await Task.Run(() => ZipFile.ExtractToDirectory(filePath, extractPath));

                    // Kurulum dosyasını bul (setup.exe)
                    string[] setupFiles = Directory.GetFiles(extractPath, "setup.exe", SearchOption.AllDirectories);

                    if (setupFiles.Length > 0)
                    {
                        installPath = setupFiles[0];
                    }
                    else
                    {
                        driverStatusMap[driver.Name] = "Hata";
                        UpdateFormattedLogs();
                        txtLog.AppendText("Kurulum dosyası bulunamadı!\n");
                        HandleDriverError();
                        return;
                    }
                }

                // Durumu güncelle - Kuruluyor
                driverStatusMap[driver.Name] = "Kuruluyor";
                UpdateFormattedLogs();

                // Kurulum işlemini başlat
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = installPath,
                    Arguments = driver.InstallArguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Process? installProcess = Process.Start(psi);

                if (installProcess == null)
                {
                    driverStatusMap[driver.Name] = "Hata";
                    UpdateFormattedLogs();
                    txtLog.AppendText("Kurulum işlemi başlatılamadı!\n");
                    HandleDriverError();
                    return;
                }

                string processName = driver.ProcessName;

                // Kurulum işleminin tamamlanmasını bekle
                await Task.Run(() =>
                {
                    int progress = 0;
                    bool processFound = true;

                    while (processFound)
                    {
                        // İlerleme çubuğunu güncelle
                        if (progress < 95)
                        {
                            progress += 5;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = progress;
                            progressBarStatus.Value = progress;
                            txtStatusBar.Text = $"{driver.Name} kuruluyor... %{progress}";
                        });

                        // Process listesini kontrol et
                        try
                        {
                            // Önce başlattığımız process'i kontrol et
                            if (!installProcess.HasExited)
                            {
                                // Process hala çalışıyor
                                System.Threading.Thread.Sleep(2000); // 2 saniye bekle
                                continue;
                            }

                            // Başlattığımız process bittiyse, benzer adlı başka processler var mı diye kontrol et
                            Process[] processes = Process.GetProcessesByName(processName);

                            if (processes.Length > 0)
                            {
                                // Hala kurulum işlemi devam ediyor
                                System.Threading.Thread.Sleep(2000); // 2 saniye bekle
                            }
                            else
                            {
                                // Kurulum işlemi tamamlandı
                                processFound = false;
                            }
                        }
                        catch
                        {
                            // Process listesine erişemiyorsak da 2 saniye bekle
                            System.Threading.Thread.Sleep(2000);
                        }
                    }

                    // Kurulum tamamlandı, ilerleme çubuğunu %100 yap
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = 100;
                        progressBarStatus.Value = 100;
                        txtStatusBar.Text = $"{driver.Name} kurulumu tamamlandı";
                    });
                });

                // Kurulum tamamlandı, durumu başarılı olarak işaretle
                driverStatusMap[driver.Name] = "Başarılı";
                UpdateFormattedLogs();

                // Sonraki sürücüye geç
                currentDriverIndex++;
                StartNextDriverInstallation();
            }
            catch (Exception ex)
            {
                driverStatusMap[driver.Name] = "Hata";
                UpdateFormattedLogs();
                txtLog.AppendText($"Kurulum hatası: {ex.Message}\n");
                HandleDriverError();
            }
        }

        private void HandleDriverError()
        {
            // Hata durumunda bir sonraki sürücüye geç
            currentDriverIndex++;
            StartNextDriverInstallation();
        }

        // Alternatif klasörde program arama
        private string? FindProgramInAlternativeFolder(ProgramInfo program)
        {
            try
            {
                // Alternatif klasörün varlığını kontrol et
                if (!Directory.Exists(alternativeProgramsFolder))
                {
                    txtLog.AppendText($"Alternatif program klasörü bulunamadı: {alternativeProgramsFolder}\n");
                    return null;
                }

                // Alternatif klasörde belirtilen desene göre dosya ara
                string[] files = Directory.GetFiles(alternativeProgramsFolder, program.AlternativeSearchPattern, SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    // İlk bulunan dosyayı döndür
                    return files[0];
                }

                return null;
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"Alternatif klasörde arama hatası: {ex.Message}\n");
                return null;
            }
        }

        // Program indirme işlemi
        private async Task DownloadProgram(ProgramInfo program, string filePath)
        {
            try
            {
                txtStatusBar.Text = $"{program.Name} indiriliyor...";
                progressBarStatus.Value = 0;

                // İndirme işlemi
                using (var response = await httpClient.GetAsync(program.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    // Toplam dosya boyutunu al
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;
                        int lastPercentage = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;

                            // İlerleme çubuğunu güncelle
                            if (totalBytes > 0)
                            {
                                int progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);

                                // Sadece yüzde değeri değiştiyse güncelle
                                if (progressPercentage > lastPercentage)
                                {
                                    progressBar.Value = progressPercentage;
                                    progressBarStatus.Value = progressPercentage;
                                    txtStatusBar.Text = $"{program.Name} indiriliyor... %{progressPercentage}";
                                    lastPercentage = progressPercentage;
                                }
                            }
                        }
                    }
                }

                // Programı kur
                await InstallProgram(program, filePath);
            }
            catch (Exception ex)
            {
                // Hata durumunda durumu güncelle
                programStatusMap[program.Name] = "Hata";
                UpdateFormattedLogs();
                txtLog.AppendText($"İndirme hatası: {ex.Message}\n");
                HandleProgramError();
            }
        }

        // Program kurulum işlemi
        // Program kurulum işlemi - TAM VERSİYON
        private async Task InstallProgram(ProgramInfo program, string filePath)
        {
            try
            {
                txtStatusBar.Text = $"{program.Name} kuruluyor...";
                progressBar.Value = 0;

                string installPath = filePath;
                string extractPath = "";

                // Eğer ZIP dosyası ise, çıkart
                if (program.IsZip)
                {
                    // Durum güncelle - Çıkartılıyor
                    programStatusMap[program.Name] = "Çıkartılıyor";
                    UpdateFormattedLogs();

                    extractPath = Path.Combine(programsFolder, Path.GetFileNameWithoutExtension(program.FileName));

                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }

                    Directory.CreateDirectory(extractPath);

                    await Task.Run(() => ZipFile.ExtractToDirectory(filePath, extractPath));

                    // Kurulum dosyasını bul (setup.exe)
                    string[] setupFiles = Directory.GetFiles(extractPath, "setup.exe", SearchOption.AllDirectories);

                    if (setupFiles.Length > 0)
                    {
                        installPath = setupFiles[0];
                    }
                    else
                    {
                        programStatusMap[program.Name] = "Hata";
                        UpdateFormattedLogs();
                        txtLog.AppendText("Kurulum dosyası bulunamadı!\n");
                        HandleProgramError();
                        return;
                    }
                }

                // Durumu güncelle - Kuruluyor
                programStatusMap[program.Name] = "Kuruluyor";
                UpdateFormattedLogs();

                // ÖZEL KURULUM KONTROLLERİ

                // WinRAR için özel kurulum
                if (program.SpecialInstallation && program.Name == "WinRAR")
                {
                    await InstallWinRARWithPowerShell(program, installPath);
                }
                // Driver Booster için özel kurulum
                else if (program.SpecialInstallation && program.Name == "Driver Booster")
                {
                    await InstallDriverBoosterWithSpecialFiles(program, installPath);
                }
                // Revo Uninstaller Pro için özel kurulum
                else if (program.SpecialInstallation && program.Name == "Revo Uninstaller Pro")
                {
                    await InstallRevoUninstallerWithLicense(program, installPath);
                }
                // Opera için özel kurulum + şifre import
                else if (program.Name == "Opera")
                {
                    await InstallOperaWithPasswordImport(program, installPath);
                }
                else
                {
                    // NORMAL KURULUM İŞLEMİ
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = installPath,
                        Arguments = program.InstallArguments,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };

                    Process? installProcess = Process.Start(psi);

                    if (installProcess == null)
                    {
                        programStatusMap[program.Name] = "Hata";
                        UpdateFormattedLogs();
                        txtLog.AppendText("Kurulum işlemi başlatılamadı!\n");
                        HandleProgramError();
                        return;
                    }

                    string processName = program.ProcessName;

                    // Kurulum işleminin tamamlanmasını bekle
                    await Task.Run(() =>
                    {
                        int progress = 0;
                        bool processFound = true;

                        while (processFound)
                        {
                            // İlerleme çubuğunu güncelle
                            if (progress < 95)
                            {
                                progress += 5;
                            }

                            Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = progress;
                                progressBarStatus.Value = progress;
                                txtStatusBar.Text = $"{program.Name} kuruluyor... %{progress}";
                            });

                            // Process listesini kontrol et
                            try
                            {
                                // Önce başlattığımız process'i kontrol et
                                if (!installProcess.HasExited)
                                {
                                    // Process hala çalışıyor
                                    System.Threading.Thread.Sleep(2000); // 2 saniye bekle
                                    continue;
                                }

                                // Başlattığımız process bittiyse, benzer adlı başka processler var mı diye kontrol et
                                Process[] processes = Process.GetProcessesByName(processName);

                                if (processes.Length > 0)
                                {
                                    // Hala kurulum işlemi devam ediyor
                                    System.Threading.Thread.Sleep(2000); // 2 saniye bekle
                                }
                                else
                                {
                                    // Kurulum işlemi tamamlandı
                                    processFound = false;
                                }
                            }
                            catch
                            {
                                // Process listesine erişemiyorsak da 2 saniye bekle
                                System.Threading.Thread.Sleep(2000);
                            }
                        }

                        // Kurulum tamamlandı, ilerleme çubuğunu %100 yap
                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = 100;
                            progressBarStatus.Value = 100;
                            txtStatusBar.Text = $"{program.Name} kurulumu tamamlandı";
                        });
                    });
                }

                // Kurulum tamamlandı, durumu başarılı olarak işaretle
                programStatusMap[program.Name] = "Başarılı";
                UpdateFormattedLogs();

                // Sonraki programa geç
                currentProgramIndex++;
                StartNextProgramInstallation();
            }
            catch (Exception ex)
            {
                programStatusMap[program.Name] = "Hata";
                UpdateFormattedLogs();
                txtLog.AppendText($"Kurulum hatası: {ex.Message}\n");
                HandleProgramError();
            }
        }
       
        // WinRAR için özel PowerShell kurulumu
        private async Task InstallWinRARWithPowerShell(ProgramInfo program, string installPath)
        {
            try
            {
                txtLog.AppendText($"WinRAR için özel CMD kurulumu başlatılıyor...\n");

                // Batch dosyası oluştur - Doğrudan CMD komutu kullan, PowerShell kullanma
                string tempBatchFile = Path.Combine(Path.GetTempPath(), "winrar_install.bat");
                string batchContent = $@"@echo off
echo WinRAR kurulumu baslatiliyor...
""{installPath}"" /S
echo Kurulum komutu gonderildi.
exit";

                File.WriteAllText(tempBatchFile, batchContent);

                // Batch dosyasını çalıştır
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{tempBatchFile}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    Verb = "runas" // Yönetici olarak çalıştır
                };

                Process? installProcess = Process.Start(psi);

                if (installProcess == null)
                {
                    throw new Exception("WinRAR kurulum işlemi başlatılamadı!");
                }

                // Kurulum sürecini bekle
                await Task.Run(() => {
                    installProcess.WaitForExit();

                    // CMD kapandıktan sonra WinRAR kurulumunun tamamlanması için ek bekleme
                    Dispatcher.Invoke(() => {
                        txtLog.AppendText("WinRAR kurulum işlemi devam ediyor, tamamlanması bekleniyor...\n");
                    });

                    System.Threading.Thread.Sleep(15000); // 15 saniye bekle (kurulum için yeterli süre)

                    // WinRAR'ın kurulup kurulmadığını kontrol et
                    bool isWinRarInstalled = Directory.Exists(@"C:\Program Files\WinRAR") ||
                                            Directory.Exists(@"C:\Program Files (x86)\WinRAR");

                    Dispatcher.Invoke(() => {
                        if (isWinRarInstalled)
                        {
                            txtLog.AppendText("WinRAR kurulumu başarıyla tamamlandı!\n");
                        }
                        else
                        {
                            txtLog.AppendText("WinRAR kurulumu tamamlandı, ancak kurulum klasörü bulunamadı.\n");
                        }

                        // Batch dosyasını temizlemeyi dene
                        try
                        {
                            if (File.Exists(tempBatchFile))
                            {
                                File.Delete(tempBatchFile);
                            }
                        }
                        catch
                        {
                            // Dosya silinemezse önemli değil
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"WinRAR CMD kurulum hatası: {ex.Message}\n");
                throw; // Hatayı üst katmana ilet
            }
        }
        private async Task InstallRevoUninstallerWithLicense(ProgramInfo program, string installPath)
        {
            try
            {
                txtLog.AppendText($"Revo Uninstaller Pro özel kurulumu başlatılıyor...\n");

                // Normal kurulum işlemini başlat
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = installPath,
                    Arguments = program.InstallArguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Process? installProcess = Process.Start(psi);

                if (installProcess == null)
                {
                    throw new Exception("Revo Uninstaller Pro kurulum işlemi başlatılamadı!");
                }

                // Kurulum sürecini bekle
                await Task.Run(() => {
                    installProcess.WaitForExit();

                    // Kurulum tamamlandıktan sonra ek bekleme
                    Dispatcher.Invoke(() => {
                        txtLog.AppendText("Revo Uninstaller Pro kurulumu tamamlandı, lisans dosyası kopyalanıyor...\n");
                    });
                    System.Threading.Thread.Sleep(5000); // 5 saniye bekle
                });

                // Doğru hedef klasör yolu (resmi kaynaklardan doğrulandı)
                string targetPath = @"C:\ProgramData\VS Revo Group\Revo Uninstaller Pro\revouninstallerpro5.lic";

                txtLog.AppendText($"Hedef klasör: {Path.GetDirectoryName(targetPath)}\n");

                // Lisans dosyasını doğru yere kopyala
                await CopySpecialFileFromResources("revouninstallerpro5.lic", targetPath);

                txtLog.AppendText("Revo Uninstaller Pro özel kurulumu başarıyla tamamlandı!\n");
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"Revo Uninstaller Pro özel kurulum hatası: {ex.Message}\n");
                throw;
            }
        }

        // Geliştirilmiş dosya kopyalama metodu - Debug bilgileri ile
        private async Task CopySpecialFileFromResources(string resourceFileName, string targetPath)
        {
            try
            {
                txtLog.AppendText($"Dosya kopyalama başlatılıyor: {resourceFileName}\n");
                txtLog.AppendText($"Hedef yol: {targetPath}\n");

                // Hedef klasörün var olduğundan emin ol
                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    txtLog.AppendText($"Hedef klasör mevcut değil, oluşturuluyor: {targetDir}\n");
                    Directory.CreateDirectory(targetDir);
                    txtLog.AppendText($"✓ Hedef klasör oluşturuldu: {targetDir}\n");
                }
                else if (!string.IsNullOrEmpty(targetDir))
                {
                    txtLog.AppendText($"✓ Hedef klasör zaten mevcut: {targetDir}\n");
                }

                // Assembly'den kaynağı bul
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"Yafes.Resources.{resourceFileName}";

                txtLog.AppendText($"Aranan kaynak: {resourceName}\n");

                // Önce kaynağın varlığını kontrol et
                var allResources = assembly.GetManifestResourceNames();
                bool resourceFound = false;
                string actualResourceName = resourceName;

                foreach (var res in allResources)
                {
                    if (res.Equals(resourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceFound = true;
                        actualResourceName = res;
                        txtLog.AppendText($"✓ Kaynak bulundu: {res}\n");
                        break;
                    }
                }

                // Eğer tam eşleşme bulunamazsa, içeriyor mu kontrol et
                if (!resourceFound)
                {
                    foreach (var res in allResources)
                    {
                        if (res.Contains(resourceFileName.Replace(".", "")) || res.EndsWith(resourceFileName))
                        {
                            resourceFound = true;
                            actualResourceName = res;
                            txtLog.AppendText($"✓ Alternatif kaynak bulundu: {res}\n");
                            break;
                        }
                    }
                }

                // Hala bulunamadıysa hata ver
                if (!resourceFound)
                {
                    txtLog.AppendText("✗ Kaynak bulunamadı! Mevcut kaynaklar:\n");
                    foreach (var res in allResources)
                    {
                        txtLog.AppendText($"  - {res}\n");
                    }
                    throw new Exception($"Kaynak dosyası bulunamadı: {resourceName}");
                }

                // Kaynağı stream olarak al
                using (Stream? resourceStream = assembly.GetManifestResourceStream(actualResourceName))
                {
                    if (resourceStream == null)
                    {
                        throw new Exception($"Kaynak stream'i alınamadı: {actualResourceName}");
                    }

                    txtLog.AppendText($"Kaynak boyutu: {resourceStream.Length} bayt\n");

                    // Dosyayı hedef yere kopyala
                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                    }
                }

                // Kopyalanan dosyayı doğrula
                if (File.Exists(targetPath))
                {
                    var fileInfo = new FileInfo(targetPath);
                    txtLog.AppendText($"✓ Dosya başarıyla kopyalandı!\n");
                    txtLog.AppendText($"  Hedef: {targetPath}\n");
                    txtLog.AppendText($"  Boyut: {fileInfo.Length} bayt\n");
                    txtLog.AppendText($"  Oluşturma zamanı: {fileInfo.CreationTime}\n");
                }
                else
                {
                    throw new Exception("Dosya kopyalandı ancak hedef yolda bulunamıyor!");
                }
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"✗ Dosya kopyalama hatası ({resourceFileName}): {ex.Message}\n");
                throw;
            }
        }
        private async Task InstallDriverBoosterWithSpecialFiles(ProgramInfo program, string installPath)
        {
            try
            {
                txtLog.AppendText($"Driver Booster özel kurulumu başlatılıyor...\n");

                // Normal kurulum işlemini başlat
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = installPath,
                    Arguments = program.InstallArguments,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Process? installProcess = Process.Start(psi);

                if (installProcess == null)
                {
                    throw new Exception("Driver Booster kurulum işlemi başlatılamadı!");
                }

                // Kurulum sürecini bekle
                await Task.Run(() => {
                    installProcess.WaitForExit();

                    // Kurulum tamamlandıktan sonra ek bekleme
                    Dispatcher.Invoke(() => {
                        txtLog.AppendText("Driver Booster kurulumu tamamlandı, özel dosyalar kopyalanıyor...\n");
                    });
                    System.Threading.Thread.Sleep(3000); // 3 saniye bekle
                });

                // version.dll dosyasını hedef klasöre kopyala
                await CopySpecialFileFromResources("version.dll", @"C:\Program Files (x86)\IObit\Driver Booster\12.4.0\version.dll");

                txtLog.AppendText("Driver Booster özel kurulumu başarıyla tamamlandı!\n");
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"Driver Booster özel kurulum hatası: {ex.Message}\n");
                throw;
            }
        }
       

      
        // Program kurulum hatası yönetimi
        private void HandleProgramError()
        {
            // Hata durumunda bir sonraki programa geç
            currentProgramIndex++;
            StartNextProgramInstallation();
        }

        private void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentCategory == "Sürücüler")
                {
                    // Sürücü klasörünü aç
                    if (!Directory.Exists(driversFolder))
                        Directory.CreateDirectory(driversFolder);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = driversFolder,
                        UseShellExecute = true
                    });
                }
                else if (currentCategory == "Programlar")
                {
                    // Program klasörünü aç
                    if (!Directory.Exists(programsFolder))
                        Directory.CreateDirectory(programsFolder);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = programsFolder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                txtLog.AppendText("Klasör açılırken hata: " + ex.Message + "\n");
            }
        }

        private void btnAddDriver_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Kurulum Dosyaları (*.exe;*.msi;*.zip)|*.exe;*.msi;*.zip|Tüm Dosyalar (*.*)|*.*";

                if (currentCategory == "Sürücüler")
                {
                    openFileDialog.Title = "Sürücü Seç";

                    if (openFileDialog.ShowDialog() == true)
                    {
                        string filePath = openFileDialog.FileName;
                        string fileName = Path.GetFileName(filePath);
                        string driverName = Path.GetFileNameWithoutExtension(filePath);
                        bool isZip = Path.GetExtension(filePath).ToLower() == ".zip";

                        // Sürücüyü listeye ekle
                        DriverInfo newDriver = new DriverInfo
                        {
                            Name = driverName,
                            Url = string.Empty, // Url boş olabilir, kullanıcı tarafından eklenen sürücü için
                            FileName = fileName,
                            ProcessName = "setup",
                            InstallArguments = "/s",
                            IsZip = isZip,
                            AlternativeSearchPattern = Path.GetFileName(filePath) // Dosya adını alternatif desen olarak kullan
                        };

                        // Dosyayı Drivers klasörüne kopyala
                        string destPath = Path.Combine(driversFolder, fileName);
                        File.Copy(filePath, destPath, true);
                        txtLog.AppendText($"Dosya başarıyla kopyalandı: {destPath}\n");

                        // Yeni sürücüyü listeye ekle
                        drivers.Add(newDriver);
                        driverStatusMap[newDriver.Name] = "Bekliyor";

                        // Sürücü listesini güncelle
                        var checkBox = new CheckBox
                        {
                            Content = driverName,
                            IsChecked = true,
                            Tag = newDriver
                        };
                        var item = new ListBoxItem();
                        item.Content = checkBox;
                        lstDrivers.Items.Add(item);
                    }
                }
                else if (currentCategory == "Programlar")
                {
                    openFileDialog.Title = "Program Seç";

                    if (openFileDialog.ShowDialog() == true)
                    {
                        string filePath = openFileDialog.FileName;
                        string fileName = Path.GetFileName(filePath);
                        string programName = Path.GetFileNameWithoutExtension(filePath);
                        bool isZip = Path.GetExtension(filePath).ToLower() == ".zip";

                        // Program ekle
                        ProgramInfo newProgram = new ProgramInfo
                        {
                            Name = programName,
                            Url = string.Empty, // Url boş olabilir, kullanıcı tarafından eklenen program için
                            FileName = fileName,
                            ProcessName = Path.GetFileNameWithoutExtension(fileName),
                            InstallArguments = "/S", // Varsayılan sessiz kurulum parametresi
                            IsZip = isZip,
                            AlternativeSearchPattern = Path.GetFileName(filePath), // Dosya adını alternatif desen olarak kullan
                            SpecialInstallation = false
                        };

                        // Dosyayı Programs klasörüne kopyala
                        string destPath = Path.Combine(programsFolder, fileName);
                        File.Copy(filePath, destPath, true);
                        txtLog.AppendText($"Dosya başarıyla kopyalandı: {destPath}\n");

                        // Yeni programı listeye ekle
                        programs.Add(newProgram);
                        programStatusMap[newProgram.Name] = "Bekliyor";

                        // Program listesini güncelle
                        var checkBox = new CheckBox
                        {
                            Content = programName,
                            IsChecked = true,
                            Tag = newProgram
                        };
                        var item = new ListBoxItem();
                        item.Content = checkBox;
                        lstDrivers.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                txtLog.AppendText("Dosya ekleme hatası: " + ex.Message + "\n");
            }
        }

        private void chkRestart_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (chkRestart.IsChecked == true)
            {
                txtLog.AppendText("Kurulum sonrası yeniden başlatma seçeneği aktif edildi\n");
            }
            else
            {
                txtLog.AppendText("Kurulum sonrası yeniden başlatma seçeneği kapatıldı\n");
            }
        }

        // Driver sınıfını genişlet - ResourceName özelliği ekle
        public class DriverInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string ProcessName { get; set; } = string.Empty;
            public string InstallArguments { get; set; } = string.Empty;
            public bool IsZip { get; set; }
            public string AlternativeSearchPattern { get; set; } = string.Empty;
            public string ResourceName { get; set; } = string.Empty; // Gömülü kaynak adı
        }

        // Program bilgisi sınıfını genişlet - ResourceName özelliği ekle
        public class ProgramInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string ProcessName { get; set; } = string.Empty;
            public string InstallArguments { get; set; } = string.Empty;
            public bool IsZip { get; set; }
            public string AlternativeSearchPattern { get; set; } = string.Empty;
            public string ResourceName { get; set; } = string.Empty; // Gömülü kaynak adı
            public bool SpecialInstallation { get; set; } = false; // Özel kurulum yöntemi
        }

        private void txtLog_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}