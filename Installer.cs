using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

internal class FoxyInstaller : Form
{
    private Label statusLabel;
    private ProgressBar progressBar;
    private Button actionButton;
    private Label titleLabel;
    private CheckBox launchCheckBox;

    private static string AppName = "FoxyClient";
    private static string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private const string RootCertificateFileName = "root-ca.crt";
    
    // Base directory where the installer is running from.
    private static string SourceDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

    [STAThread]
    static void Main()
    {
        // Re-launch as administrator if not already elevated
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Assembly.GetEntryAssembly().Location,
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
                return;
            }
        }
        catch { /* If elevation fails, continue anyway */ }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new FoxyInstaller());
    }

    public FoxyInstaller()
    {
        this.Text = "FoxyClient Official Setup";
        this.Size = new Size(450, 300);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(25, 25, 25);
        this.ForeColor = Color.White;

        titleLabel = new Label()
        {
            Text = "Install FoxyClient",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 140, 0)
        };

        statusLabel = new Label()
        {
            Text = "Press 'Install' to begin the deployment to your system.",
            Location = new Point(25, 80),
            Size = new Size(400, 40),
            Font = new Font("Segoe UI", 10)
        };

        progressBar = new ProgressBar()
        {
            Location = new Point(25, 140),
            Size = new Size(385, 25),
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };

        launchCheckBox = new CheckBox()
        {
            Text = "Run FoxyClient after finish",
            Location = new Point(25, 175),
            AutoSize = true,
            Checked = true,
            Visible = false,
            Font = new Font("Segoe UI", 9)
        };

        actionButton = new Button()
        {
            Text = "Install Now",
            Location = new Point(160, 210),
            Size = new Size(120, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 50),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        actionButton.FlatAppearance.BorderSize = 0;
        actionButton.Click += (s, e) => {
            actionButton.Enabled = false;
            progressBar.Visible = true;
            Thread thread = new Thread(RunInstallation);
            thread.Start();
        };

        this.Controls.Add(titleLabel);
        this.Controls.Add(statusLabel);
        this.Controls.Add(progressBar);
        this.Controls.Add(launchCheckBox);
        this.Controls.Add(actionButton);
    }

    private void RunInstallation()
    {
        try
        {
            UpdateStatusUI("Initializing directories...", 10);
            if (!Directory.Exists(InstallDir)) Directory.CreateDirectory(InstallDir);

            UpdateStatusUI("Extracting application payload...", 30);
            ExtractEmbeddedSetup();

            UpdateStatusUI("Installing FoxySec root certificate...", 60);
            InstallRootCertificate();

            UpdateStatusUI("Creating system shortcuts...", 80);
            CreateShortcut("Desktop", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk"));
            CreateShortcut("StartMenu", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), AppName + ".lnk"));

            UpdateStatusUI("Registering program in system...", 95);
            RegisterProgram();

            this.Invoke((MethodInvoker)delegate {
                progressBar.Value = 100;
                statusLabel.Text = "Installation successful! You're ready to play.";
                launchCheckBox.Visible = true;
                actionButton.Text = "Finish";
                actionButton.Enabled = true;
                actionButton.Click += (s, ev) => {
                    if (launchCheckBox.Checked)
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = Path.Combine(InstallDir, "FoxyClient.exe"),
                            WorkingDirectory = InstallDir
                        });
                    }
                    Application.Exit();
                };
            });
        }
        catch (Exception ex)
        {
            this.Invoke((MethodInvoker)delegate {
                statusLabel.Text = "Error: " + ex.Message;
                actionButton.Enabled = true;
                actionButton.Text = "Retry";
            });
            MessageBox.Show("An error occurred during installation:\n" + ex.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExtractEmbeddedSetup()
    {
        string tempZip = Path.Combine(Path.GetTempPath(), "foxy_setup.zip");
        
        // 1. Save embedded resource to disk
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream("Setup.zip"))
        {
            if (stream == null) throw new Exception("Embedded payload not found in assembly!");
            using (FileStream fs = new FileStream(tempZip, FileMode.Create))
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, read);
                }
            }
        }

        // 2. Extract ZIP using Shell.Application (COM)
        try
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(shellType);
            var zipFile = shell.NameSpace(tempZip);
            var destFolder = shell.NameSpace(InstallDir);
            
            // 4 = progress, 16 = yes to all, 512 = no UI
            destFolder.CopyHere(zipFile.Items(), 4 | 16 | 512); 
            
            // Give system a second to finalize I/O
            Thread.Sleep(1000);
        }
        finally
        {
            if (File.Exists(tempZip)) File.Delete(tempZip);
        }
    }

    private void InstallRootCertificate()
    {
        string certificatePath = Path.Combine(InstallDir, RootCertificateFileName);
        for (int attempt = 0; attempt < 40 && !File.Exists(certificatePath); attempt++)
        {
            Thread.Sleep(250);
        }

        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException("The bundled root certificate was not found.", certificatePath);
        }

        using (var certificate = new X509Certificate2(certificatePath))
        {
            bool isCertificateAuthority = false;
            foreach (X509Extension extension in certificate.Extensions)
            {
                var constraints = extension as X509BasicConstraintsExtension;
                if (constraints != null)
                {
                    isCertificateAuthority = constraints.CertificateAuthority;
                    break;
                }
            }

            if (!isCertificateAuthority || !string.Equals(certificate.Subject, certificate.Issuer, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException("The bundled certificate is not a self-signed root CA.");
            }

            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2Collection matches = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    certificate.Thumbprint,
                    false);

                if (matches.Count == 0)
                {
                    store.Add(certificate);
                }
            }
        }
    }

    private void RegisterProgram()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", AppName);
                    key.SetValue("DisplayVersion", "1.4.9");
                    key.SetValue("Publisher", "FoxyClient");
                    key.SetValue("InstallLocation", InstallDir);
                    key.SetValue("DisplayIcon", Path.Combine(InstallDir, "FoxyClient.exe"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(InstallDir, "FoxyClient.exe") + "\" --uninstall");
                    key.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
        }
        catch (Exception ex)
        {
            // Non-critical failure
            Console.WriteLine("Registry registration failed: " + ex.Message);
        }
    }

    private void UpdateStatusUI(string text, int progress)
    {
        if (this.InvokeRequired)
        {
            this.Invoke((Action)(() => UpdateStatusUI(text, progress)));
            return;
        }
        statusLabel.Text = text;
        if (progress > 0) progressBar.Value = progress;
    }

    private void CopyFile(string fileName)
    {
        string source = Path.Combine(SourceDir, fileName);
        string dest = Path.Combine(InstallDir, fileName);
        if (File.Exists(source))
        {
            File.Copy(source, dest, true);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
        }
    }

    private static void CreateShortcut(string type, string shortcutPath)
    {
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = Path.Combine(InstallDir, "FoxyClient.exe");
            shortcut.WorkingDirectory = InstallDir;
            shortcut.Description = "FoxyClient Launcher";
            shortcut.Save();
        }
        catch { }
    }
}
